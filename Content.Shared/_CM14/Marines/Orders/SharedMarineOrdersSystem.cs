using Content.Shared._CM14.Marines.Skills;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Marines.Orders;

public abstract class SharedMarineOrdersSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<Entity<MarineComponent>> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoveOrderComponent, EntityUnpausedEvent>(OnUnpause);
        SubscribeLocalEvent<FocusOrderComponent, EntityUnpausedEvent>(OnUnpause);
        SubscribeLocalEvent<HoldOrderComponent, EntityUnpausedEvent>(OnUnpause);

        SubscribeLocalEvent<MarineOrdersComponent, FocusActionEvent>(OnAction);
        SubscribeLocalEvent<MarineOrdersComponent, HoldActionEvent>(OnAction);
        SubscribeLocalEvent<MarineOrdersComponent, MoveActionEvent>(OnAction);

        SubscribeLocalEvent<MoveOrderComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);
        SubscribeLocalEvent<HoldOrderComponent, DamageModifyEvent>(OnDamageModify);

        SubscribeLocalEvent<MoveOrderComponent, ComponentShutdown>(OnMoveShutdown);
    }

    private void OnDamageModify(Entity<HoldOrderComponent> orders, ref DamageModifyEvent args)
    {
        var comp = orders.Comp;
        if (comp.Received.Count == 0)
            return;

        var damage = args.Damage.DamageDict;
        var multiplier = 1 - comp.DamageModifier * comp.Received[0].Multiplier;

        foreach (var type in comp.DamageTypes)
        {
            if (damage.TryGetValue(type, out var amount))
                damage[type] = amount * multiplier;
        }
    }

    private void OnRefreshMovement(Entity<MoveOrderComponent> orders, ref RefreshMovementSpeedModifiersEvent args)
    {
        var comp = orders.Comp;
        if (comp.Received.Count == 0)
            return;

        var speed = 1 + (comp.MoveSpeedModifier * comp.Received[0].Multiplier).Float();
        args.ModifySpeed(speed, speed);
    }

    private void OnUnpause<T>(Entity<T> orders, ref EntityUnpausedEvent args) where T : IComponent, IOrderComponent
    {
        var comp = orders.Comp;
        for (var i = 0; i < comp.Received.Count; i++)
        {
            var received = comp.Received[i];
            comp.Received[i] = (received.Multiplier, received.ExpiresAt + args.PausedTime);
        }
    }

    private void OnMoveShutdown(Entity<MoveOrderComponent> uid, ref ComponentShutdown ev)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    protected virtual void OnAction(Entity<MarineOrdersComponent> orders, ref FocusActionEvent args)
    {
        OnAction<FocusOrderComponent>(orders, args);
    }

    protected virtual void OnAction(Entity<MarineOrdersComponent> orders, ref HoldActionEvent args)
    {
        OnAction<HoldOrderComponent>(orders, args);
    }

    protected virtual void OnAction(Entity<MarineOrdersComponent> orders, ref MoveActionEvent args)
    {
        OnAction<MoveOrderComponent>(orders, args);
    }

    private void OnAction<T>(Entity<MarineOrdersComponent> orders, InstantActionEvent args) where T : IOrderComponent, new()
    {
        if (args.Handled)
            return;

        if (HandleAction<T>(orders))
            args.Handled = true;
    }

    private bool HandleAction<T>(Entity<MarineOrdersComponent> orders) where T : IOrderComponent, new()
    {
        if (!TryComp(orders, out TransformComponent? xform) ||
            _mobState.IsDead(orders))
        {
            return false;
        }

        var level = Math.Max(1, CompOrNull<SkillsComponent>(orders)?.Leadership ?? 1);
        var duration = orders.Comp.Duration * (level + 1);

        // TODO CM14 implement focus order effects
        // _actions.SetCooldown(orders.Comp.FocusActionEntity, orders.Comp.Cooldown);
        _actions.SetCooldown(orders.Comp.MoveActionEntity, orders.Comp.Cooldown);
        _actions.SetCooldown(orders.Comp.HoldActionEntity, orders.Comp.Cooldown);

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, orders.Comp.OrderRange, _receivers);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            AddOrder<T>(receiver, level, duration);
        }

        return true;
    }

    /// <summary>
    /// Adds an order component to an entity. If the order already exists then the multiplier and duration is overriden.
    /// </summary>
    private void AddOrder<T>(Entity<MarineComponent> receiver, int multiplier, TimeSpan duration) where T : IOrderComponent, new()
    {
        var time = _timing.CurTime;
        var comp = EnsureComp<T>(receiver);

        comp.Received.Add((multiplier, time + duration));
        comp.Received.Sort((a, b) => a.CompareTo(b));

        _movementSpeed.RefreshMovementSpeedModifiers(receiver);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RemoveExpired<MoveOrderComponent>();
        RemoveExpired<FocusOrderComponent>();
        RemoveExpired<HoldOrderComponent>();
    }

    private void RemoveExpired<T>() where T : IComponent, IOrderComponent
    {
        var query = EntityQueryEnumerator<T>();
        var time = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp))
        {
            for (var i = comp.Received.Count - 1; i >= 0; i--)
            {
                var received = comp.Received[i];
                if (received.ExpiresAt < time)
                    comp.Received.RemoveAt(i);
            }

            if (comp.Received.Count == 0)
                RemCompDeferred<T>(uid);
        }
    }
}
