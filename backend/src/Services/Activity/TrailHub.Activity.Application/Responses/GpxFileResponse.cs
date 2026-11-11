namespace TrailHub.Activity.Application.Responses;

/// <summary>A GPX download: its suggested file name and XML content.</summary>
public sealed record GpxFileResponse(string FileName, string Content);
