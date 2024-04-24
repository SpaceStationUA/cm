﻿using System.Numerics;
using Content.Server.Spreader;
using Content.Shared._CM14.Xenos.Construction;
using Content.Shared.Atmos;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using XenoWeedableComponent = Content.Shared._CM14.Xenos.Construction.Nest.XenoWeedableComponent;
using XenoWeedsComponent = Content.Shared._CM14.Xenos.Construction.XenoWeedsComponent;

namespace Content.Server._CM14.Xenos.Construction;

public sealed class XenoConstructionSystem : SharedXenoConstructionSystem
{
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly List<EntityUid> _anchored = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiveCoreComponent, MapInitEvent>(OnHiveCoreMapInit);
        SubscribeLocalEvent<XenoWeedsComponent, SpreadNeighborsEvent>(OnWeedsSpreadNeighbors);
        SubscribeLocalEvent<XenoWeedableComponent, AnchorStateChangedEvent>(OnWeedableAnchorStateChanged);
    }

    private void OnHiveCoreMapInit(Entity<HiveCoreComponent> ent, ref MapInitEvent args)
    {
        var coordinates = _transform.GetMoverCoordinates(ent).SnapToGrid(EntityManager, _map);
        Spawn(ent.Comp.Spawns, coordinates);
    }

    private void OnWeedsSpreadNeighbors(Entity<XenoWeedsComponent> ent, ref SpreadNeighborsEvent args)
    {
        var source = ent.Comp.IsSource ? ent.Owner : ent.Comp.Source;

        // TODO CM14
        // There is an edge case right now where existing weeds can block new weeds
        // from expanding further. If this is the case then the weeds should reassign
        // their source to this one and reactivate if it is closer to them than their
        // original source and only if it is still within range
        if (args.NeighborFreeTiles.Count <= 0 ||
            !Exists(source) ||
            !TryComp(source, out TransformComponent? transform) ||
            ent.Comp.Spawns.Id is not { } prototype)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        var any = false;
        foreach (var neighbor in args.NeighborFreeTiles)
        {
            var gridOwner = neighbor.Grid.Owner;
            var tile = neighbor.Tile.GridIndices;
            var coords = _mapSystem.GridTileToLocal(gridOwner, neighbor.Grid, tile);

            var sourceLocal = _mapSystem.CoordinatesToTile(gridOwner, neighbor.Grid, transform.Coordinates);
            var diff = Vector2.Abs(tile - sourceLocal);
            if (diff.X >= ent.Comp.Range || diff.Y >= ent.Comp.Range)
                break;

            var neighborWeeds = Spawn(prototype, coords);
            var neighborWeedsComp = EnsureComp<XenoWeedsComponent>(neighborWeeds);

            neighborWeedsComp.IsSource = false;
            neighborWeedsComp.Source = source;

            EnsureComp<ActiveEdgeSpreaderComponent>(neighborWeeds);

            any = true;

            for (var i = 0; i < 4; i++)
            {
                var dir = (AtmosDirection) (1 << i);
                var pos = neighbor.Tile.GridIndices.Offset(dir);
                if (!_mapSystem.TryGetTileRef(gridOwner, neighbor.Grid, pos, out var adjacent))
                    continue;

                _anchored.Clear();
                _mapSystem.GetAnchoredEntities((gridOwner, neighbor.Grid), adjacent.GridIndices, _anchored);
                foreach (var anchored in _anchored)
                {
                    if (!TryComp(anchored, out XenoWeedableComponent? weedable) ||
                        weedable.Entity != null ||
                        !TryComp(anchored, out TransformComponent? weedableTransform) ||
                        !weedableTransform.Anchored)
                    {
                        continue;
                    }

                    weedable.Entity = SpawnAtPosition(weedable.Spawn, anchored.ToCoordinates());
                }
            }
        }

        if (!any)
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);

        args.Updates--;
    }

    private void OnWeedableAnchorStateChanged(Entity<XenoWeedableComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            QueueDel(ent.Comp.Entity);
    }
}
