using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.UO.Data.Contexts;

public class AiContext : IDisposable
{
    /// <summary>
    /// Represents a scheduled action that will be executed after a delay
    /// </summary>
    public class AiScheduledAction
    {
        public string Id { get; set; }
        public DateTime ExecuteAt { get; set; }
        public Action<AiContext> Action { get; set; }
        public bool IsRepeating { get; set; }
        public int RepeatIntervalMs { get; set; }
        public string Description { get; set; }

        public AiScheduledAction(string id, DateTime executeAt, Action<AiContext> action, string description = "")
        {
            Id = id;
            ExecuteAt = executeAt;
            Action = action;
            Description = description;
            IsRepeating = false;
        }
    }

    /// <summary>
    /// Represents a queued action to be executed in sequence
    /// </summary>
    public class AiQueuedAction
    {
        public string Id { get; set; }
        public Action<AiContext> Action { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }

        /// Higher priority = executed first
        public AiQueuedAction(string id, Action<AiContext> action, int priority = 0, string description = "")
        {
            Id = id;
            Action = action;
            Priority = priority;
            Description = description;
        }
    }

    #region Core Properties

    public UOMobileEntity Self { get; set; }

    private double _elapsedTime = 0.0;
    private AiStateMachine _stateMachine;
    private bool _stateMachineInitialized = false;
    private readonly Dictionary<string, object> _internalData = new();

    private static readonly DirectionType[] directions =
    [
        DirectionType.North, DirectionType.SouthWest, DirectionType.East, DirectionType.NorthEast,
        DirectionType.South, DirectionType.West
    ];

    #endregion

    #region Initialization

    public void InitializeContext(UOMobileEntity mobile)
    {
        Self = mobile;
        InitializeStateMachine();
        InitializeActionSystem();
    }

