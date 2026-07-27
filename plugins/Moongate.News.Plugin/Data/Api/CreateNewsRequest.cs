namespace Moongate.News.Plugin.Data.Api;

/// <summary>Body for creating a news entry; the author is taken from the caller's token.</summary>
public sealed record CreateNewsRequest(string Title, string Body, bool IsPublished);
