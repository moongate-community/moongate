using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Types;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.Server.Utils;
using Moongate.UO.Data.Templates.Items;
using SixLabors.ImageSharp.Formats.Png;

namespace Moongate.Server.Http.Extensions;

internal static class ItemTemplateRouteExtensions
{
    public static IEndpointRouteBuilder MapItemTemplateRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.ItemTemplateService is null && context.ArtService is null)
        {
            return endpoints;
        }

        var itemTemplatesGroup = endpoints.MapGroup("/api/item-templates").WithTags("ItemTemplates");

        if (context.JwtOptions.IsEnabled)
        {
            itemTemplatesGroup.RequireAuthorization();
        }

        if (context.ItemTemplateService is not null)
        {
            itemTemplatesGroup.MapGet(
                                  "/",
                                  (
                                      int page,
                                      int pageSize,
                                      string? name,
                                      string? tag,
                                      CancellationToken cancellationToken
                                  ) => HandleGetItemTemplates(
                                      context,
                                      page,
                                      pageSize,
                                      name,
                                      tag,
                                      cancellationToken
                                  )
                              )
                              .WithName("ItemTemplatesGetPaged")
                              .WithSummary("Returns paged item templates.")
                              .Produces<MoongateHttpItemTemplatePage>()
                              .Produces(StatusCodes.Status400BadRequest);

            itemTemplatesGroup.MapGet(
                                  "/{id}",
                                  (string id, CancellationToken cancellationToken) =>
                                      HandleGetItemTemplateById(context, id, cancellationToken)
                              )
                              .WithName("ItemTemplatesGetById")
                              .WithSummary("Returns an item template by id.")
                              .Produces<MoongateHttpItemTemplateDetail>()
                              .Produces(StatusCodes.Status404NotFound);
        }

        if (context.ArtService is not null)
        {
            itemTemplatesGroup.MapGet(
                                  "/by-item-id/{itemId}/image",
                                  (string itemId, CancellationToken cancellationToken) =>
                                      HandleGetItemTemplateImageByItemId(context, itemId, cancellationToken)
                              )
                              .WithName("ItemTemplatesGetImageByItemId")
                              .WithSummary("Returns item art image by item graphic id (0x....).")
                              .Produces(StatusCodes.Status200OK, contentType: "image/png")
                              .Produces(StatusCodes.Status400BadRequest)
                              .Produces(StatusCodes.Status404NotFound);
        }

        return endpoints;
    }

    private static IResult HandleGetItemTemplateById(
        MoongateHttpRouteContext context,
        string id,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(id))
        {
            return TypedResults.BadRequest("id is required");
        }

        if (!context.ItemTemplateService!.TryGet(id, out var template) || template is null)
        {
            return TypedResults.NotFound();
        }

        return Results.Json(
            MapItemTemplateToHttpDetail(context, template),
            MoongateHttpJsonContext.Default.MoongateHttpItemTemplateDetail
        );
    }

    private static IResult HandleGetItemTemplateImageByItemId(
        MoongateHttpRouteContext context,
        string itemIdText,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (context.ArtService is null)
        {
            return TypedResults.NotFound();
        }

        if (!TryParseHexItemId(itemIdText, out var itemId))
        {
            return TypedResults.BadRequest("itemId must be in 0x... format");
        }

        var itemsImageDirectory = Path.Combine(context.DirectoriesConfig[DirectoryType.Images], "items");
        Directory.CreateDirectory(itemsImageDirectory);

        var normalizedHex = itemId.ToString("X4", CultureInfo.InvariantCulture);
        var cachePath = Path.Combine(itemsImageDirectory, $"0x{normalizedHex}.png");

        if (File.Exists(cachePath))
        {
            return Results.File(cachePath, "image/png");
        }

        var legacyPath = Directory.EnumerateFiles(itemsImageDirectory, $"*_{normalizedHex}.png")
                                  .FirstOrDefault();

        if (legacyPath is not null)
        {
            return Results.File(legacyPath, "image/png");
        }

        using var image = context.ArtService.GetArt(itemId);

        if (image is null)
        {
            return TypedResults.NotFound();
        }

        using var normalized = ItemImageNormalizer.CropAndPad(image);

        using (var stream = File.Create(cachePath))
        {
            normalized.Save(stream, new PngEncoder());
        }

        return Results.File(cachePath, "image/png");
    }

    private static IResult HandleGetItemTemplates(
        MoongateHttpRouteContext context,
        int page,
        int pageSize,
        string? name,
        string? tag,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        IEnumerable<ItemTemplateDefinition> templates = context.ItemTemplateService!.GetAll();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameTerm = name.Trim();
            templates = templates.Where(
                template => !string.IsNullOrWhiteSpace(template.Name) &&
                            template.Name.Contains(nameTerm, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagTerm = tag.Trim();
            templates = templates.Where(
                template => template.Tags.Any(
                    existingTag => !string.IsNullOrWhiteSpace(existingTag) &&
                                   existingTag.Contains(tagTerm, StringComparison.OrdinalIgnoreCase)
                )
            );
        }

        var filteredTemplates = templates.ToList();
        var totalCount = filteredTemplates.Count;
        var skip = (safePage - 1) * safePageSize;
        var items = filteredTemplates.Skip(skip)
                                     .Take(safePageSize)
                                     .Select(MapItemTemplateToHttpSummary)
                                     .ToList();

        var response = new MoongateHttpItemTemplatePage
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = items
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpItemTemplatePage);
    }

    private static MoongateHttpItemTemplateDetail MapItemTemplateToHttpDetail(
        MoongateHttpRouteContext context,
        ItemTemplateDefinition template
    )
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Category = template.Category,
            ItemId = template.ItemId,
            Description = template.Description,
            Tags = template.Tags.ToList(),
            ScriptId = template.ScriptId,
            Weight = template.Weight > 0 ? template.Weight : null,
            GoldValue = template.GoldValue.ToString(),
            Hue = template.Hue.ToString(),
            GumpId = template.GumpId,
            Rarity = template.Rarity.ToString(),
            Container = template.Container.ToList(),
            ContainerItems = template.Container
                                     .Select(
                                         containerId =>
                                         {
                                             if (!context.ItemTemplateService!.TryGet(containerId, out var childTemplate) ||
                                                 childTemplate is null)
                                             {
                                                 return null;
                                             }

                                             return new MoongateHttpItemTemplateContainerItem
                                             {
                                                 Id = childTemplate.Id,
                                                 Name = childTemplate.Name,
                                                 ItemId = childTemplate.ItemId
                                             };
                                         }
                                     )
                                     .OfType<MoongateHttpItemTemplateContainerItem>()
                                     .ToList(),
            Params = template.Params.ToDictionary(
                static kvp => kvp.Key,
                static kvp => new ItemTemplateParamDefinition
                {
                    Type = kvp.Value.Type,
                    Value = kvp.Value.Value
                },
                StringComparer.OrdinalIgnoreCase
            )
        };

    private static MoongateHttpItemTemplateSummary MapItemTemplateToHttpSummary(ItemTemplateDefinition template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Category = template.Category,
            ItemId = template.ItemId,
            Params = template.Params.ToDictionary(
                static kvp => kvp.Key,
                static kvp => new ItemTemplateParamDefinition
                {
                    Type = kvp.Value.Type,
                    Value = kvp.Value.Value
                },
                StringComparer.OrdinalIgnoreCase
            )
        };

    private static bool TryParseHexItemId(string itemIdText, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(itemIdText))
        {
            return false;
        }

        var value = itemIdText.Trim();

        if (!value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(
            value.AsSpan(2),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out itemId
        );
    }
}
