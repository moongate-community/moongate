using Serilog;

namespace Moongate.UO.Data.Ai;

/// <summary>
/// Ultra-fast state machine optimized for Moongate integration
/// Uses Action/Func callbacks for maximum performance
/// </summary>
public class DefaultAiStateMachine
{
    private readonly Dictionary<string, Dictionary<string, string>> _transitions;
    private readonly Dictionary<string, Dictionary<string, Func<bool>>> _conditionalTransitions;
    private readonly List<Action<string, string, string>> _callbacks;
    private readonly Queue<StateTransition> _history;
    private readonly int _maxHistory;

    public string CurrentState { get; private set; }

    public DefaultAiStateMachine(string initialState, int maxHistory = 50)
    {
        CurrentState = initialState;
        _transitions = new Dictionary<string, Dictionary<string, string>>();
        _conditionalTransitions = new Dictionary<string, Dictionary<string, Func<bool>>>();
        _callbacks = new List<Action<string, string, string>>();
        _history = new Queue<StateTransition>(maxHistory);
        _maxHistory = maxHistory;
    }

    /// <summary>
    /// Add transition rule - O(1) performance
    /// </summary>
    public void AddTransition(string from, string onEvent, string to)
    {
        if (!_transitions.ContainsKey(from))
        {
            _transitions[from] = new Dictionary<string, string>();
        }

        _transitions[from][onEvent] = to;
    }

    /// <summary>
    /// Add conditional transition with predicate - for advanced logic
    /// </summary>
    public void AddConditionalTransition(string from, string onEvent, string to, Func<bool> condition)
    {
        /// Add normal transition first
        AddTransition(from, onEvent, to);

        /// Store condition
        if (!_conditionalTransitions.ContainsKey(from))
        {
            _conditionalTransitions[from] = new Dictionary<string, Func<bool>>();
        }

        _conditionalTransitions[from][onEvent] = condition;
    }

    /// <summary>
    /// Execute state transition - ultra fast O(1) lookup with conditional support
    /// </summary>
    public bool Transition(string eventTrigger)
    {
        if (!_transitions.TryGetValue(CurrentState, out var stateTransitions) ||
            !stateTransitions.TryGetValue(eventTrigger, out var nextState))
        {
            return false; /// Invalid transition
        }

        /// Check conditional transition if exists
        if (_conditionalTransitions.TryGetValue(CurrentState, out var conditionalTransitions) &&
            conditionalTransitions.TryGetValue(eventTrigger, out var condition))
        {
            try
            {
                if (!condition())
                {
                    return false; /// Condition failed
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<DefaultAiStateMachine>()
                    .Warning(ex, "Conditional transition predicate error");
                return false;
            }
        }

        var previousState = CurrentState;
        CurrentState = nextState;

        /// Efficient history management
        if (_history.Count >= _maxHistory)
        {
            _history.Dequeue();
        }

        _history.Enqueue(new StateTransition(previousState, nextState, eventTrigger, DateTime.UtcNow));

        /// Notify callbacks efficiently
        NotifyCallbacks(previousState, nextState, eventTrigger);

        return true;
    }

    /// <summary>
    /// Check if transition is valid without executing
    /// </summary>
    public bool CanTransition(string eventTrigger)
    {
        return _transitions.TryGetValue(CurrentState, out var stateTransitions) &&
               stateTransitions.ContainsKey(eventTrigger);
    }

    /// <summary>
    /// Get available events from current state
    /// </summary>
    public IEnumerable<string> GetAvailableEvents()
    {
        if (_transitions.TryGetValue(CurrentState, out var stateTransitions))
        {
            return stateTransitions.Keys;
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Add callback for state changes - ultra fast Action delegate
    /// </summary>
    public void AddStateChangeListener(Action<string, string, string> callback)
    {
        _callbacks.Add(callback);
    }

    /// <summary>
    /// Remove callback for state changes
    /// </summary>
    public bool RemoveStateChangeListener(Action<string, string, string> callback)
    {
        return _callbacks.Remove(callback);
    }

    /// <summary>
    /// Notify callbacks with maximum performance
    /// </summary>
    private void NotifyCallbacks(string from, string to, string eventTrigger)
    {
        for (int i = 0; i < _callbacks.Count; i++)
        {
            try
            {
                /// Direct Action call - no reflection, no JsValue conversion
                _callbacks[i](from, to, eventTrigger);
            }
            catch (Exception ex)
            {
                /// Log error without breaking state machine
                Log.ForContext<DefaultAiStateMachine>()
                    .Warning(ex, "State machine callback error");
            }
        }
    }

    /// <summary>
    /// Reset to specific state
    /// </summary>
    public void Reset(string newState)
    {
        CurrentState = newState;
        _history.Clear();
    }

    /// <summary>
    /// Get recent transition history
    /// </summary>
    public StateTransition[] GetHistory(int limit = 10)
    {
        var historyArray = _history.ToArray();
        var startIndex = Math.Max(0, historyArray.Length - limit);
        var result = new StateTransition[Math.Min(limit, historyArray.Length)];
        Array.Copy(historyArray, startIndex, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// State transition record for history tracking
    /// </summary>
    public readonly struct StateTransition
    {
        public readonly string FromState;
        public readonly string ToState;
        public readonly string Event;
        public readonly DateTime Timestamp;

        public StateTransition(string from, string to, string eventTrigger, DateTime timestamp)
        {
            FromState = from;
            ToState = to;
            Event = eventTrigger;
            Timestamp = timestamp;
        }
    }
}

/// <summary>
/// AI State Machine specialized for Moongate AI brains
/// Integrates with existing AI system using Action callbacks
/// </summary>
public class AIStateMachineBrain : DefaultAiStateMachine
{
    private readonly string _brainId;

    public AIStateMachineBrain(string brainId, string initialState)
        : base(initialState)
    {
        _brainId = brainId;
        SetupCommonAITransitions();
    }

    /// <summary>
    /// Setup common AI state transitions
    /// </summary>
    private void SetupCommonAITransitions()
    {
        /// Common AI states and transitions
        AddTransition("idle", "see_player", "alert");
        AddTransition("idle", "take_damage", "combat");
        AddTransition("idle", "wander", "moving");

        AddTransition("alert", "lose_target", "idle");
        AddTransition("alert", "target_hostile", "combat");
        AddTransition("alert", "timeout", "idle");

        AddTransition("moving", "see_player", "alert");
        AddTransition("moving", "destination_reached", "idle");
        AddTransition("moving", "take_damage", "combat");

        AddTransition("combat", "target_dead", "idle");
        AddTransition("combat", "target_fled", "alert");
        AddTransition("combat", "low_health", "fleeing");

        AddTransition("fleeing", "safe_distance", "idle");
        AddTransition("fleeing", "cornered", "combat");
    }

    public string BrainId => _brainId;
}
