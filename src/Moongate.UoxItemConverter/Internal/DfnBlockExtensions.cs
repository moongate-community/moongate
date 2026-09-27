namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads the inheritance lines of a <see cref="DfnBlock" /> one way for every pass.
/// </summary>
internal static class DfnBlockExtensions
{
    /// <summary>
    ///     Gets the <c>get=</c> targets of a block, empty when it has none; two or more are a random pick.
    /// </summary>
    public static string[] GetTargets(this DfnBlock block)
    {
        return block.Fields.TryGetValue("get", out var text)
            ? text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
    }

    /// <summary>
    ///     Gets the targets a block inherits from: its <c>getlbr=</c> when it has one (LBR is UOX3's default era and the
    ///     era line comes after <c>get=</c>), else its <c>get=</c> targets. The other era tags are ignored.
    /// </summary>
    public static string[] ParentTargets(this DfnBlock block)
    {
        return block.Fields.TryGetValue("getlbr", out var eraTarget) ? [eraTarget.Trim()] : block.GetTargets();
    }
}
