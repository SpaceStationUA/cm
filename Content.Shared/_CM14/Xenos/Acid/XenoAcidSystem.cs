﻿using Content.Shared._CM14.Xenos.Plasma;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Xenos.Acid;

public sealed class XenoAcidSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidEvent>(OnXenoCorrosiveAcid);
        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidDoAfterEvent>(OnXenoCorrosiveAcidDoAfter);
        SubscribeLocalEvent<CorrodingComponent, EntityUnpausedEvent>(OnCorrodingUnpaused);
    }

    private void OnXenoCorrosiveAcid(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidEvent args)
    {
        if (xeno.Owner != args.Performer ||
            !CheckCorrodablePopups(xeno, args.Target))
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.AcidDelay, new XenoCorrosiveAcidDoAfterEvent(args), xeno, args.Target)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoCorrosiveAcidDoAfter(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CheckCorrodablePopups(xeno, target))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, args.PlasmaCost))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        var acid = SpawnAtPosition(args.AcidId, target.ToCoordinates());
        _transform.SetParent(acid, target);
        AddComp(target, new CorrodingComponent
        {
            Acid = acid,
            CorrodesAt = _timing.CurTime + args.Time
        });
    }

    private void OnCorrodingUnpaused(Entity<CorrodingComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.CorrodesAt += args.PausedTime;
    }

    private bool CheckCorrodablePopups(Entity<XenoAcidComponent> xeno, EntityUid target)
    {
        if (!HasComp<CorrodableComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-not-corrodable", ("target", target)), xeno, xeno);
            return false;
        }

        if (HasComp<CorrodingComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-already-corroding", ("target", target)), xeno, xeno);
            return false;
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CorrodingComponent>();
        var time = _timing.CurTime;

        while (query.MoveNext(out var uid, out var corroding))
        {
            if (time < corroding.CorrodesAt)
                continue;

            QueueDel(uid);
            QueueDel(corroding.Acid);
        }
    }
}
