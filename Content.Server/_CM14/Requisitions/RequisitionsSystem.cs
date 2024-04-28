﻿using System.Numerics;
using Content.Server.Cargo.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared._CM14.Marines;
using Content.Shared._CM14.Requisitions;
using Content.Shared._CM14.Requisitions.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using static Content.Shared._CM14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Server._CM14.Requisitions;

public sealed class RequisitionsSystem : SharedRequisitionsSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsComputerComponent, MapInitEvent>(OnComputerMapInit);
        SubscribeLocalEvent<RequisitionsComputerComponent, BeforeActivatableUIOpenEvent>(OnComputerBeforeActivatableUIOpen);

        SubscribeLocalEvent<RequisitionsElevatorComponent, EntityUnpausedEvent>(OnElevatorUnpaused);

        Subs.BuiEvents<RequisitionsComputerComponent>(RequisitionsUIKey.Key, subs =>
        {
            subs.Event<RequisitionsBuyMsg>(OnBuy);
            subs.Event<RequisitionsPlatformMsg>(OnPlatform);
        });
    }

    private void OnComputerMapInit(Entity<RequisitionsComputerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Account = GetAccount();
        Dirty(ent);
    }

    private void OnElevatorUnpaused(Entity<RequisitionsElevatorComponent> ent, ref EntityUnpausedEvent args)
    {
        if (ent.Comp.ToggledAt is { } at)
            ent.Comp.ToggledAt = at + args.PausedTime;
    }

    private void OnComputerBeforeActivatableUIOpen(Entity<RequisitionsComputerComponent> computer, ref BeforeActivatableUIOpenEvent args)
    {
        SendUIState(computer);
    }

    private void OnBuy(Entity<RequisitionsComputerComponent> computer, ref RequisitionsBuyMsg args)
    {
        var actor = args.Actor;
        if (args.Category >= computer.Comp.Categories.Count)
        {
            Log.Error($"Player {ToPrettyString(actor)} tried to buy out of bounds requisitions order: category {args.Category}");
            return;
        }

        var category = computer.Comp.Categories[args.Category];
        if (args.Order >= category.Entries.Count)
        {
            Log.Error($"Player {ToPrettyString(actor)} tried to buy out of bounds requisitions order: category {args.Category}");
            return;
        }

        var order = category.Entries[args.Order];
        if (!TryComp(computer.Comp.Account, out RequisitionsAccountComponent? account) ||
            account.Balance < order.Cost)
        {
            return;
        }

        if (GetElevator(computer) is not { } elevator)
            return;

        if (IsFull(elevator))
            return;

        account.Balance -= order.Cost;
        elevator.Comp.Orders.Add(order);
        SendUIStateAll();
    }

    private void OnPlatform(Entity<RequisitionsComputerComponent> computer, ref RequisitionsPlatformMsg args)
    {
        if (GetElevator(computer) is not { } elevator)
            return;

        var comp = elevator.Comp;
        if (comp.NextMode != null || comp.Busy)
            return;

        if (comp.Mode == Lowering || comp.Mode == Raising)
            return;

        if (args.Raise && comp.Mode == Raised)
            return;

        if (!args.Raise && comp.Mode == Lowered)
            return;

        RequisitionsElevatorMode? nextMode = comp.Mode switch
        {
            Lowered => Raising,
            Raised => Lowering,
            _ => null
        };

        if (nextMode == null)
            return;

        if (nextMode == Lowering)
        {
            foreach (var entity in _physics.GetContactingEntities(elevator))
            {
                if (HasComp<MobStateComponent>(entity))
                    return;
            }
        }

        comp.ToggledAt = _timing.CurTime;
        comp.Busy = true;
        SetMode(elevator, Preparing, nextMode);
        Dirty(elevator);
    }

    private Entity<RequisitionsAccountComponent> GetAccount()
    {
        var query = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            return (uid, account);
        }

        var newAccount = Spawn(null, MapCoordinates.Nullspace);
        var newAccountComp = EnsureComp<RequisitionsAccountComponent>(newAccount);

        return (newAccount, newAccountComp);
    }

    private Entity<RequisitionsElevatorComponent>? GetElevator(Entity<RequisitionsComputerComponent> computer)
    {
        var elevators = new List<Entity<RequisitionsElevatorComponent, TransformComponent>>();
        var query = EntityQueryEnumerator<RequisitionsElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            elevators.Add((uid, elevator, xform));
        }

        if (elevators.Count == 0)
            return null;

        if (elevators.Count == 1)
            return elevators[0];

        var computerCoords = _transform.GetMapCoordinates(computer);
        Entity<RequisitionsElevatorComponent>? closest = null;
        var closestDistance = float.MaxValue;
        foreach (var (uid, elevator, xform) in elevators)
        {
            var elevatorCoords = _transform.GetMapCoordinates(uid, xform);
            if (computerCoords.MapId != elevatorCoords.MapId)
                continue;

            var distance = (elevatorCoords.Position - computerCoords.Position).LengthSquared();
            if (closestDistance > distance)
            {
                closestDistance = distance;
                closest = (uid, elevator);
            }
        }

        if (closest == null)
            return elevators[0];

        return closest;
    }

    private bool IsFull(Entity<RequisitionsElevatorComponent> elevator)
    {
        var side = elevator.Comp.Radius * 2 + 1;
        return elevator.Comp.Orders.Count >= side * side;
    }

    private void SendUIState(Entity<RequisitionsComputerComponent> computer)
    {
        var elevator = GetElevator(computer);
        var mode = elevator?.Comp.NextMode ?? elevator?.Comp.Mode;
        var busy = elevator?.Comp.Busy ?? false;
        var balance = CompOrNull<RequisitionsAccountComponent>(computer.Comp.Account)?.Balance ?? 0;
        var full = elevator != null && IsFull(elevator.Value);

        var state = new RequisitionsBuiState(mode, busy, balance, full);
        _ui.SetUiState(computer.Owner, RequisitionsUIKey.Key, state);
    }

    private void SendUIStateAll()
    {
        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            SendUIState((uid, computer));
        }
    }

    private void UpdateRailings(Entity<RequisitionsElevatorComponent> elevator, RequisitionsRailingMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<RequisitionsRailingComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            SetRailingMode(railing, mode);
        }
    }

    private void UpdateGears(Entity<RequisitionsElevatorComponent> elevator, RequisitionsGearMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<RequisitionsGearComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            if (railing.Comp.Mode == mode)
                continue;

            railing.Comp.Mode = mode;
            Dirty(railing);
        }
    }

    private void TryPlayAudio(Entity<RequisitionsElevatorComponent> elevator)
    {
        var comp = elevator.Comp;
        if (comp.Audio != null)
            return;

        var time = _timing.CurTime;
        if (comp.NextMode == Lowering || comp.Mode == Lowering)
        {
            if (time < comp.ToggledAt + comp.LowerSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.LoweringSound, elevator)?.Entity;
            return;
        }

        if (comp.NextMode == Raising || comp.Mode == Raising)
        {
            if (time < comp.ToggledAt + comp.RaiseSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.RaisingSound, elevator)?.Entity;
        }
    }

    private void SetMode(Entity<RequisitionsElevatorComponent> elevator, RequisitionsElevatorMode mode, RequisitionsElevatorMode? nextMode)
    {
        elevator.Comp.Mode = mode;
        elevator.Comp.NextMode = nextMode;
        Dirty(elevator);

        RequisitionsGearMode? gearMode = mode switch
        {
            Lowered or Raised or Preparing => RequisitionsGearMode.Static,
            Lowering or Raising => RequisitionsGearMode.Moving,
            _ => null
        };

        if (gearMode != null)
            UpdateGears(elevator, gearMode.Value);

        Console.WriteLine(mode);
        Console.WriteLine(nextMode);
        RequisitionsRailingMode? railingMode = (mode, nextMode) switch
        {
            (Lowered, _) => RequisitionsRailingMode.Raised,
            (Raised, _) => RequisitionsRailingMode.Lowering,
            (_, Lowering) => RequisitionsRailingMode.Raising,
            _ => null
        };

        if (railingMode != null)
            UpdateRailings(elevator, railingMode.Value);

        SendUIStateAll();
    }

    private void SpawnOrders(Entity<RequisitionsElevatorComponent> elevator)
    {
        var comp = elevator.Comp;
        if (comp.Mode == Raised)
        {
            var coordinates = _transform.GetMoverCoordinates(elevator);
            var xOffset = comp.Radius;
            var yOffset = comp.Radius;
            foreach (var order in comp.Orders)
            {
                var crate = SpawnAtPosition(order.Crate, coordinates.Offset(new Vector2(xOffset, yOffset)));

                foreach (var prototype in order.Entities)
                {
                    var entity = Spawn(prototype, MapCoordinates.Nullspace);
                    _entityStorage.Insert(entity, crate);
                }

                yOffset--;
                if (yOffset < -comp.Radius)
                {
                    yOffset = comp.Radius;
                    xOffset--;
                }

                if (xOffset < -comp.Radius)
                    xOffset = comp.Radius;
            }

            comp.Orders.Clear();
        }
    }

    private bool Sell(Entity<RequisitionsElevatorComponent> elevator)
    {
        var account = GetAccount();
        var entities = _physics.GetContactingEntities(elevator);
        var soldAny = false;
        foreach (var entity in entities)
        {
            if (entity == elevator.Comp.Audio)
                continue;

            if (HasComp<CargoSellBlacklistComponent>(entity))
                continue;

            if (TryComp(entity, out RequisitionsCrateComponent? crate))
            {
                account.Comp.Balance += crate.Reward;
                soldAny = true;
            }

            QueueDel(entity);
        }

        if (soldAny)
            Dirty(account);

        return soldAny;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var updateUI = false;
        var accounts = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (accounts.MoveNext(out var uid, out var account))
        {
            if (account.Started)
                continue;

            account.Started = true;
            var marines = Count<MarineComponent>();
            account.Balance = marines * account.StartingDollarsPerMarine;
            Dirty(uid, account);

            updateUI = true;
        }

        var time = _timing.CurTime;
        var elevators = EntityQueryEnumerator<RequisitionsElevatorComponent>();
        while (elevators.MoveNext(out var uid, out var elevator))
        {
            if (time > elevator.ToggledAt + elevator.ToggleDelay)
            {
                elevator.ToggledAt = null;
                elevator.Busy = false;
                Dirty(uid, elevator);
                SendUIStateAll();
                continue;
            }

            if (elevator.ToggledAt == null)
                continue;

            TryPlayAudio((uid, elevator));

            var delay = elevator.NextMode == Raising ? elevator.RaiseDelay : elevator.LowerDelay;
            if (elevator.Mode == Preparing &&
                elevator.NextMode != null &&
                time > elevator.ToggledAt + delay)
            {
                SetMode((uid, elevator), elevator.NextMode.Value, null);
                continue;
            }

            if (elevator.Mode == Lowering || elevator.Mode == Raising)
            {
                var startDelay = delay + elevator.NextMode switch
                {
                    Lowering => elevator.LowerDelay,
                    Raising => elevator.RaiseDelay,
                    _ => TimeSpan.Zero
                };

                var moveDelay= startDelay + elevator.Mode switch
                {
                    Lowering => elevator.LowerDelay,
                    Raising => elevator.RaiseDelay,
                    _ => TimeSpan.Zero
                };

                if (time > elevator.ToggledAt + moveDelay)
                {
                    elevator.Audio = null;

                    var mode = elevator.Mode switch
                    {
                        Raising => Raised,
                        Lowering => Lowered,
                        _ => elevator.Mode
                    };
                    SetMode((uid, elevator), mode, elevator.NextMode);

                    SpawnOrders((uid, elevator));

                    updateUI = true;
                    continue;
                }

                if (elevator.Mode == Lowering &&
                    time > elevator.ToggledAt + delay)
                {
                    if (Sell((uid, elevator)))
                        updateUI = true;
                }
            }
        }

        if (updateUI)
            SendUIStateAll();
    }
}
