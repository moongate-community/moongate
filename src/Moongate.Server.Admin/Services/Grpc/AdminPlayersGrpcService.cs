using System.Globalization;
using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Serilog;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Server.Admin.Services.Grpc;

internal sealed class AdminPlayersGrpcService : AdminPlayers.AdminPlayersBase
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 200;
    private const int MaximumReasonLength = 200;

    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _packets;

    public AdminPlayersGrpcService(AdminWorldServices world)
    {
        _sessions = world.Sessions;
        _mobiles = world.Mobiles;
        _packets = world.Packets;
    }

    public override Task<ListOnlinePlayersResponse> ListOnlinePlayers(ListOnlinePlayersRequest request, ServerCallContext context)
    {
        if (request.PageSize > MaximumPageSize)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, $"Page size cannot exceed {MaximumPageSize}."));
        }

        var size = request.PageSize == 0 ? DefaultPageSize : (int)request.PageSize;

        // The characters that are in the world, by character id: the cursor is the last one given.
        var page = _sessions.GetAll()
            .Where(session => session.CharacterId.Value > request.AfterCharacterId)
            .OrderBy(session => session.CharacterId.Value)
            .Select(session => (Session: session, Found: _mobiles.TryGet(session.CharacterId, out var mobile), Mobile: mobile))
            .Where(entry => entry.Found)
            .Take(size + 1)
            .ToList();

        var response = new ListOnlinePlayersResponse();

        foreach (var (session, _, mobile) in page.Take(size))
        {
            response.Players.Add(
                new OnlinePlayer
                {
                    SessionId = session.SessionId, AccountId = session.AccountId.Value, CharacterId = session.CharacterId.Value,
                    Name = mobile!.Name ?? "", AccountType = ToContract(session.AccountType), Map = (int)mobile.Map,
                    X = mobile.Location.X, Y = mobile.Location.Y, Z = mobile.Location.Z
                }
            );
        }

        response.NextAfterCharacterId = page.Count > size ? response.Players[^1].CharacterId : 0;

        return Task.FromResult(response);
    }

    public override async Task<Empty> KickPlayer(KickPlayerRequest request, ServerCallContext context)
    {
        if (request.CharacterId == 0)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Character ID must be nonzero."));
        }

        if (request.Reason.Length > MaximumReasonLength || request.Reason.Contains('\0'))
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "The reason is too long or malformed."));
        }

        var http = context.GetHttpContext();
        http.Items["AdminTargetId"] = request.CharacterId;

        if (!_sessions.TryGetByCharacterId(new(request.CharacterId), out var target))
        {
            throw new RpcException(new(StatusCode.NotFound, "Player not online."));
        }

        if (http.User.FindFirstValue(ClaimTypes.NameIdentifier) == target.AccountId.Value.ToString(CultureInfo.InvariantCulture))
        {
            throw new RpcException(new(StatusCode.FailedPrecondition, "You cannot kick your own account."));
        }

        // A game master cannot touch an account of its own level or higher; an administrator can touch anyone.
        if (!http.User.IsInRole(AdminAuthorizationPolicies.AdministratorRole) && target.AccountType >= DomainAccountType.GameMaster)
        {
            throw new RpcException(
                new(StatusCode.PermissionDenied, "A game master cannot kick an account of its own level or higher.")
            );
        }

        Log.ForContext<AdminPlayersGrpcService>()
            .Information(
                "Admin kick of character {CharacterId} (account {AccountId}), reason {Reason}",
                request.CharacterId,
                target.AccountId.Value,
                request.Reason
            );
        await _packets.DisconnectAsync(target.SessionId);

        return new();
    }

    private static AccountType ToContract(DomainAccountType type)
    {
        return type switch
        {
            DomainAccountType.Regular       => AccountType.Regular,
            DomainAccountType.GameMaster    => AccountType.GameMaster,
            DomainAccountType.Administrator => AccountType.Administrator,
            _                               => AccountType.Unspecified
        };
    }
}
