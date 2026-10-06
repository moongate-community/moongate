using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class BulletinBoardsConfigTests
{
    [Fact]
    public void Defaults_AreSevenDaysFiftyMessagesAndTheTwoWaits()
    {
        var config = new BulletinBoardsConfig();

        Assert.Equal((7, 50, 120, 30), (config.ExpireDays, config.MaxMessages, config.ThreadSeconds, config.ReplySeconds));
        config.Validate();
    }

    [Theory,
     InlineData(-1, 50, 120, 30, "ultima.bulletin_boards.expire_days"),
     InlineData(3651, 50, 120, 30, "ultima.bulletin_boards.expire_days"),
     InlineData(7, 0, 120, 30, "ultima.bulletin_boards.max_messages"),
     InlineData(7, 201, 120, 30, "ultima.bulletin_boards.max_messages"),
     InlineData(7, 50, -1, 30, "ultima.bulletin_boards.thread_seconds"),
     InlineData(7, 50, 86401, 30, "ultima.bulletin_boards.thread_seconds"),
     InlineData(7, 50, 120, -1, "ultima.bulletin_boards.reply_seconds"),
     InlineData(7, 50, 120, 86401, "ultima.bulletin_boards.reply_seconds")]
    public void Validate_AValueOutOfRange_NamesTheSetting(int days, int messages, int thread, int reply, string setting)
    {
        var config = new BulletinBoardsConfig
            { ExpireDays = days, MaxMessages = messages, ThreadSeconds = thread, ReplySeconds = reply };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(0, 1, 0, 0), InlineData(3650, 200, 86400, 86400)]
    public void Validate_TheLimits_AreAccepted(int days, int messages, int thread, int reply)
    {
        new BulletinBoardsConfig { ExpireDays = days, MaxMessages = messages, ThreadSeconds = thread, ReplySeconds = reply }
            .Validate();
    }

    [Fact]
    public void UltimaConfig_HasTheBulletinBoardsSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(7, config.BulletinBoards.ExpireDays);

        config.BulletinBoards.MaxMessages = 0;

        Assert.Contains(
            "ultima.bulletin_boards.max_messages",
            Assert.Throws<InvalidOperationException>(config.Validate).Message
        );
    }
}
