using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._CM14.Marines.Orders;

public abstract class SharedMarineOrdersSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private readonly HashSet<Entity<MarineComponent>> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineOrdersComponent, ComponentGetStateAttemptEvent>(OnComponentGetState);
        SubscribeLocalEvent<FocusOrderComponent, ComponentGetStateAttemptEvent>(OnComponentGetState);
        SubscribeLocalEvent<HoldOrderComponent, ComponentGetStateAttemptEvent>(OnComponentGetState);
        SubscribeLocalEvent<MoveOrderComponent, ComponentGetStateAttemptEvent>(OnComponentGetState);

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


    private void OnDamageModify(EntityUid uid, HoldOrderComponent comp, DamageModifyEvent args)
    {
        var damage = args.Damage.DamageDict;
        var multiplier = 1 * comp.DamageModifier;

        foreach (var type in comp.DamageTypes)
        {
            if (damage.TryGetValue(type, out var amount))
                damage[type] = amount * multiplier;
        }
    }
    private void OnRefreshMovement(EntityUid uid, MoveOrderComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        var speed = (1 * comp.MoveSpeedModifier).Float();
        args.ModifySpeed(speed, speed);
    }

    private void OnUnpause<T>(EntityUid uid, T comp, EntityUnpausedEvent args) where T : IComponent, IOrderComponent
    {
        comp.Duration += args.PausedTime;
    }

    private void OnMoveShutdown(Entity<MoveOrderComponent> uid, ref ComponentShutdown ev)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    protected virtual void OnAction(EntityUid uid, MarineOrdersComponent orders, FocusActionEvent args)
    {
        OnAction(uid, Orders.Focus, orders, args);

    }

    protected virtual void OnAction(EntityUid uid, MarineOrdersComponent orders, HoldActionEvent args)
    {
        OnAction(uid, Orders.Hold, orders, args);

    }

    protected virtual void OnAction(EntityUid uid, MarineOrdersComponent orders, MoveActionEvent args)
    {
        OnAction(uid, Orders.Move, orders, args);
    }

    private void OnAction(EntityUid uid, Orders order, MarineOrdersComponent orders, InstantActionEvent args)
    {
        if (args.Handled)
            return;

        HandleAction(uid, order, orders);

        args.Handled = true;
    }

    private void HandleAction(EntityUid uid, Orders order, MarineOrdersComponent orderComp)
    {

        if (!TryComp<TransformComponent>(uid, out var xform))
        {
            DebugTools.Assert("Order issued by an entity without TransformComponent");
            return;
        }

        _actions.SetCooldown(orderComp.FocusActionEntity, orderComp.Cooldown);
        _actions.SetCooldown(orderComp.MoveActionEntity, orderComp.Cooldown);
        _actions.SetCooldown(orderComp.HoldActionEntity, orderComp.Cooldown);

        _receivers.Clear();

        _entityLookup.GetEntitiesInRange(xform.Coordinates, orderComp.OrderRange, _receivers);

        foreach (var receiver in _receivers)
        {
            AddOrder(receiver, order, orderComp);
        }
    }

    /// <summary>
    /// Adds an order component to an entity. If the order already exists then the multiplier and duration is overriden.
    /// </summary>
    private void AddOrder(EntityUid uid, Orders order, MarineOrdersComponent orderComp)
    {
        switch (order)
        {
            case Orders.Focus:
                var focusComp = EnsureComp<FocusOrderComponent>(uid);
                focusComp.AssignMultiplier(orderComp.Multiplier);
                focusComp.Duration = _timing.CurTime + orderComp.Duration;
                break;
            case Orders.Hold:
                var holdComp = EnsureComp<HoldOrderComponent>(uid);
                holdComp.AssignMultiplier(orderComp.Multiplier);
                holdComp.Duration = _timing.CurTime + orderComp.Duration;
                break;
            case Orders.Move:
                var moveComp = EnsureComp<MoveOrderComponent>(uid);
                moveComp.AssignMultiplier(orderComp.Multiplier);
                moveComp.Duration = _timing.CurTime + orderComp.Duration;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
                break;
            default:
                DebugTools.Assert("Invalid Order");
                break;
        }
    }

    private void OnComponentGetState<T>(EntityUid uid, T comp, ref ComponentGetStateAttemptEvent args)
    {
        // It's null on replays apparently
        if (args.Player is null)
            return;

        args.Cancelled = !HasComp<MarineComponent>(args.Player.AttachedEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RemoveExpired<MoveOrderComponent>();
        RemoveExpired<FocusOrderComponent>();
        RemoveExpired<HoldOrderComponent>();
    }

    private void RemoveExpired<T>() where T: IComponent, IOrderComponent
    {
        var query = EntityQueryEnumerator<T>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime > comp.Duration)
            {
                RemCompDeferred<T>(uid);
            }
        }
    }
}
