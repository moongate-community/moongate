using Moongate.Core.Primitives;
using Moongate.Tests.Support.Serialization.Types;

namespace Moongate.Tests.Support.Serialization.Data;

public sealed class TemplateRarityHolder
{
    public EnumValueSpec<TemplateRarity> Rarity { get; set; }
}
