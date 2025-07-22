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
    protected UOMobileEntity MobileEntity;

    private AiStateMachine _stateMachine;
    private bool _stateMachineInitialized = false;
    private readonly Dictionary<string, object> _internalData = new();

    private static readonly DirectionType[] directions =
    {
        DirectionType.North, DirectionType.Left, DirectionType.East, DirectionType.Right,
        DirectionType.South, DirectionType.West
    };

    public void InitializeContext(UOMobileEntity mobile)
    {
        MobileEntity = mobile;
        InitializeStateMachine();
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

        var stateMachineId = $"ai_sm_{MobileEntity.Id}_{MobileEntity.BrainId}";
        _stateMachine = new AiStateMachine(stateMachineId, "idle");
        _stateMachineInitialized = true;

        /// Add default state change logging
        _stateMachine.AddStateChangeListener((from, to, eventName) =>
            {
                Log.ForContext<AiContext>()
                    .Debug(
                        "AI {Mobile}: {From} -> {To} ({Event})",
                        MobileEntity.Name,
                        from,
                        to,
                        eventName
                    );
            }
        );
    }

    public void Say(string message)
    {
        MobileEntity.Speech(ChatMessageType.Regular, 1168, message, 0, 3);
    }

    public void Move(DirectionType direction)
    {
        if (MobileEntity == null)
        {
            throw new InvalidOperationException("MobileEntity is not initialized.");
        }

        var newLocation = MobileEntity.Location + direction;

        var landTile = MobileEntity.Map.GetLandTile(newLocation.X, newLocation.Y);

        newLocation = new Point3D(newLocation.X, newLocation.Y, landTile.Z);

        MoongateContext.Container.Resolve<IMobileService>().MoveMobile(MobileEntity, newLocation);
    }

    public DirectionType RandomDirection()
    {
        return directions[Random.Shared.Next(directions.Length)];
    }

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

    #region State Machine API - Direct Access

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
        _internalData.Clear(); /// Clear all internal data on reset
    }

    #endregion

    #region Predefined AI Setups

    /// <summary>
    /// Setup common AI state machine with predefined transitions
    /// </summary>
    public void SetupCommonAI()
    {
        if (GetData("ai_setup_common", false)) return;
        SetData("ai_setup_common", true);

        /// Basic AI states and transitions
        AddTransition("idle", "see_player", "alert");
        AddTransition("idle", "take_damage", "combat");
        AddTransition("idle", "wander", "moving");
        AddTransition("idle", "hear_noise", "investigating");

        AddTransition("alert", "lose_target", "idle");
        AddTransition("alert", "target_hostile", "combat");
        AddTransition("alert", "investigate", "investigating");
        AddTransition("alert", "timeout", "idle");

        AddTransition("investigating", "find_target", "alert");
        AddTransition("investigating", "timeout", "idle");
        AddTransition("investigating", "take_damage", "combat");

        AddTransition("moving", "see_player", "alert");
        AddTransition("moving", "destination_reached", "idle");
        AddTransition("moving", "take_damage", "combat");
        AddTransition("moving", "blocked", "idle");

        AddTransition("combat", "target_dead", "idle");
        AddTransition("combat", "target_fled", "searching");
        AddTransition("combat", "low_health", "fleeing");
        AddTransition("combat", "lose_target", "searching");

        AddTransition("searching", "find_target", "combat");
        AddTransition("searching", "timeout", "idle");
        AddTransition("searching", "take_damage", "combat");

        AddTransition("fleeing", "safe_distance", "idle");
        AddTransition("fleeing", "cornered", "combat");
        AddTransition("fleeing", "healed", "idle");

        Log.ForContext<AiContext>().Debug("Common AI state machine setup for {Mobile}", MobileEntity.Name);
    }

    /// <summary>
    /// Setup guard AI with specific behaviors
    /// </summary>
    public void SetupGuardAI()
    {
        if (GetData("ai_setup_guard", false)) return;
        SetData("ai_setup_guard", true);

        SetupCommonAI();

        /// Guard-specific transitions
        AddTransition("idle", "start_patrol", "patrolling");
        AddTransition("patrolling", "see_criminal", "chasing");
        AddTransition("patrolling", "end_patrol", "idle");
        AddTransition("chasing", "caught_criminal", "arresting");
        AddTransition("chasing", "lost_criminal", "searching");
        AddTransition("arresting", "arrest_complete", "patrolling");

        /// Setup default patrol points around current location
        var patrolPoints = new List<Point3D>
        {
            MobileEntity.Location,
            MobileEntity.Location + new Point3D(5, 0, 0),
            MobileEntity.Location + new Point3D(5, 5, 0),
            MobileEntity.Location + new Point3D(0, 5, 0)
        };
        SetData("patrol_points", patrolPoints);
        SetData("patrol_index", 0);

        /// Guard conditional transitions
        AddConditionalTransition(
            "alert",
            "attack",
            "combat",
            () =>
            {
                var nearbyPlayers = FindNearbyPlayers(3);
                return nearbyPlayers.Any(IsHostileTowards);
            }
        );

        Log.ForContext<AiContext>().Debug("Guard AI state machine setup for {Mobile}", MobileEntity.Name);
    }

    /// <summary>
    /// Setup merchant AI with trading behaviors
    /// </summary>
    public void SetupMerchantAI()
    {
        if (GetData("ai_setup_merchant", false))
        {
            return;
        }

        SetData("ai_setup_merchant", true);

        /// Merchant states
        AddTransition("idle", "customer_approach", "greeting");
        AddTransition("greeting", "customer_interested", "selling");
        AddTransition("greeting", "customer_leaves", "idle");
        AddTransition("selling", "customer_buys", "happy");
        AddTransition("selling", "customer_haggles", "negotiating");
        AddTransition("selling", "customer_refuses", "disappointed");
        AddTransition("negotiating", "deal_accepted", "happy");
        AddTransition("negotiating", "deal_rejected", "disappointed");
        AddTransition("happy", "timeout", "idle");
        AddTransition("disappointed", "timeout", "idle");

        /// Setup merchant data
        SetData("base_price", 100);
        SetData("min_price", 80);
        SetData("mood_modifier", 1.0);
        SetData("last_sale_time", 0L);

        /// Merchant conditional transitions
        AddConditionalTransition(
            "negotiating",
            "accept_deal",
            "happy",
            () =>
            {
                var proposedPrice = GetData("proposed_price", 0);
                var minPrice = GetData("min_price", 100);
                return proposedPrice >= minPrice;
            }
        );

        Log.ForContext<AiContext>().Debug("Merchant AI state machine setup for {Mobile}", MobileEntity.Name);
    }

    /// <summary>
    /// Setup pack animal AI with following behavior
    /// </summary>
    public void SetupPackAnimalAI(UOMobileEntity owner)
    {
        if (GetData("ai_setup_pack", false))
        {
            return;
        }

        SetData("ai_setup_pack", true);

        SetData("owner", owner);
        SetData("last_owner_position", owner.Location);

        /// Pack animal specific states
        AddTransition("idle", "owner_moves", "following");
        AddTransition("idle", "take_damage", "fleeing");
        AddTransition("following", "owner_stops", "idle");
        AddTransition("following", "owner_far", "catching_up");
        AddTransition("following", "take_damage", "fleeing");
        AddTransition("catching_up", "owner_reached", "following");
        AddTransition("catching_up", "owner_lost", "searching_owner");
        AddTransition("fleeing", "safe", "searching_owner");
        AddTransition("searching_owner", "owner_found", "following");
        AddTransition("searching_owner", "timeout", "idle");

        Log.ForContext<AiContext>().Debug("Pack animal AI setup for {Mobile}", MobileEntity.Name);
    }

    #endregion

    #region Extended AI Context Methods

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
        var dx = MobileEntity.Location.X - target.X;
        var dy = MobileEntity.Location.Y - target.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// State-aware movement - moves based on current state
    /// </summary>
    public void StateAwareMove()
    {
        switch (CurrentState)
        {
            case "patrolling":
                PatrolMove();
                break;
            case "chasing":
                ChaseMove();
                break;
            case "following":
                FollowMove();
                break;
            case "catching_up":
                CatchUpMove();
                break;
            case "fleeing":
                FleeMove();
                break;
            case "searching":
            case "searching_owner":
                SearchMove();
                break;
            default:
                Move(RandomDirection());
                break;
        }
    }

    #endregion

    #region Movement Patterns

    /// <summary>
    /// Patrol movement pattern
    /// </summary>
    private void PatrolMove()
    {
        var patrolPoints = GetData("patrol_points", new List<Point3D>());
        var currentPatrolIndex = GetData("patrol_index", 0);

        if (patrolPoints.Count > 0)
        {
            var targetPoint = patrolPoints[currentPatrolIndex];
            MoveTowards(targetPoint);

            if (IsNearLocation(targetPoint, 1))
            {
                var nextIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                SetData("patrol_index", nextIndex);

                if (nextIndex == 0)
                {
                    Transition("end_patrol");
                }
            }
        }
        else
        {
            Move(RandomDirection());
        }
    }

    /// <summary>
    /// Chase movement towards target
    /// </summary>
    private void ChaseMove()
    {
        var target = GetData<UOMobileEntity>("chase_target");
        if (target != null)
        {
            MoveTowards(target.Location);

            if (IsNearLocation(target.Location, 1))
            {
                Transition("caught_criminal");
            }
        }
        else
        {
            Transition("lost_criminal");
        }
    }

    /// <summary>
    /// Follow owner movement
    /// </summary>
    private void FollowMove()
    {
        var owner = GetData<UOMobileEntity>("owner");
        if (owner != null)
        {
            var distance = GetDistance(owner.Location);

            if (distance > 8)
            {
                Transition("owner_far");
            }
            else if (distance > 2)
            {
                MoveTowards(owner.Location);
            }
            else
            {
                /// Close enough, check if owner stopped
                var lastPos = GetData("last_owner_position", owner.Location);
                if (lastPos.X == owner.Location.X && lastPos.Y == owner.Location.Y)
                {
                    Transition("owner_stops");
                }

                SetData("last_owner_position", owner.Location);
            }
        }
    }

    /// <summary>
    /// Catch up to owner movement - faster
    /// </summary>
    private void CatchUpMove()
    {
        var owner = GetData<UOMobileEntity>("owner");
        if (owner != null)
        {
            var distance = GetDistance(owner.Location);

            if (distance <= 3)
            {
                Transition("owner_reached");
            }
            else if (distance > 20)
            {
                Transition("owner_lost");
            }
            else
            {
                /// Move quickly towards owner
                MoveTowards(owner.Location);
            }
        }
        else
        {
            Transition("owner_lost");
        }
    }

    /// <summary>
    /// Flee movement away from threats
    /// </summary>
    private void FleeMove()
    {
        var threatLocation = GetData("threat_location", Point3D.Zero);
        if (threatLocation != Point3D.Zero)
        {
            var direction = GetDirectionAwayFrom(threatLocation);
            Move(direction);

            if (GetDistance(threatLocation) > 10)
            {
                Transition("safe_distance");
            }
        }
        else
        {
            Move(RandomDirection());
        }
    }

    /// <summary>
    /// Search movement pattern
    /// </summary>
    private void SearchMove()
    {
        var lastKnownPosition = GetData("last_target_position", MobileEntity.Location);
        var searchRadius = GetData("search_radius", 5);

        var targetPoint = GetRandomPointNear(lastKnownPosition, searchRadius);
        MoveTowards(targetPoint);
    }

    #endregion

    #region Helper Methods

    private void MoveTowards(Point3D target)
    {
        var direction = GetDirectionTowards(target);
        Move(direction);
    }

    private DirectionType GetDirectionTowards(Point3D target)
    {
        var dx = target.X - MobileEntity.Location.X;
        var dy = target.Y - MobileEntity.Location.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? DirectionType.East : DirectionType.West;
        }
        else
        {
            return dy > 0 ? DirectionType.South : DirectionType.North;
        }
    }

    private DirectionType GetDirectionAwayFrom(Point3D threat)
    {
        var dx = MobileEntity.Location.X - threat.X;
        var dy = MobileEntity.Location.Y - threat.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? DirectionType.East : DirectionType.West;
        }
        else
        {
            return dy > 0 ? DirectionType.South : DirectionType.North;
        }
    }

    private bool IsNearLocation(Point3D target, int range)
    {
        return GetDistance(target) <= range;
    }

    private Point3D GetRandomPointNear(Point3D center, int radius)
    {
        var random = Random.Shared;
        var x = center.X + random.Next(-radius, radius + 1);
        var y = center.Y + random.Next(-radius, radius + 1);
        return new Point3D(x, y, center.Z);
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

    public void Dispose()
    {
    }
}
