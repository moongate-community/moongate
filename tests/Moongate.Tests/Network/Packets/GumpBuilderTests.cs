using Moongate.Network.Packets.Helpers;
using Moongate.Network.Packets.Outgoing.UI;

namespace Moongate.Tests.Network.Packets;

public class GumpBuilderTests
{
    [Test]
    public void Build_ShouldComposeLayoutWithStableTokenSpacing()
    {
        var layout = new GumpBuilder()
                     .ResizePic(10, 20, 5054, 250, 180)
                     .NoMove()
                     .NoClose()
                     .BuildLayout();

        Assert.That(
            layout,
            Is.EqualTo("{ resizepic 10 20 5054 250 180 } { nomove } { noclose }")
        );
    }

    [Test]
    public void ButtonMethods_ShouldWriteExpectedButtonTypes()
    {
        var layout = new GumpBuilder()
                     .Button(50, 80, 4005, 4007, 1)
                     .ButtonPage(90, 80, 4011, 4013, 2)
                     .BuildLayout();

        Assert.That(
            layout,
            Is.EqualTo("{ button 50 80 4005 4007 1 0 1 } { button 90 80 4011 4013 0 2 0 }")
        );
    }

    [Test]
    public void CheckBox_ShouldWriteSwitchIdAndInitialState()
    {
        var layout = new GumpBuilder()
                     .CheckBox(20, 25, 210, 211, 42)
                     .CheckBox(20, 45, 210, 211, 43, true)
                     .BuildLayout();

        Assert.That(
            layout,
            Is.EqualTo("{ checkbox 20 25 210 211 0 42 } { checkbox 20 45 210 211 1 43 }")
        );
    }

    [Test]
    public void Text_And_HtmlLocalized_ShouldRegisterTextsWithCorrectIndexes()
    {
        var builder = new GumpBuilder()
                      .Text(25, 40, 1152, "Title")
                      .HtmlLocalized(30, 70, 200, 100, "<BASEFONT COLOR=#FFFFFF>Body</BASEFONT>", true, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    builder.BuildLayout(),
                    Is.EqualTo("{ text 25 40 1152 0 } { htmlgump 30 70 200 100 1 1 1 }")
                );
                Assert.That(builder.BuildTexts(), Is.EqualTo(new[] { "Title", "<BASEFONT COLOR=#FFFFFF>Body</BASEFONT>" }));
            }
        );
    }

    [Test]
    public void ToCompressedPacket_ShouldMapBuilderState()
    {
        var packet = new GumpBuilder()
                     .NoMove()
                     .NoClose()
                     .HtmlLocalized(30, 70, 200, 100, "Body")
                     .ToCompressedPacket(0x00000012, 0x00005678, 25, 35);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet, Is.TypeOf<CompressedGumpPacket>());
                Assert.That(packet.SenderSerial, Is.EqualTo(0x00000012u));
                Assert.That(packet.GumpId, Is.EqualTo(0x00005678u));
                Assert.That(packet.X, Is.EqualTo(25u));
                Assert.That(packet.Y, Is.EqualTo(35u));
                Assert.That(packet.Layout, Is.EqualTo("{ nomove } { noclose } { htmlgump 30 70 200 100 0 1 0 }"));
                Assert.That(packet.TextLines, Is.EqualTo(new[] { "Body" }));
            }
        );
    }

    [Test]
    public void ToGenericPacket_ShouldMapBuilderState()
    {
        var packet = new GumpBuilder()
                     .ResizePic(10, 20, 5054, 250, 180)
                     .Text(25, 40, 1152, "Title")
                     .Button(50, 80, 4005, 4007, 1)
                     .ToGenericPacket(0x00000011, 0x00001234, 120, 80);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet, Is.TypeOf<GenericGumpPacket>());
                Assert.That(packet.SenderSerial, Is.EqualTo(0x00000011u));
                Assert.That(packet.GumpId, Is.EqualTo(0x00001234u));
                Assert.That(packet.X, Is.EqualTo(120u));
                Assert.That(packet.Y, Is.EqualTo(80u));
                Assert.That(packet.Layout, Does.Contain("{ resizepic 10 20 5054 250 180 }"));
                Assert.That(packet.TextLines, Is.EqualTo(new[] { "Title" }));
            }
        );
    }
}
