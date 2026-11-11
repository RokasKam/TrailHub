using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;

namespace TrailHub.ArchitectureTests;

/// <summary>
/// Enforces the repository → service → controller layering: controllers only talk to service interfaces,
/// services live in Application and implement those interfaces, repositories implement Application's
/// repository interfaces in Infrastructure, and no type is nested inside another.
/// </summary>
public sealed class LayeringRuleTests
{
    private static readonly string[] ServiceNames = ["Profile", "Trails", "Activity", "Trips", "Groups", "Routes", "Weather"];

    private static readonly string[] SharedAssemblies =
    [
        "TrailHub.BuildingBlocks.Domain",
        "TrailHub.BuildingBlocks.Application",
        "TrailHub.BuildingBlocks.Infrastructure",
        "TrailHub.BuildingBlocks.Web",
        "TrailHub.Contracts",
    ];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];

    public static TheoryData<string> Services => new(ServiceNames);

    public static TheoryData<string> AllAssemblies => new(
        ServiceNames.SelectMany(s => Layers.Select(layer => $"TrailHub.{s}.{layer}"))
            .Concat(SharedAssemblies));

    [Theory]
    [MemberData(nameof(AllAssemblies))]
    public void No_type_is_nested_in_another(string assembly)
    {
        var nested = Assembly.Load(assembly).GetTypes()
            .Where(t => t.IsNested && !IsCompilerGenerated(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(nested.Count == 0, $"Nested types: {string.Join(", ", nested)}");
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Controllers_depend_only_on_service_interfaces(string service)
    {
        var controllers = Assembly.Load($"TrailHub.{service}.Api").GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();
        Assert.NotEmpty(controllers);

        var violations = controllers
            .SelectMany(c => c.GetConstructors().SelectMany(ctor => ctor.GetParameters()).Select(p => (Controller: c, p.ParameterType)))
            .Where(x => !x.ParameterType.IsInterface || x.ParameterType.Namespace != $"TrailHub.{service}.Application.Interfaces.Service")
            .Select(x => $"{x.Controller.Name} → {x.ParameterType.Name}")
            .ToList();

        Assert.True(violations.Count == 0, $"Controllers must only take service interfaces: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Service_interfaces_are_implemented_in_application_services(string service)
    {
        var application = Assembly.Load($"TrailHub.{service}.Application");
        var interfaces = application.GetTypes().Where(t => t.IsInterface && t.Namespace == $"TrailHub.{service}.Application.Interfaces.Service").ToList();
        Assert.NotEmpty(interfaces);

        var unimplemented = interfaces
            .Where(i => !application.GetTypes().Any(t => t is { IsClass: true, IsAbstract: false } && i.IsAssignableFrom(t) && t.Namespace == $"TrailHub.{service}.Application.Services"))
            .Select(i => i.Name)
            .ToList();

        Assert.True(unimplemented.Count == 0, $"No service in Application.Services implements: {string.Join(", ", unimplemented)}");
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Repository_interfaces_are_implemented_in_infrastructure_repositories(string service)
    {
        var interfaces = Assembly.Load($"TrailHub.{service}.Application").GetTypes()
            .Where(t => t.IsInterface && t.Namespace == $"TrailHub.{service}.Application.Interfaces.Repository")
            .ToList();
        var infrastructure = Assembly.Load($"TrailHub.{service}.Infrastructure").GetTypes();

        var unimplemented = interfaces
            .Where(i => !infrastructure.Any(t => t is { IsClass: true, IsAbstract: false } && i.IsAssignableFrom(t) && t.Namespace == $"TrailHub.{service}.Infrastructure.Repositories"))
            .Select(i => i.Name)
            .ToList();

        Assert.True(unimplemented.Count == 0, $"No repository in Infrastructure.Repositories implements: {string.Join(", ", unimplemented)}");
    }

    /// <summary>Closures, iterators, async state machines and source-generated helpers are nested by the compiler.</summary>
    private static bool IsCompilerGenerated(Type type) =>
        type.Name.Contains('<', StringComparison.Ordinal)
        || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || type.IsDefined(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), inherit: false)
        || (type.DeclaringType is { } declaring && IsCompilerGenerated(declaring));
}
