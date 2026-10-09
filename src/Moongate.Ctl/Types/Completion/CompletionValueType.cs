namespace Moongate.Ctl.Types.Completion;

/// <summary>
///     What follows an option or stands as an argument, so a shell knows what to offer for it.
/// </summary>
internal enum CompletionValueType
{
    /// <summary>
    ///     Nothing: the option is a flag.
    /// </summary>
    None,

    /// <summary>
    ///     Free text the shell cannot guess, such as a list of host names.
    /// </summary>
    Text,

    /// <summary>
    ///     A directory.
    /// </summary>
    Directory,

    /// <summary>
    ///     One of a few fixed words.
    /// </summary>
    Choice
}
