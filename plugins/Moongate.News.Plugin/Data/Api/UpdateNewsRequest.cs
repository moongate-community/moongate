namespace Moongate.News.Plugin.Data.Api;

/// <summary>Body for updating a news entry.</summary>
public sealed record UpdateNewsRequest(string Title, string Body, bool IsPublished);
