using Moongate.Server.Data.Interaction;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

internal sealed class MobileCombatModule
{
    private readonly IMobileService? _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;
    private readonly ISkillGainService? _skillGainService;
    private readonly Func<double> _skillCheckRollProvider;

    public MobileCombatModule(
        IMobileService? mobileService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService,
        IOutgoingPacketQueue? outgoingPacketQueue,
        ISkillGainService? skillGainService,
        Func<double>? skillCheckRollProvider = null
    )
    {
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _skillGainService = skillGainService;
        _skillCheckRollProvider = skillCheckRollProvider ?? Random.Shared.NextDouble;
    }

    public bool CheckSkill(UOMobileEntity mobile, string skillName, double minSkill, double maxSkill, uint targetId = 0)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (_skillGainService is null || !TryResolveSkillName(skillName, out var resolvedSkill))
        {
            return false;
        }

        var skill = mobile.GetSkill(resolvedSkill);

        if (skill is null)
        {
            return false;
        }

        var currentValue = skill.Value / 10.0;

        if (currentValue < minSkill)
        {
            return false;
        }

        if (currentValue >= maxSkill || minSkill >= maxSkill)
        {
            return true;
        }

        var successChance = Math.Clamp((currentValue - minSkill) / (maxSkill - minSkill), 0.0, 1.0);
        var wasSuccessful = successChance >= _skillCheckRollProvider();
        _ = _skillGainService.TryGain(
            mobile,
            resolvedSkill,
            successChance,
            wasSuccessful,
            new SkillGainContext(mobile.Location, targetId == 0 ? null : (Serial)targetId)
        );

        return wasSuccessful;
    }

    public bool Dismount(Serial riderId)
    {
        if (riderId == Serial.Zero || _mobileService is null)
        {
            return false;
        }

        return _mobileService.DismountAsync(riderId).GetAwaiter().GetResult();
    }

    public void RefreshMountedSession(Serial riderId, Serial mountId, bool isMounted)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(riderId, out var session) || session.Character is null)
        {
            return;
        }

        var rider = ResolveRuntimeMobile(riderId) ??
                    _mobileService?.GetAsync(riderId).GetAwaiter().GetResult() ??
                    session.Character;
        var mount = mountId == Serial.Zero
                        ? null
                        : ResolveRuntimeMobile(mountId) ??
                          _mobileService?.GetAsync(mountId).GetAwaiter().GetResult();

        if (_outgoingPacketQueue is null)
        {
            return;
        }

        MountedSelfRefreshHelper.Refresh(session, _outgoingPacketQueue, rider, mount, isMounted);
    }

    public bool TryMount(Serial riderId, Serial mountId, UOMobileEntity? rider, UOMobileEntity? mount)
    {
        if (riderId == Serial.Zero || mountId == Serial.Zero || _mobileService is null)
        {
            return false;
        }

        if (rider is not null)
        {
            _mobileService.CreateOrUpdateAsync(rider).GetAwaiter().GetResult();
        }

        if (mount is not null)
        {
            _mobileService.CreateOrUpdateAsync(mount).GetAwaiter().GetResult();
        }

        return _mobileService.TryMountAsync(riderId, mountId).GetAwaiter().GetResult();
    }

    private UOMobileEntity? ResolveRuntimeMobile(Serial mobileId)
    {
        if (_spatialWorldService is null)
        {
            return null;
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var runtimeMobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (runtimeMobile is not null)
            {
                return runtimeMobile;
            }
        }

        return null;
    }

    private static bool TryResolveSkillName(string skillName, out UOSkillName resolvedSkill)
    {
        resolvedSkill = default;

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        var normalized = new string(skillName.Where(char.IsLetterOrDigit).ToArray());

        foreach (var candidate in Enum.GetValues<UOSkillName>())
        {
            var candidateName = new string(candidate.ToString().Where(char.IsLetterOrDigit).ToArray());

            if (string.Equals(candidateName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedSkill = candidate;

                return true;
            }
        }

        return false;
    }
}
