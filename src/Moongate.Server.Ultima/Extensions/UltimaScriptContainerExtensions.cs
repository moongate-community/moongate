using DryIoc;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers what the Ultima world publishes to Lua: its modules and its enums.
/// </summary>
public static class UltimaScriptContainerExtensions
{
    /// <summary>
    ///     Registers the Lua modules and enums of the Ultima world. It needs nothing else in the container, so the
    ///     documentation reads the same list without starting a server.
    /// </summary>
    /// <param name="container">
    ///     The container the modules are registered in.
    /// </param>
    /// <returns>
    ///     The same container, for chaining.
    /// </returns>
    public static Container AddUltimaScriptModules(this Container container)
    {
        container.AddScriptModule<DiceModule>();
        container.AddScriptModule<LocalizationModule>();
        container.AddScriptModule<NpcModule>();
        container.AddScriptModule<ItemModule>();
        container.AddScriptModule<WorldModule>();
        container.AddScriptModule<MobileModule>();
        container.AddScriptModule<TargetModule>();
        container.AddScriptModule<HuePickerModule>();
        container.AddScriptModule<SkillModule>();
        container.AddScriptModule<CombatModule>();
        container.AddScriptModule<PromptModule>();
        container.AddScriptModule<GumpModule>();
        container.AddScriptModule<BankModule>();
        container.AddScriptModule<EffectModule>();
        container.AddScriptModule<MoongatesModule>();
        container.AddScriptModule<LocationsModule>();
        container.AddScriptModule<JailModule>();
        container.AddScriptModule<BookModule>();
        container.AddScriptModule<CommandsModule>();
        container.AddScriptModule<BoardModule>();
        // No module function takes them: registered so on_speech can compare its keywords and its type with names.
        container.RegisterScriptEnum<SpeechKeywordType>();
        container.RegisterScriptEnum<SpeechType>();
        container.RegisterScriptEnum<DirectionType>();
        container.RegisterScriptEnum<JailResultType>();
        container.RegisterScriptEnum<BankResultType>();
        container.RegisterScriptEnum<SkillType>();
        container.RegisterScriptEnum<SeasonType>();
        container.RegisterScriptEnum<WeatherKindType>();
        container.RegisterScriptEnum<EffectGraphicType>();
        container.RegisterScriptEnum<EffectRenderModeType>();
        container.RegisterScriptEnum<EffectLayerType>();
        container.RegisterScriptEnum<BodyType>();
        container.RegisterScriptEnum<HumanAnimationType>();
        container.RegisterScriptEnum<MonsterAnimationType>();
        container.RegisterScriptEnum<AnimalAnimationType>();

        return container;
    }
}
