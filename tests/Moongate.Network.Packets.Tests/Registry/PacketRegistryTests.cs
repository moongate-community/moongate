using System.Collections;

using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Tests.Support.Registry;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Registry;

public class PacketRegistryTests
{
    [Fact]
    public void Default_ClientVersionDirections_HaveDistinctDescriptors()
    {
        var registry = PacketRegistry.Default;

        Assert.True(registry.TryGetDescriptor(0xBD, PacketDirection.Incoming, out var response));
        Assert.True(registry.TryGetDescriptor(0xBD, PacketDirection.Outgoing, out var request));
        Assert.Equal(typeof(ClientVersionPacket), response.PacketType);
        Assert.Null(response.FixedLength);
        Assert.Equal(4, response.MinimumLength);
        Assert.Equal(typeof(ClientVersionRequestPacket), request.PacketType);
        Assert.Equal(3, request.FixedLength);
        Assert.Equal(11, registry.RegisteredPackets.Count);
        Assert.True(registry.IsFrozen);
    }

    [Fact]
    public void Default_PingDirections_ShareOneCachedDescriptor()
    {
        var registry = PacketRegistry.Default;

        Assert.True(registry.TryGetDescriptor(0x73, PacketDirection.Incoming, out var incoming));
        Assert.True(registry.TryGetDescriptor(0x73, PacketDirection.Outgoing, out var outgoing));
        Assert.Same(incoming, outgoing);
        Assert.Same(PingPacket.Descriptor, incoming);
        Assert.Equal(PacketDirection.Both, incoming.Direction);
    }

