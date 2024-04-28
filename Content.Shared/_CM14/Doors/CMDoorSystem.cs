﻿using Content.Shared.Directions;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Doors;

public sealed class CMDoorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<CMDoubleDoorComponent> _doubleQuery;

    public override void Initialize()
    {
        _doorQuery = GetEntityQuery<DoorComponent>();
        _doubleQuery = GetEntityQuery<CMDoubleDoorComponent>();

        // TODO CM14 there is an edge case where one door can close but the other can't, to fix this CanClose should be checked on the adjacent door when a double door tries to close
        SubscribeLocalEvent<CMDoubleDoorComponent, DoorStateChangedEvent>(OnDoorStateChanged);
    }

    private void OnDoorStateChanged(Entity<CMDoubleDoorComponent> door, ref DoorStateChangedEvent args)
    {
        switch (args.State)
        {
            case DoorState.Opening:
                Open(door);
                break;
            case DoorState.Closing:
                Close(door);
                break;
        }
    }

    private AnchoredEntitiesEnumerator? GetAdjacentEnumerator(Entity<CMDoubleDoorComponent> ent)
    {
        if (!TryComp(ent, out TransformComponent? transform) ||
            !TryComp(transform.GridUid, out MapGridComponent? grid))
        {
            return default;
        }

        var adjacent = transform.Coordinates.Offset(transform.LocalRotation.GetCardinalDir());
        var position = _map.LocalToTile(transform.GridUid.Value, grid, adjacent);
        return _map.GetAnchoredEntitiesEnumerator(transform.GridUid.Value, grid, position);
    }

    private bool AreFacing(EntityUid one, EntityUid two)
    {
        return TryComp(one, out TransformComponent? transformOne) &&
               TryComp(two, out TransformComponent? transformTwo) &&
               transformOne.LocalRotation.GetCardinalDir().GetOpposite() ==
               transformTwo.LocalRotation.GetCardinalDir();
    }

    private void Open(Entity<CMDoubleDoorComponent> ent)
    {
        if (GetAdjacentEnumerator(ent) is not { } enumerator)
            return;

        var time = _timing.CurTime;

        ent.Comp.LastOpeningAt = time;
        Dirty(ent);

        while (enumerator.MoveNext(out var anchored))
        {
            if (_doubleQuery.TryGetComponent(anchored, out var doubleDoor) &&
                doubleDoor.LastOpeningAt != time &&
                AreFacing(ent, anchored.Value) &&
                _doorQuery.TryGetComponent(anchored, out var door) &&
                door.State != DoorState.Opening)
            {
                doubleDoor.LastOpeningAt = time;
                Dirty(anchored.Value, doubleDoor);

                var sound = door.OpenSound;
                door.OpenSound = null;
                _doors.StartOpening(anchored.Value, door);
                door.OpenSound = sound;
            }
        }
    }

    private void Close(Entity<CMDoubleDoorComponent> ent)
    {
        if (GetAdjacentEnumerator(ent) is not { } enumerator)
            return;

        var time = _timing.CurTime;

        ent.Comp.LastClosingAt = time;
        Dirty(ent);

        while (enumerator.MoveNext(out var anchored))
        {
            if (_doubleQuery.TryGetComponent(anchored, out var doubleDoor) &&
                doubleDoor.LastClosingAt != time &&
                AreFacing(ent, anchored.Value) &&
                _doorQuery.TryGetComponent(anchored, out var door) &&
                door.State != DoorState.Closing)
            {
                doubleDoor.LastClosingAt = time;
                Dirty(anchored.Value, doubleDoor);

                var sound = door.CloseSound;
                door.CloseSound = null;
                _doors.StartClosing(anchored.Value, door);
                door.CloseSound = sound;
            }
        }
    }
}
