using System.Reflection;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.ArchitectureTests;

/// <summary>
/// Enforces the clean-architecture dependency rule: Domain ← Application ← Infrastructure ← Api.
/// The compiler only keeps references that are actually used, so these checks catch real leaks
/// (e.g. an EF type in a handler), not just a stray ProjectReference.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] ServiceNames = ["Profile", "Trails", "Activity", "Trips", "Groups", "Weather"];

    public static TheoryData<string> Services => new(ServiceNames);

    private static readonly string[] FrameworkAndIo =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "NetTopologySuite",
        "Microsoft.AspNetCore",
        "MassTransit",
        "StackExchange.Redis",
        "Microsoft.Extensions.Caching",
        "System.Net.Http",
    ];

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_depends_only_on_shared_domain_kernel(string service)
    {
        var references = ReferencesOf($"TrailHub.{service}.Domain");

        Assert.All(references.Where(r => r.StartsWith("TrailHub.", StringComparison.Ordinal)), r => Assert.Equal("TrailHub.BuildingBlocks.Domain", r));
        AssertNoneStartWith(references, FrameworkAndIo);
        AssertNoneStartWith(references, ["Microsoft.Extensions"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Application_has_no_infrastructure_or_web_dependencies(string service)
    {
        var references = ReferencesOf($"TrailHub.{service}.Application");

        AssertNoneStartWith(references, FrameworkAndIo);
        AssertNoneStartWith(references, [$"TrailHub.{service}.Infrastructure", $"TrailHub.{service}.Api", "TrailHub.BuildingBlocks.Infrastructure", "TrailHub.BuildingBlocks.Web"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Infrastructure_does_not_depend_on_the_api(string service)
    {
        var references = ReferencesOf($"TrailHub.{service}.Infrastructure");

        AssertNoneStartWith(references, [$"TrailHub.{service}.Api", "TrailHub.BuildingBlocks.Web"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Services_do_not_reference_each_other(string service)
    {
        var others = ServiceNames.Where(s => s != service).Select(s => $"TrailHub.{s}.").ToArray();
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
        {
            AssertNoneStartWith(ReferencesOf($"TrailHub.{service}.{layer}"), others);
        }
    }

    [Fact]
    public void Contracts_are_dependency_free()
    {
        AssertNoneStartWith(ReferencesOf("TrailHub.Contracts"), ["TrailHub.", "Microsoft.", "MassTransit"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Aggregates_expose_no_public_setters(string service)
    {
        var aggregates = Assembly.Load($"TrailHub.{service}.Domain").GetTypes()
            .Where(t => t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() == typeof(AggregateRoot<>));

        var publicSetters = aggregates
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.SetMethod is { IsPublic: true })
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        Assert.Empty(publicSetters);
    }

    private static IReadOnlyList<string> ReferencesOf(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!).ToList();

    private static void AssertNoneStartWith(IEnumerable<string> references, IEnumerable<string> forbiddenPrefixes)
    {
        var violations = references.Where(r => forbiddenPrefixes.Any(f => r.StartsWith(f, StringComparison.Ordinal))).ToList();
        Assert.True(violations.Count == 0, $"Forbidden dependencies: {string.Join(", ", violations)}");
    }
}
