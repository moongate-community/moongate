using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.News.Plugin.Data.Api;
using Moongate.News.Plugin.Interfaces;

namespace Moongate.News.Plugin.Endpoints;

/// <summary>Public, read-only access to published shard news.</summary>
public sealed class NewsEndpoints : IApiEndpointRegistration
{
    private readonly INewsService _news;

    public NewsEndpoints(INewsService news)
    {
        _news = news;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/news", List)
            .WithName("ListNews")
            .WithTags("news")
            .Produces<IReadOnlyList<NewsResponse>>();
        routes.MapGet("/api/v1/news/{id}", GetOne)
            .WithName("GetNews")
            .WithTags("news")
            .Produces<NewsResponse>();
    }

    /// <summary>Lists published news, newest first.</summary>
    private IResult List()
        => TypedResults.Ok(_news.GetPublished().Select(NewsResponse.From).ToList());

    /// <summary>Returns one published news entry; 404 if it is missing or a draft.</summary>
    private IResult GetOne(uint id)
        => _news.Get(new Serial(id)) is { IsPublished: true } news
            ? TypedResults.Ok(NewsResponse.From(news))
            : TypedResults.NotFound();
}
