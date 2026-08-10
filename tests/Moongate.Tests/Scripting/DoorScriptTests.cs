using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Scripting.Items;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Types.Items;
using MoonSharp.Interpreter;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Scripting;

/// <summary>
/// The shipped door script, exercised as the server runs it.
/// <para>
/// It runs the real <c>door.lua</c> read off disk rather than a copy written here: a door's arithmetic
/// is the whole feature, and a test carrying its own version of it would keep passing while the file
/// the shard actually loads drifted away.
/// </para>
/// <para>
/// <c>item</c> and <c>game</c> are recording tables rather than the real modules, so what is under
/// test is the door's reasoning — which graphic, which direction, how far — and not the item service.
/// </para>
/// </summary>
public class DoorScriptTests
{
    private const int MetalDoorBase = 0x675;

    /// <summary>Facing, and the tile a door of that facing swings to. From ModernUO's BaseDoor.</summary>
    public static TheoryData<int, int, int> Facings()
        => new()
        {
            { 0, -1, 1 },
            { 1, 1, 1 },
            { 2, -1, 0 },
            { 3, 1, -1 },
            { 4, 1, 1 },
            { 5, 1, -1 },
            { 6, 0, 0 },
            { 7, 0, -1 }
        };

    [Fact]
    public void DoubleClick_AClosedDoor_OpensAndSwings()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 2, 100, 100);   // facing 1, closed

        Assert.Equal(MetalDoorBase + 3, fixture.SetItemId);
        Assert.Equal((101, 101), fixture.MovedTo);
    }

    [Fact]
    public void DoubleClick_AnOpenDoor_ClosesAndSwingsBack()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 3, 101, 101);   // facing 1, open

        Assert.Equal(MetalDoorBase + 2, fixture.SetItemId);
        Assert.Equal((100, 100), fixture.MovedTo);
    }

    // Every facing, so a wrong sign in the offset table cannot hide in the one case that was tried.
    [Theory, MemberData(nameof(Facings))]
    public void EachFacing_SwingsByItsOwnOffset(int facing, int dx, int dy)
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + (2 * facing), 100, 100);

        Assert.Equal((100 + dx, 100 + dy), fixture.MovedTo);
    }

    // A closed door re-opens to the same place it opened to last time, or doors drift across the map.
    [Fact]
    public void OpeningAndClosing_ReturnsADoorToWhereItStood()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 4, 100, 100);   // facing 2, closed
        var opened = Assert.NotNull(fixture.MovedTo);

        fixture.DoubleClick(fixture.SetItemId, opened.X, opened.Y);

        Assert.Equal((100, 100), fixture.MovedTo);
    }

    // A door whose template this script does not know is left alone rather than guessed at.
    [Fact]
    public void DoubleClick_AnUnknownTemplate_DoesNothing()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 2, 100, 100, templateId: "world_decoration");

        Assert.Null(fixture.MovedTo);
        Assert.Equal(0, fixture.SetItemId);
    }

    // The auto-close is what stops a town ending the night with every door standing open.
    [Fact]
    public void OpeningADoor_SchedulesItsOwnClose()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 2, 100, 100);

        Assert.True(fixture.ScheduledAfterMs > 0);
    }

    [Fact]
    public void ClosingADoor_SchedulesNothing()
    {
        using var fixture = new Fixture();

        fixture.DoubleClick(MetalDoorBase + 3, 101, 101);

        Assert.Equal(0, fixture.ScheduledAfterMs);
    }

    private sealed class Fixture : IDisposable
    {
        private const string DoorScript = "../../../../../src/Moongate.Scripting/Assets/Items/door.lua";

        private readonly string _root;
        private readonly Script _script;
        private readonly LuaItemScriptRuntime _runtime;

        public Fixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "mg-door-" + Guid.NewGuid().ToString("N"));
            var itemsDirectory = Path.Combine(_root, "scripts", "items");
            Directory.CreateDirectory(itemsDirectory);

            // The shipped script, not a copy of it.
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, DoorScript),
                Path.Combine(itemsDirectory, "door.lua")
            );

            _script = new();
            _runtime = new(_script, new(_root, ["scripts"]));

            InstallRecordingModules();
        }

        /// <summary>The graphic the script last asked for, or 0.</summary>
        public int SetItemId { get; private set; }

        /// <summary>Where the script last moved the door, or null.</summary>
        public (int X, int Y)? MovedTo { get; private set; }

        /// <summary>The delay of the last close the script scheduled, or 0.</summary>
        public int ScheduledAfterMs { get; private set; }

        public void DoubleClick(int itemId, int x, int y, string templateId = "metal_door")
        {
            SetItemId = 0;
            MovedTo = null;
            ScheduledAfterMs = 0;

            var item = new ItemEntity
            {
                Id = (Serial)1u,
                TemplateId = templateId,
                ScriptId = "items.door",
                ItemId = itemId,
                Name = "",
                Position = new(x, y, 0)
            };

            _runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null));
        }

        public void Dispose()
        {
            // Not in a finally that could fail the verdict: a leftover temp directory is untidy, not
            // a failed test.
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        private void InstallRecordingModules()
        {
            var item = new Table(_script)
            {
                ["set"] = (Func<double, Table, bool>)((_, fields) =>
                {
                    SetItemId = (int)fields.Get("item_id").Number;

                    return true;
                }),
                ["move"] = (Func<double, double, double, double, bool>)((_, x, y, _) =>
                {
                    MovedTo = ((int)x, (int)y);

                    return true;
                })
            };

            var game = new Table(_script)
            {
                ["schedule"] = (Func<double, DynValue, string>)((delayMs, _) =>
                {
                    ScheduledAfterMs = (int)delayMs;

                    return "timer";
                })
            };

            _script.Globals["item"] = item;
            _script.Globals["game"] = game;
        }
    }
}
