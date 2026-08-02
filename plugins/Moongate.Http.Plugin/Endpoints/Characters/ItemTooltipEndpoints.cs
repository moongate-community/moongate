using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Http.Plugin.Endpoints.Characters;

/// <summary>What the game says about one item a character is carrying or wearing.</summary>
public sealed class ItemTooltipEndpoints : IApiEndpointRegistration
{
    /// <summary>
    /// How long a tooltip waits for the game loop. Short on purpose: nobody hovering an item wants to
    /// watch a spinner, and a loop this busy has a worse problem than a missing tooltip.
    /// </summary>
    private static readonly TimeSpan _loopTimeout = TimeSpan.FromSeconds(2);

    private readonly CharacterAccessService _access;
    private readonly CharacterInventoryReader _inventory;
    private readonly IItemService _items;
    private readonly IOplService _opl;
    private readonly OplTextRenderer _renderer;
    private readonly IGameLoopContext _loop;

    public ItemTooltipEndpoints(
        CharacterAccessService access,
        CharacterInventoryReader inventory,
        IItemService items,
        IOplService opl,
        OplTextRenderer renderer,
        IGameLoopContext loop
    )
    {
        _access = access;
        _inventory = inventory;
        _items = items;
        _opl = opl;
        _renderer = renderer;
        _loop = loop;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/characters/{serial}/items/{itemSerial}/tooltip", GetTooltip)
                 .WithName("GetItemTooltip")
                 .WithTags("characters")
                 .Produces<ItemTooltipResponse>()
                 .RequireAuthorization(HttpServerService.PlayerPolicy);

    /// <summary>
    /// Whether the item is worn by the character or sits somewhere in its backpack. Reuses the reader
    /// that already walks containers with a cycle guard, rather than walking them again here.
    /// </summary>
    private bool Carries(MobileEntity mobile, Serial itemId)
    {
        var worn = _inventory.ReadEquipment(mobile);
        var carried = _inventory.ReadBackpack(mobile);
        var wanted = itemId.ToString();

        return Contains(worn, wanted) || Contains(carried, wanted);
    }

    private static bool Contains(IReadOnlyList<CharacterItemResponse> items, string serial)
        => items.Any(item => item.Serial == serial || Contains(item.Contents, serial));

    /// <summary>What the game says about one item the character is carrying or wearing.</summary>
    /// <remarks>
    /// The lines are the object property list the game client renders, resolved to text — the item's
    /// name, its weight, and whatever else the shard describes. A shard whose string table is not
    /// loaded gets an **empty** list rather than an error: the technical fields are still worth having.
    /// The route is nested under the character because an item knows its container, not whose it is.
    /// Authorization is the character's — an account reads its own, staff read anyone's — and the item
    /// must actually be in that character's equipment or backpack, so a serial cannot be probed here.
    /// Answers 503 when the game loop does not respond: the property list is built on the loop, whose
    /// cache is deliberately unsynchronized.
    /// </remarks>
    private async Task<IResult> GetTooltip(string serial, string itemSerial, ClaimsPrincipal user)
    {
        var (character, denial) = _access.Resolve(serial, user);

        if (denial is not null)
        {
            return denial;
        }

        if (!Serial.TryParse(itemSerial, out var itemId))
        {
            return NotFound();
        }

        // The item must be the character's. This is what nesting the route buys: no ownership chain
        // from an item up to an account exists, so the character's own tree is the check.
        if (!Carries(character!.Mobile, itemId))
        {
            return NotFound();
        }

        var item = _items.GetById(itemId);

        if (item is null)
        {
            return NotFound();
        }

        IReadOnlyList<string> lines;

        try
        {
            // On the loop: IOplService caches its snapshots without synchronization, by design, so
            // building one from a request thread would race the loop that owns it.
            lines = await _loop.InvokeAsync(() => Render(itemId), _loopTimeout);
        }
        catch (TimeoutException)
        {
            return Results.Problem(
                "The game loop did not respond; the tooltip is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        return Results.Ok(
            new ItemTooltipResponse(item.Id.ToString(), lines, item.TemplateId, item.ItemId, item.Hue.Value, item.Amount)
        );
    }

    /// <summary>
    /// One answer for an item that is not the character's, one that does not exist, and one whose
    /// serial is not a number: from outside, all three mean there is nothing to describe.
    /// </summary>
    private static IResult NotFound()
        => Results.Problem("No such item on that character.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// The property list as text. Entries the string table cannot describe are dropped rather than
    /// rendered as their raw cliloc, so a partially loaded table shows fewer lines instead of noise.
    /// </summary>
    private IReadOnlyList<string> Render(Serial itemId)
        => [.. _opl.GetOrBuild(itemId).Entries.Select(_renderer.Render).OfType<string>()];
}
