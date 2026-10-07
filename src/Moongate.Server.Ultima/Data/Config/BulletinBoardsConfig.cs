namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the bulletin boards: how long a thread lasts, how many messages a board holds and how long a
///     character waits between two posts.
/// </summary>
public sealed class BulletinBoardsConfig
{
    public const int MaximumExpireDays = 3650;
    public const int MaximumMessages = 200;
    public const int MaximumWaitSeconds = 86_400;

    /// <summary>
    ///     Gets or sets the days a thread lasts after its last reply; 0 keeps it forever.
    /// </summary>
    public int ExpireDays { get; set; } = 7;

    /// <summary>
    ///     Gets or sets the messages a board holds: beyond them the thread with the oldest last reply goes.
    /// </summary>
    public int MaxMessages { get; set; } = 50;

    /// <summary>
    ///     Gets or sets the seconds a character waits between two new threads on one board.
    /// </summary>
    public int ThreadSeconds { get; set; } = 120;

    /// <summary>
    ///     Gets or sets the seconds a character waits between two posts of any kind on one board.
    /// </summary>
    public int ReplySeconds { get; set; } = 30;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        Check("expire_days", ExpireDays, 0, MaximumExpireDays);
        Check("max_messages", MaxMessages, 1, MaximumMessages);
        Check("thread_seconds", ThreadSeconds, 0, MaximumWaitSeconds);
        Check("reply_seconds", ReplySeconds, 0, MaximumWaitSeconds);
    }

    private static void Check(string key, int value, int from, int to)
    {
        if (value < from || value > to)
        {
            throw new InvalidOperationException($"ultima.bulletin_boards.{key} must be from {from} to {to}, found {value}.");
        }
    }
}
