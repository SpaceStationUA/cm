﻿using Content.Shared.DoAfter;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using static Content.Shared.Storage.StorageComponent;

namespace Content.Shared._CM14.Storage;

public sealed class CMStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private readonly List<EntityUid> _toRemove = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<StorageFillComponent, CMStorageItemFillEvent>(OnStorageFillItem);
        SubscribeLocalEvent<StorageOpenDoAfterComponent, OpenStorageDoAfterEvent>(OnStorageOpenDoAfter);

        Subs.BuiEvents<StorageCloseOnMoveComponent>(StorageUiKey.Key, sub =>
        {
            sub.Event<BoundUIOpenedEvent>(OnCloseOnMoveUIOpened);
        });

        Subs.BuiEvents<StorageOpenComponent>(StorageUiKey.Key, sub =>
        {
            sub.Event<BoundUIClosedEvent>(OnCloseOnMoveUIClosed);
        });
    }

    private void OnStorageFillItem(Entity<StorageFillComponent> storage, ref CMStorageItemFillEvent args)
    {
        var tries = 0;
        while (!_storage.CanInsert(storage, args.Item, out var reason) &&
               reason == "comp-storage-insufficient-capacity" &&
               tries < 3)
        {
            tries++;

            // TODO CM14 make this error if this is a cm-specific storage
            Log.Warning($"Storage {ToPrettyString(storage)} can't fit {ToPrettyString(args.Item)}");

            var modified = false;
            foreach (var shape in _item.GetItemShape((args.Item, args.Item)))
            {
                var grid = args.Storage.Grid;
                if (grid.Count == 0)
                {
                    grid.Add(shape);
                    continue;
                }

                // TODO CM14 this might create more space than is necessary to fit the item if there is some free space left in the storage before expanding it
                var last = grid[^1];
                var expanded = new Box2i(last.Left, last.Bottom, last.Right + shape.Width + 1, last.Top);

                if (expanded.Top < shape.Top)
                    expanded.Top = shape.Top;

                grid[^1] = expanded;
                modified = true;
            }

            if (modified)
                Dirty(storage);
        }
    }

    public bool IgnoreItemSize(Entity<StorageComponent> storage, EntityUid item)
    {
        return TryComp(storage, out IgnoreContentsSizeComponent? ignore) &&
               ignore.Items.IsValid(item, EntityManager);
    }

    public bool OpenDoAfter(EntityUid uid, EntityUid entity, StorageComponent? storageComp = null, bool silent = false)
    {
        if (!TryComp(uid, out StorageOpenDoAfterComponent? comp) ||
            comp.Duration == TimeSpan.Zero)
        {
            return false;
        }

        var ev = new OpenStorageDoAfterEvent(GetNetEntity(uid), GetNetEntity(entity), silent);
        var doAfter = new DoAfterArgs(EntityManager, entity, comp.Duration, ev, uid)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfter);

        return true;
    }

    private void OnStorageOpenDoAfter(Entity<StorageOpenDoAfterComponent> ent, ref OpenStorageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryGetEntity(args.Uid, out var uid) ||
            !TryGetEntity(args.Entity, out var entity))
        {
            return;
        }

        if (!TryComp(uid, out StorageComponent? storage))
            return;

        args.Handled = true;
        _storage.OpenStorageUI(uid.Value, entity.Value, storage, args.Silent, false);
    }

    private void OnCloseOnMoveUIOpened(Entity<StorageCloseOnMoveComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.Session.AttachedEntity is not { } player)
            return;

        var coordinates = GetNetCoordinates(_transform.GetMoverCoordinates(player));
        EnsureComp<StorageOpenComponent>(ent).OpenedAt[player] = coordinates;
    }

    private void OnCloseOnMoveUIClosed(Entity<StorageOpenComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.Session.AttachedEntity is not { } player)
            return;

        ent.Comp.OpenedAt.Remove(player);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<StorageOpenComponent>();
        while (query.MoveNext(out var uid, out var open))
        {
            _toRemove.Clear();
            foreach (var (user, netOrigin) in open.OpenedAt)
            {
                if (TerminatingOrDeleted(user))
                {
                    _toRemove.Add(user);
                    continue;
                }

                var origin = GetCoordinates(netOrigin);
                var current = _transform.GetMoverCoordinates(user);

                if (!origin.InRange(EntityManager, _transform, current, 0.1f))
                    _toRemove.Add(user);
            }

            foreach (var user in _toRemove)
            {
                if (_net.IsServer && TryComp(user, out ActorComponent? actor))
                    _ui.TryClose(uid, StorageUiKey.Key, actor.PlayerSession);

                if (_net.IsClient &&
                    TryComp(uid, out UserInterfaceComponent? ui) &&
                    ui.OpenInterfaces.TryGetValue(StorageUiKey.Key, out var openUI))
                {
                    openUI.Close();
                }

                open.OpenedAt.Remove(user);
            }

            if (open.OpenedAt.Count == 0)
                RemCompDeferred<StorageOpenComponent>(uid);
        }
    }
}