    /// <summary>
    /// Execute action in background without blocking the main thread
    /// </summary>
    public void RunBackground(Action<AiContext> action, string description = "")
    {
        Task.Run(() =>
        {
            try
            {
                action(this);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in background action for {Mobile}: {Description}",
                    Self?.Name, description);
            }
        });
    }

    /// <summary>
    /// Initialize state machine for this mobile - called once per mobile
    /// </summary>
    private void InitializeStateMachine()
    {
        if (_stateMachineInitialized)
        {
            return;
        }

        var stateMachineId = $"ai_sm_{Self.Id}_{Self.BrainId}";
        _stateMachine = new AiStateMachine(stateMachineId, "idle");
        _stateMachineInitialized = true;

        /// Add default state change logging
        _stateMachine.AddStateChangeListener((from, to, eventName) =>
            {
                Log.ForContext<AiContext>()
                    .Debug(
                        "AI {Mobile}: {From} -> {To} ({Event})",
                        Self.Name,
                        from,
                        to,
                        eventName
                    );
            }
        );
    }

    /// <summary>
    /// Initialize action system
    /// </summary>
    private void InitializeActionSystem()
    {
        SetData("scheduled_actions", new List<AiScheduledAction>());
        SetData("action_queue", new List<AiQueuedAction>());
    }

    #endregion

    #region Main Update

    /// <summary>
    /// Main AI update method - call this every 500ms tick
    /// </summary>
    public void UpdateAI()
    {
        IncrementElapsedTime(0.5); /// 500ms = 0.5 seconds
        ProcessActions();
    }

    #endregion

    #region Time Management

    public void IncrementElapsedTime(double deltaTime)
    {
        _elapsedTime += deltaTime;
    }

    public double GetElapsedTime()
    {
        return _elapsedTime;
    }

    public void ResetElapsedTime()
    {
        _elapsedTime = 0.0;
    }

    #endregion

    #region Movement

    public void Move(DirectionType direction)
    {
        if (Self == null)
        {
            throw new InvalidOperationException("MobileEntity is not initialized.");
        }

        var newLocation = Self.Location + direction;
        var landTile = Self.Map.GetLandTile(newLocation.X, newLocation.Y);
        newLocation = new Point3D(newLocation.X, newLocation.Y, landTile.Z);

        MoongateContext.Container.Resolve<IMobileService>().MoveMobile(Self, newLocation);
    }

    public DirectionType RandomDirection()
    {
        return directions[Random.Shared.Next(directions.Length)];
    }

    #endregion

    #region Internal Data Management

    /// <summary>
    /// Get internal data with default value
    /// </summary>
    public T GetData<T>(string key, T defaultValue = default)
    {
        return _internalData.TryGetValue(key, out var value) && value is T tValue ? tValue : defaultValue;
    }

    /// <summary>
    /// Set internal data
    /// </summary>
    public void SetData(string key, object value)
    {
        _internalData[key] = value;
    }

    /// <summary>
    /// Check if data exists
    /// </summary>
    public bool HasData(string key)
    {
        return _internalData.ContainsKey(key);
    }

    /// <summary>
    /// Remove data
    /// </summary>
    public void RemoveData(string key)
    {
        _internalData.Remove(key);
    }

    #endregion

    #region Action System

    /// <summary>
    /// Schedule an action to be executed after a delay
    /// </summary>
    public void ScheduleAction(string actionId, int delayMs, Action<AiContext> action, string description = "")
    {
        var executeAt = DateTime.UtcNow.AddMilliseconds(delayMs);
        var scheduledAction = new AiScheduledAction(actionId, executeAt, action, description);

        var scheduledActions = GetData("scheduled_actions", new List<AiScheduledAction>());

        /// Remove existing action with same ID if any
        scheduledActions.RemoveAll(a => a.Id == actionId);
        scheduledActions.Add(scheduledAction);

        SetData("scheduled_actions", scheduledActions);

        Log.ForContext<AiContext>()
            .Debug(
                "Scheduled action {ActionId} for {Mobile} in {DelayMs}ms",
                actionId,
                Self.Name,
                delayMs
            );
    }

    /// <summary>
    /// Schedule a repeating action
    /// </summary>
    public void ScheduleRepeatingAction(string actionId, int intervalMs, Action<AiContext> action, string description = "")
    {
        var executeAt = DateTime.UtcNow.AddMilliseconds(intervalMs);
        var scheduledAction = new AiScheduledAction(actionId, executeAt, action, description)
        {
            IsRepeating = true,
            RepeatIntervalMs = intervalMs
        };

        var scheduledActions = GetData("scheduled_actions", new List<AiScheduledAction>());
        scheduledActions.RemoveAll(a => a.Id == actionId);
        scheduledActions.Add(scheduledAction);

        SetData("scheduled_actions", scheduledActions);

        Log.ForContext<AiContext>()
            .Debug(
                "Scheduled repeating action {ActionId} for {Mobile} every {IntervalMs}ms",
                actionId,
                Self.Name,
                intervalMs
            );
    }

    /// <summary>
    /// Cancel a scheduled action
    /// </summary>
    public bool CancelScheduledAction(string actionId)
    {
        var scheduledActions = GetData("scheduled_actions", new List<AiScheduledAction>());
        var removed = scheduledActions.RemoveAll(a => a.Id == actionId);
        SetData("scheduled_actions", scheduledActions);

        if (removed > 0)
        {
            Log.ForContext<AiContext>()
                .Debug(
                    "Cancelled scheduled action {ActionId} for {Mobile}",
                    actionId,
                    Self.Name
                );
        }

        return removed > 0;
    }

    /// <summary>
    /// Add action to queue
    /// </summary>
    public void QueueAction(string actionId, Action<AiContext> action, int priority = 0, string description = "")
    {
        var queuedAction = new AiQueuedAction(actionId, action, priority, description);
        var actionQueue = GetData("action_queue", new List<AiQueuedAction>());

        /// Remove existing action with same ID if any
        actionQueue.RemoveAll(a => a.Id == actionId);
        actionQueue.Add(queuedAction);

        /// Sort by priority (higher first)
        actionQueue.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        SetData("action_queue", actionQueue);

        Log.ForContext<AiContext>()
            .Debug(
                "Queued action {ActionId} for {Mobile} with priority {Priority}",
                actionId,
                Self.Name,
                priority
            );
    }

    /// <summary>
    /// Execute the next action in queue
    /// </summary>
    public bool ExecuteNextQueuedAction()
    {
        var actionQueue = GetData("action_queue", new List<AiQueuedAction>());

        if (actionQueue.Count == 0)
            return false;

        var nextAction = actionQueue[0];
        actionQueue.RemoveAt(0);
        SetData("action_queue", actionQueue);

        try
        {
            Log.ForContext<AiContext>()
                .Debug(
                    "Executing queued action {ActionId} for {Mobile}",
                    nextAction.Id,
                    Self.Name
                );
            nextAction.Action(this);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Error executing queued action {ActionId} for {Mobile}",
                nextAction.Id,
                Self?.Name
            );
            return false;
        }
    }

    /// <summary>
    /// Clear all queued actions
    /// </summary>
    public void ClearActionQueue()
    {
        SetData("action_queue", new List<AiQueuedAction>());
        Log.ForContext<AiContext>().Debug("Cleared action queue for {Mobile}", Self.Name);
    }

    /// <summary>
    /// Get count of queued actions
    /// </summary>
    public int GetQueuedActionCount()
    {
        var actionQueue = GetData("action_queue", new List<AiQueuedAction>());
        return actionQueue.Count;
    }

    /// <summary>
    /// Process all scheduled and queued actions - called every AI tick
    /// </summary>
    public void ProcessActions()
    {
        ProcessScheduledActions();

        /// Execute one queued action per tick to avoid blocking
        if (GetQueuedActionCount() > 0)
        {
            ExecuteNextQueuedAction();
        }
    }

    /// <summary>
    /// Process scheduled actions
    /// </summary>
    private void ProcessScheduledActions()
    {
        var scheduledActions = GetData("scheduled_actions", new List<AiScheduledAction>());
        var now = DateTime.UtcNow;
        var actionsToExecute = new List<AiScheduledAction>();
        var actionsToKeep = new List<AiScheduledAction>();

        foreach (var action in scheduledActions)
        {
            if (now >= action.ExecuteAt)
            {
                actionsToExecute.Add(action);

                /// If repeating, reschedule it
                if (action.IsRepeating)
                {
                    action.ExecuteAt = now.AddMilliseconds(action.RepeatIntervalMs);
                    actionsToKeep.Add(action);
                }
            }
            else
            {
                actionsToKeep.Add(action);
            }
        }

        /// Update the scheduled actions list
        SetData("scheduled_actions", actionsToKeep);

        /// Execute the actions
        foreach (var action in actionsToExecute)
        {
            try
            {
                Log.ForContext<AiContext>()
                    .Debug(
                        "Executing scheduled action {ActionId} for {Mobile}",
                        action.Id,
                        Self.Name
                    );
                action.Action(this);
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Error executing scheduled action {ActionId} for {Mobile}",
                    action.Id,
                    Self?.Name
                );
            }
        }
    }

    /// <summary>
    /// Helper method to schedule state transition after delay
    /// </summary>
    public void ScheduleStateTransition(string eventName, int delayMs)
    {
        ScheduleAction(
            $"transition_{eventName}",
            delayMs,
            ctx => ctx.Transition(eventName),
            $"Transition to {eventName}"
        );
    }

    /// <summary>
    /// Helper method to schedule movement after delay
    /// </summary>
    public void ScheduleMovement(DirectionType direction, int delayMs)
    {
        ScheduleAction(
            $"move_{direction}",
            delayMs,
            ctx => ctx.Move(direction),
            $"Move {direction}"
        );
    }

    /// <summary>
    /// Helper method for JavaScript - schedule action with simple callback
    /// </summary>
    public void ScheduleSimpleAction(string actionId, int delayMs, string eventToTrigger)
    {
        ScheduleAction(
            actionId,
            delayMs,
            ctx => ctx.Transition(eventToTrigger),
            $"Simple action: {eventToTrigger}"
        );
    }

    /// <summary>
    /// Helper method for JavaScript - queue simple action
    /// </summary>
    public void QueueSimpleAction(string actionId, string eventToTrigger, int priority = 0)
    {
        QueueAction(
            actionId,
            ctx => ctx.Transition(eventToTrigger),
            priority,
            $"Simple queued action: {eventToTrigger}"
        );
    }

    #endregion

    #region State Machine API

    /// <summary>
    /// Get current AI state
    /// </summary>
    public string CurrentState => _stateMachine?.CurrentState ?? "idle";

    /// <summary>
    /// Trigger state transition
    /// </summary>
    public bool Transition(string eventName)
    {
        return _stateMachine?.Transition(eventName) ?? false;
    }

    /// <summary>
    /// Check if transition is valid
    /// </summary>
    public bool CanTransition(string eventName)
    {
        return _stateMachine?.CanTransition(eventName) ?? false;
    }

    /// <summary>
    /// Add state transition rule
    /// </summary>
    public void AddTransition(string from, string onEvent, string to)
    {
        _stateMachine?.AddTransition(from, onEvent, to);
    }

    /// <summary>
    /// Add conditional transition with predicate
    /// </summary>
    public void AddConditionalTransition(string from, string onEvent, string to, Func<bool> condition)
    {
        _stateMachine?.AddConditionalTransition(from, onEvent, to, condition);
    }

    /// <summary>
    /// Add callback for state changes
    /// </summary>
    public void OnStateChange(Action<string, string, string> callback)
    {
        _stateMachine?.AddStateChangeListener(callback);
    }

    /// <summary>
    /// Reset to specific state
    /// </summary>
    public void ResetToState(string newState)
    {
        _stateMachine?.Reset(newState);
        _internalData.Clear();    /// Clear all internal data on reset
        InitializeActionSystem(); /// Reinitialize action system
    }

    #endregion

    #region Timeout Management

    /// <summary>
    /// Start a timeout for current state
    /// </summary>
    public void StartTimeout(string timeoutKey, int milliseconds)
    {
        SetData(timeoutKey, DateTime.UtcNow.AddMilliseconds(milliseconds));
    }

    /// <summary>
    /// Check if timeout has expired
    /// </summary>
    public bool IsTimeoutExpired(string timeoutKey)
    {
        var timeout = GetData<DateTime?>(timeoutKey);
        return timeout.HasValue && DateTime.UtcNow > timeout.Value;
    }

    /// <summary>
    /// Clear timeout
    /// </summary>
    public void ClearTimeout(string timeoutKey)
    {
        RemoveData(timeoutKey);
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Find nearby players within range - placeholder implementation
    /// </summary>
    public List<UOMobileEntity> FindNearbyPlayers(int range)
    {
        /// Placeholder - in real implementation this would use Moongate's spatial queries
        /// Return empty list for now - implement based on Moongate's world system
        return new List<UOMobileEntity>();
    }

    /// <summary>
    /// Check if this mobile is hostile towards target
    /// </summary>
    public bool IsHostileTowards(UOMobileEntity target)
    {
        /// Placeholder - implement based on Moongate's faction/criminal system
        return false;
    }

    /// <summary>
    /// Get distance to target location
    /// </summary>
    public double GetDistance(Point3D target)
    {
        var dx = Self.Location.X - target.X;
        var dy = Self.Location.Y - target.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        /// Clean up any resources if needed
        CancelScheduledAction("*"); /// Cancel all scheduled actions
        ClearActionQueue();
    }

    #endregion
}
