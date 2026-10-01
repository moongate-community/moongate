using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     The commands of a gump in order; <see cref="Build" /> writes them with their string table.
/// </summary>
public sealed class GumpLayout
{
    private readonly List<GumpEntry> _entries = [];

    public IReadOnlyList<GumpEntry> Entries => _entries;

    public GumpLayout Add(GumpEntry entry)
    {
        _entries.Add(entry);

        return this;
    }

    public GumpBuildResult Build()
    {
        var layout = new StringBuilder();
        var strings = new GumpStrings();
        var buttons = new HashSet<int>();
        var switches = new HashSet<int>();
        var entries = new HashSet<int>();

        foreach (var entry in _entries)
        {
            entry.Write(layout, strings);

            switch (entry)
            {
                case GumpButton { Page: 0 } button:
                    buttons.Add(button.ButtonId);

                    break;
                case GumpCheckbox checkbox:
                    switches.Add(checkbox.SwitchId);

                    break;
                case GumpRadio radio:
                    switches.Add(radio.SwitchId);

                    break;
                case GumpTextEntry textEntry:
                    entries.Add(textEntry.EntryId);

                    break;
            }
        }

        return new()
        {
            Layout = layout.ToString(), Strings = strings.Strings.ToList(), Buttons = buttons, Switches = switches,
            TextEntries = entries
        };
    }
}
