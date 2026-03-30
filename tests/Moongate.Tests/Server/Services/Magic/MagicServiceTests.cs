using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class MagicServiceTests
{
    private RecordingTimerService _timerService = null!;
    private RecordingGameEventBusService _gameEventBusService = null!;
    private FakeCharacterService _characterService = null!;
    private SpellRegistry _spellRegistry = null!;
    private MagicService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _timerService = new RecordingTimerService();
        _gameEventBusService = new RecordingGameEventBusService();
        _characterService = new FakeCharacterService();
        _spellRegistry = new SpellRegistry();
        _service = new MagicService(_timerService, _gameEventBusService, _characterService, _spellRegistry);
    }

    [Test]
    public async Task TryCastAsync_DeadCaster_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: false, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_UnregisteredSpell_ReturnsFalse()
    {
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_InsufficientMana_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 10, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 9);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_MissingRequiredReagents_ReturnsFalse()
    {
        _spellRegistry.Register(
            new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [ReagentType.Garlic], [1]))
        );
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
        Assert.That(_characterService.GetBackpackWithItemsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task TryCastAsync_ValidSpell_StartsCastingAndRegistersTimer()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1.5), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.True);
        Assert.That(_service.IsCasting(caster.Id), Is.True);
        Assert.That(_timerService.RegisteredTimers, Has.Count.EqualTo(1));
        Assert.That(_timerService.RegisteredTimers[0].Name, Is.EqualTo($"spell_cast_{caster.Id}_{StubSpellId}"));
        Assert.That(_timerService.RegisteredTimers[0].Interval, Is.EqualTo(TimeSpan.FromSeconds(1.5)));
    }

    [Test]
    public async Task TryCastAsync_WhileAlreadyCasting_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_timerService.RegisteredTimers, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Interrupt_ActiveCast_ClearsCastStateAndUnregistersTimer()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        _service.Interrupt(caster.Id);

        Assert.That(_service.IsCasting(caster.Id), Is.False);
        Assert.That(_timerService.UnregisteredTimerIds, Has.Count.EqualTo(1));
        Assert.That(_timerService.UnregisteredTimerIds[0], Is.EqualTo($"spell_cast_{caster.Id}_{StubSpellId}"));
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_ActiveCast_ClearsCastState()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    private static UOMobileEntity CreateCaster(bool isAlive, int mana)
    {
        return new UOMobileEntity
        {
            Id = new Serial(1),
            IsAlive = isAlive,
            Mana = mana
        };
    }

    private sealed class RecordingTimerService : ITimerService
    {
        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public List<string> UnregisteredTimerIds { get; } = [];

        public void ProcessTick()
        {
        }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(callback);

            RegisteredTimers.Add(new(name, interval, callback, delay, repeat));

            return name;
        }

        public void UnregisterAllTimers()
        {
            RegisteredTimers.Clear();
        }

        public bool UnregisterTimer(string timerId)
        {
            UnregisteredTimerIds.Add(timerId);

            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            var removed = RegisteredTimers.RemoveAll(timer => timer.Name == name);

            return removed;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    private sealed record RegisteredTimer(
        string Name,
        TimeSpan Interval,
        Action Callback,
        TimeSpan? Delay,
        bool Repeat
    );

    private sealed class RecordingGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = gameEvent;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
        {
            _ = listener;
        }
    }

    private sealed class FakeCharacterService : ICharacterService
    {
        public int GetBackpackWithItemsCalls { get; private set; }

        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            throw new NotSupportedException();
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            ArgumentNullException.ThrowIfNull(character);
            GetBackpackWithItemsCalls++;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubSpell : MagerySpellBase
    {
        private readonly int _manaCost;
        private readonly TimeSpan _castDelay;

        public StubSpell(int spellId, int manaCost, TimeSpan castDelay, SpellInfo info)
        {
            SpellId = spellId;
            _manaCost = manaCost;
            _castDelay = castDelay;
            Info = info;
        }

        public override int SpellId { get; }

        public override SpellCircleType Circle => SpellCircleType.First;

        public override SpellInfo Info { get; }

        public override int ManaCost => _manaCost;

        public override TimeSpan CastDelay => _castDelay;

        public override double MinSkill => 0;

        public override double MaxSkill => 100;

        public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            _ = caster;
            _ = target;
        }
    }

    private const int StubSpellId = 4;
}