    [Fact]
    public void Default_Inventory_HasExpectedMetadata()
    {
        var expected = new (byte OpCode, Type Type, PacketSizing Sizing, int? Fixed, int Minimum, PacketDirection Direction)[]
        {
            (0x55, typeof(LoginCompletePacket), PacketSizing.Fixed, 1, 1, PacketDirection.Outgoing),
            (0x73, typeof(PingPacket), PacketSizing.Fixed, 2, 2, PacketDirection.Both),
            (0x80, typeof(AccountLoginPacket), PacketSizing.Fixed, 62, 62, PacketDirection.Incoming),
            (0x82, typeof(LoginDeniedPacket), PacketSizing.Fixed, 2, 2, PacketDirection.Outgoing),
            (0x8C, typeof(ServerRedirectPacket), PacketSizing.Fixed, 11, 11, PacketDirection.Outgoing),
            (0x91, typeof(GameLoginPacket), PacketSizing.Fixed, 65, 65, PacketDirection.Incoming),
            (0xA0, typeof(ServerSelectPacket), PacketSizing.Fixed, 3, 3, PacketDirection.Incoming),
            (0xA8, typeof(ServerListPacket), PacketSizing.Variable, null, 6, PacketDirection.Outgoing),
            (0xBD, typeof(ClientVersionRequestPacket), PacketSizing.Fixed, 3, 3, PacketDirection.Outgoing),
            (0xBD, typeof(ClientVersionPacket), PacketSizing.Variable, null, 4, PacketDirection.Incoming),
            (0xEF, typeof(LoginSeedPacket), PacketSizing.Fixed, 21, 21, PacketDirection.Incoming)
        };

        Assert.Equal(expected.Length, PacketRegistry.Default.RegisteredPackets.Count);
        foreach (var item in expected)
        {
            var descriptor = Assert.Single(PacketRegistry.Default.RegisteredPackets, candidate => candidate.PacketType == item.Type);
            Assert.Equal(item.OpCode, descriptor.OpCode);
            Assert.Equal(item.Sizing, descriptor.Sizing);
            Assert.Equal(item.Fixed, descriptor.FixedLength);
            Assert.Equal(item.Minimum, descriptor.MinimumLength);
            Assert.Equal(item.Direction, descriptor.Direction);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void TryGetDescriptor_AmbiguousOrUnknownDirection_ReturnsFalse(int direction)
    {
        Assert.False(PacketRegistry.Default.TryGetDescriptor(0x73, (PacketDirection)direction, out var descriptor));
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryDecode_KnownIncomingPackets_DispatchesAndReadsFields()
    {
        Assert.True(PacketRegistry.Default.TryDecode([0x73, 0x2A], out var pingPacket));
        Assert.Equal((byte)42, Assert.IsType<PingPacket>(pingPacket).Sequence);

        Assert.True(PacketRegistry.Default.TryDecode(Convert.FromHexString("EF1234567800000007000000000000006D00000000"), out var seedPacket));
        var seed = Assert.IsType<LoginSeedPacket>(seedPacket);
        Assert.Equal(0x12345678u, seed.Seed);
        Assert.Equal(7u, seed.Major);
        Assert.Equal(0u, seed.Minor);
        Assert.Equal(109u, seed.Revision);
        Assert.Equal(0u, seed.Patch);

        var account = new byte[62];
        account[0] = 0x80;
        account[1] = (byte)'a';
        account[31] = (byte)'b';
        account[61] = 0x5D;
        Assert.True(PacketRegistry.Default.TryDecode(account, out var accountPacket));
        var accountLogin = Assert.IsType<AccountLoginPacket>(accountPacket);
        Assert.Equal("a", accountLogin.Account);
        Assert.Equal("b", accountLogin.Password);
        Assert.Equal((byte)0x5D, accountLogin.NextLoginKey);

        var game = new byte[65];
        Convert.FromHexString("9112345678").CopyTo(game, 0);
        game[5] = (byte)'a';
        game[35] = (byte)'b';
        Assert.True(PacketRegistry.Default.TryDecode(game, out var gamePacket));
        var gameLogin = Assert.IsType<GameLoginPacket>(gamePacket);
        Assert.Equal(0x12345678u, gameLogin.AuthKey);
        Assert.Equal("a", gameLogin.Account);
        Assert.Equal("b", gameLogin.Password);

        Assert.True(PacketRegistry.Default.TryDecode(Convert.FromHexString("A01234"), out var selectPacket));
        Assert.Equal((ushort)0x1234, Assert.IsType<ServerSelectPacket>(selectPacket).ServerIndex);

        Assert.True(PacketRegistry.Default.TryDecode(Convert.FromHexString("BD000C372E302E3130392E30"), out var versionPacket));
        var version = Assert.IsType<ClientVersionPacket>(versionPacket);
        Assert.Equal("7.0.109.0", version.Version);
        Assert.Equal(12, version.Length);
        Assert.True(PacketRegistry.Default.TryDecode(Convert.FromHexString("BD000D372E302E3130392E3000"), out versionPacket));
        Assert.Equal(13, Assert.IsType<ClientVersionPacket>(versionPacket).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("9900")]
    [InlineData("8204")]
    [InlineData("BD000400")]
    [InlineData("7300FF")]
    public void TryDecode_UnknownOutgoingOrMalformedFrame_ReturnsFalse(string hex)
    {
        Assert.False(PacketRegistry.Default.TryDecode(Convert.FromHexString(hex), out var packet));
        Assert.Null(packet);
    }

    [Fact]
    public void RegisterIncoming_BidirectionalCollision_IsAtomic()
    {
        var registry = new PacketRegistry();
        registry.RegisterOutgoing<OutgoingCollisionPacket>();

        Assert.Throws<InvalidOperationException>(() => registry.RegisterIncoming<BidirectionalCollisionPacket>());
        Assert.False(registry.TryGetDescriptor(0xE1, PacketDirection.Incoming, out _));
        Assert.True(registry.TryGetDescriptor(0xE1, PacketDirection.Outgoing, out var existing));
        Assert.Equal(typeof(OutgoingCollisionPacket), existing.PacketType);
        Assert.Single(registry.RegisteredPackets);
    }

    [Fact]
    public void Freeze_IsIdempotentClosesRegistrationAndReturnsReadOnlySnapshot()
    {
        var registry = new PacketRegistry();
        registry.RegisterIncoming<PingPacket>();
        registry.Freeze();
        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => registry.RegisterOutgoing<LoginCompletePacket>());
        Assert.Throws<NotSupportedException>(() => ((IList)registry.RegisteredPackets).RemoveAt(0));
        Assert.Single(registry.RegisteredPackets);
    }

    [Fact]
    public void Registration_DuplicateAndBidirectionalOutgoingMisuse_AreRejected()
    {
        var duplicate = new PacketRegistry();
        duplicate.RegisterIncoming<PingPacket>();

        Assert.Throws<InvalidOperationException>(() => duplicate.RegisterIncoming<PingPacket>());

        var wrongMethod = new PacketRegistry();
        var error = Assert.Throws<InvalidOperationException>(() => wrongMethod.RegisterOutgoing<PingPacket>());
        Assert.Contains("RegisterIncoming", error.Message, StringComparison.Ordinal);
        Assert.Empty(wrongMethod.RegisteredPackets);
    }

    [Fact]
    public void FrozenRegistry_SupportsConcurrentReadsAndDecodes()
    {
        var failures = 0;

        Parallel.For(0, 100, iteration =>
        {
            if (!PacketRegistry.Default.TryGetDescriptor(0x73, PacketDirection.Incoming, out _)
                || !PacketRegistry.Default.TryDecode([0x73, 0x2A], out var packet)
                || packet is not PingPacket { Sequence: 42 })
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void TryDecode_InvalidHeader_DoesNotInvokeParser()
    {
        var registry = new PacketRegistry();
        registry.RegisterIncoming<ThrowingParserPacket>();

        Assert.False(registry.TryDecode([0xD0], out var packet));
        Assert.Null(packet);
    }
}
