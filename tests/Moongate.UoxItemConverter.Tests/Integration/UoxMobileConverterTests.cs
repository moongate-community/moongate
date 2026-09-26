using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Names;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class UoxMobileConverterTests : IDisposable
{
    private readonly ConverterTestDirectories _dirs = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string CombinedOutput => _output + _error.ToString();

    public UoxMobileConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public void Run_TheNameLists_AreWrittenWithReadableIdsAndDictionaryNames()
    {
        _dirs.WriteSource("items.dfn", "[0x0eed]\n{\nid=0x0eed\n}\n");
        _dirs.WriteMobileSource(
            "npc/namelists.dfn",
            "[RANDOMNAME 1]\n{ Basic Human Male Names\nAaron\nAbbott\nAaron\n}\n[RANDOMNAME 5]\n{\n3009//a daemon\nImp\n}\n"
        );
        _dirs.WriteMobileSource("../dictionaries/dictionary.ENG", "3009=a daemon\n");

        Assert.True(Run() == 0, CombinedOutput);

        var names = TomlUtils.DeserializeFromFile<NameListFile>(_dirs.NamesDestinationPath)!.Names;
        Assert.Equal(["Aaron", "Abbott"], names.Single(list => list.Id == "male").Names);
        Assert.Equal(["a daemon", "Imp"], names.Single(list => list.Id == "daemon").Names);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _dirs.Dispose();
    }

    private int Run()
    {
        return UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            _dirs.LootDestinationDirectory,
            _output,
            _error,
            _dirs.MobileSourceDirectory,
            _dirs.MobileDestinationDirectory,
            _dirs.NamesDestinationPath
        );
    }
}
