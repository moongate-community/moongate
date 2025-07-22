using Serilog;

namespace Moongate.UO.Data.Contexts;

/// <summary>
/// Ultra-fast state machine optimized for AI contexts
/// Simplified version integrated directly with AiContext
/// </summary>
public class AiStateMachine : IDisposable
{
    private readonly Dictionary<string, Dictionary<string, string>> _transitions;
    private readonly Dictionary<string, Dictionary<string, Func<bool>>> _conditionalTransitions;
    private readonly List<Action<string, string, string>> _callbacks;

    public string CurrentState { get; private set; }

    public string MachineId { get; }

    public AiStateMachine(string machineId, string initialState)
    {
        MachineId = machineId;
        CurrentState = initialState;
        _transitions = new Dictionary<string, Dictionary<string, string>>();
        _conditionalTransitions = new Dictionary<string, Dictionary<string, Func<bool>>>();
        _callbacks = new List<Action<string, string, string>>();
    }

    public void AddTransition(string from, string onEvent, string to)
    {
        if (!_transitions.ContainsKey(from))
        {
            _transitions[from] = new Dictionary<string, string>();
        }

        _transitions[from][onEvent] = to;
    }

    public void Reset(string newState)
    {
        CurrentState = newState;
    }

    public void Reset()
    {
        Reset("idle");
    }

    public void AddConditionalTransition(string from, string onEvent, string to, Func<bool> condition)
    {
        AddTransition(from, onEvent, to);

        if (!_conditionalTransitions.ContainsKey(from))
        {
            _conditionalTransitions[from] = new Dictionary<string, Func<bool>>();
        }

        _conditionalTransitions[from][onEvent] = condition;
    }

    public bool Transition(string eventTrigger)
    {
        if (!_transitions.TryGetValue(CurrentState, out var stateTransitions) ||
            !stateTransitions.TryGetValue(eventTrigger, out var nextState))
        {
            return false;
        }

        /// Check conditional transition if exists
        if (_conditionalTransitions.TryGetValue(CurrentState, out var conditionalTransitions) &&
            conditionalTransitions.TryGetValue(eventTrigger, out var condition))
        {
            try
            {
                if (!condition())
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<AiStateMachine>().Warning(ex, "Conditional transition error");
                return false;
            }
        }

        var previousState = CurrentState;
        CurrentState = nextState;

        /// Notify callbacks
        for (int i = 0; i < _callbacks.Count; i++)
        {
            try
            {
                _callbacks[i](previousState, nextState, eventTrigger);
            }
            catch (Exception ex)
            {
                Log.ForContext<AiStateMachine>().Warning(ex, "State change callback error");
            }
        }

        return true;
    }

    public bool CanTransition(string eventTrigger)
    {
        return _transitions.TryGetValue(CurrentState, out var stateTransitions) &&
               stateTransitions.ContainsKey(eventTrigger);
    }

    public void AddStateChangeListener(Action<string, string, string> callback)
    {
        _callbacks.Add(callback);
    }

    public void Dispose()
    {
        _callbacks.Clear();
        _conditionalTransitions.Clear();
        _transitions.Clear();
    }
}
