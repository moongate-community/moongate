using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.News.Plugin.Data.Api;
using Moongate.News.Plugin.Interfaces;

namespace Moongate.News.Plugin.Endpoints;

/// <summary>Staff management of shard news.</summary>
public sealed class NewsAdminEndpoints : IApiEndpointRegistration
{
    private readonly INewsService _news;

    public NewsAdminEndpoints(INewsService news)
    {
        _news = news;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/admin/news", Create).WithName("CreateNews").WithTags("news").RequireAuthorization(HttpServerService.AdminPolicy);
        routes.MapGet("/api/v1/admin/news", ListAll).WithName("ListAllNews").WithTags("news").RequireAuthorization(HttpServerService.AdminPolicy);
        routes.MapGet("/api/v1/admin/news/{id}", GetOne).WithName("GetNewsAdmin").WithTags("news").RequireAuthorization(HttpServerService.AdminPolicy);
        routes.MapPut("/api/v1/admin/news/{id}", Update).WithName("UpdateNews").WithTags("news").RequireAuthorization(HttpServerService.AdminPolicy);
        routes.MapDelete("/api/v1/admin/news/{id}", Delete).WithName("DeleteNews").WithTags("news").RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>Creates a news entry, authored by the calling staff member.</summary>
    private async Task<IResult> Create(CreateNewsRequest request, ClaimsPrincipal user)
    {
        var author = user.FindFirstValue(ClaimTypes.Name) ?? "unknown";
        var news = await _news.CreateAsync(request.Title, request.Body, author, request.IsPublished);

        return TypedResults.Created($"/api/v1/news/{news.Id.Value}", NewsResponse.From(news));
    }

    /// <summary>Lists every news entry, drafts included, newest first.</summary>
    private IResult ListAll()
        => TypedResults.Ok(_news.GetAll().Select(NewsResponse.From).ToList());

    /// <summary>Returns one news entry in any state.</summary>
    private IResult GetOne(uint id)
        => _news.Get(new Serial(id)) is { } news ? TypedResults.Ok(NewsResponse.From(news)) : TypedResults.NotFound();

    /// <summary>Updates a news entry's title, body and published state.</summary>
    private async Task<IResult> Update(uint id, UpdateNewsRequest request)
    {
        var news = await _news.UpdateAsync(new Serial(id), request.Title, request.Body, request.IsPublished);

        return news is { } updated ? TypedResults.Ok(NewsResponse.From(updated)) : TypedResults.NotFound();
    }

    /// <summary>Deletes a news entry.</summary>
    private async Task<IResult> Delete(uint id)
        => await _news.DeleteAsync(new Serial(id)) ? TypedResults.NoContent() : TypedResults.NotFound();
}
