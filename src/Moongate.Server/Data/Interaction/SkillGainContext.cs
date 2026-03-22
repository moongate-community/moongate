using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Interaction;

/// <summary>
/// Carries action context used by skill gain policies such as anti-macro.
/// </summary>
public sealed record SkillGainContext(Point3D Location, Serial? TargetId);
