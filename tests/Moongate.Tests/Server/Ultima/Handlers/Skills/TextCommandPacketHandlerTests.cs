using System.Text;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Skills;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Skills;

public sealed class TextCommandPacketHandlerTests : IAsyncLifetime
{
    private readonly RecordingSkillUseService _skills = new();

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public async Task InitializeAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
    }

    [Theory]
    [InlineData("21 0", SkillType.Hiding)]
    [InlineData("21", SkillType.Hiding)]
    [InlineData("0 0", SkillType.Alchemy)]
    [InlineData("21\t0", SkillType.Hiding)]
    public void Handle_AUseSkillCommand_UsesTheSkillItsTextStartsWith(string text, SkillType expected)
    {
        Handle(TextCommandPacket.UseSkill, text);

        Assert.Equal([(_session, expected)], _skills.Used);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hiding")]
    [InlineData(" 21 0")]
    [InlineData("-1 0")]
    [InlineData("9999 0")]
    [InlineData("99999999999999999999")]
    public void Handle_AUseSkillCommandThatNamesNoSkill_UsesNone(string text)
    {
        Handle(TextCommandPacket.UseSkill, text);

        Assert.Empty(_skills.Used);
    }

    [Theory]
    [InlineData(0x56)]
    [InlineData(0x58)]
    [InlineData(0xC7)]
    public void Handle_AnotherKindOfCommand_UsesNoSkill(byte kind)
    {
        Handle(kind, "21 0");

        Assert.Empty(_skills.Used);
    }

    private void Handle(byte kind, string text)
    {
        var body = Encoding.ASCII.GetBytes(text);
        var data = new byte[body.Length + 5];
        data[0] = 0x12;
        data[1] = (byte)(data.Length >> 8);
        data[2] = (byte)data.Length;
        data[3] = kind;
        body.CopyTo(data, 4);
        Assert.True(TextCommandPacket.TryParse(data, out var packet));

        new TextCommandPacketHandler(_skills).Handle(_session, packet);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
