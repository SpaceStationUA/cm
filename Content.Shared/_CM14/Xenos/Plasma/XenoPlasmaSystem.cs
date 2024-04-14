﻿using Content.Shared._CM14.Xenos.Evolution;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Rounding;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._CM14.Xenos.Plasma;

public sealed class XenoPlasmaSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;

    public override void Initialize()
    {
        _xenoPlasmaQuery = GetEntityQuery<XenoPlasmaComponent>();

        SubscribeLocalEvent<XenoPlasmaComponent, MapInitEvent>(OnXenoPlasmaMapInit);
        SubscribeLocalEvent<XenoPlasmaComponent, ComponentRemove>(OnXenoPlasmaRemove);
        SubscribeLocalEvent<XenoPlasmaComponent, XenoRegenEvent>(OnXenoRegen);
        SubscribeLocalEvent<XenoPlasmaComponent, RejuvenateEvent>(OnXenoRejuvenate);
        SubscribeLocalEvent<XenoPlasmaComponent, XenoTransferPlasmaActionEvent>(OnXenoTransferPlasmaAction);
        SubscribeLocalEvent<XenoPlasmaComponent, XenoTransferPlasmaDoAfterEvent>(OnXenoTransferDoAfter);
        SubscribeLocalEvent<XenoPlasmaComponent, NewXenoEvolvedComponent>(OnNewXenoEvolved);
    }

    private void OnXenoPlasmaMapInit(Entity<XenoPlasmaComponent> ent, ref MapInitEvent args)
    {
        UpdateAlert(ent);
    }

    private void OnXenoPlasmaRemove(Entity<XenoPlasmaComponent> ent, ref ComponentRemove args)
    {
        _alerts.ClearAlertCategory(ent, AlertCategory.XenoPlasma);
    }

    private void OnXenoRegen(Entity<XenoPlasmaComponent> xeno, ref XenoRegenEvent args)
    {
        RegenPlasma((xeno, xeno), xeno.Comp.PlasmaRegenOnWeeds);
    }

    private void OnXenoRejuvenate(Entity<XenoPlasmaComponent> xeno, ref RejuvenateEvent args)
    {
        RegenPlasma((xeno, xeno), xeno.Comp.MaxPlasma);
    }

    private void OnXenoTransferPlasmaAction(Entity<XenoPlasmaComponent> xeno, ref XenoTransferPlasmaActionEvent args)
    {
        if (xeno.Owner == args.Target ||
            !HasComp<XenoPlasmaComponent>(args.Target) ||
            !HasPlasma(xeno, args.Amount))
        {
            return;
        }

        args.Handled = true;

        var ev = new XenoTransferPlasmaDoAfterEvent(args.Amount);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.PlasmaTransferDelay, ev, xeno, args.Target)
        {
            BreakOnMove = true,
            DistanceThreshold = args.Range
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoTransferDoAfter(Entity<XenoPlasmaComponent> self, ref XenoTransferPlasmaDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (self.Owner == target ||
            !TryComp(target, out XenoPlasmaComponent? otherXeno) ||
            !TryRemovePlasma((self, self), args.Amount))
        {
            return;
        }

        args.Handled = true;
        RegenPlasma(target, args.Amount);

        // for some reason the popup will sometimes not show for the predicting client here
        if (_net.IsClient)
            return;

        _popup.PopupEntity(Loc.GetString("cm-xeno-plasma-transferred-to-other", ("plasma", args.Amount), ("target", target), ("total", self.Comp.Plasma)), self, self);
        _popup.PopupEntity(Loc.GetString("cm-xeno-plasma-transferred-to-self", ("plasma", args.Amount), ("target", self.Owner), ("total", otherXeno.Plasma)), target, target);

        _audio.PlayPredicted(self.Comp.PlasmaTransferSound, self, self);
    }

    private void OnNewXenoEvolved(Entity<XenoPlasmaComponent> newXeno, ref NewXenoEvolvedComponent args)
    {
        if (TryComp(args.OldXeno, out XenoPlasmaComponent? oldXeno))
        {
            var newPlasma = FixedPoint2.Min(oldXeno.Plasma, newXeno.Comp.MaxPlasma);
            SetPlasma(newXeno, newPlasma);
        }
    }

    private void UpdateAlert(Entity<XenoPlasmaComponent> xeno)
    {
        var level = MathF.Max(0f, xeno.Comp.Plasma.Float());
        var max = _alerts.GetMaxSeverity(AlertType.XenoPlasma);
        var severity = max - ContentHelpers.RoundToLevels(level, xeno.Comp.MaxPlasma, max + 1);
        _alerts.ShowAlert(xeno, AlertType.XenoPlasma, (short) severity);
    }

    public bool HasPlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        return xeno.Comp.Plasma >= plasma;
    }

    public bool HasPlasmaPopup(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma, bool predicted = true)
    {
        if (!Resolve(xeno, ref xeno.Comp))
            return false;

        if (!HasPlasma((xeno, xeno.Comp), plasma))
        {
            var popup = Loc.GetString("cm-xeno-not-enough-plasma");
            if (predicted)
                _popup.PopupClient(popup, xeno, xeno);
            else
                _popup.PopupEntity(popup, xeno, xeno);

            return false;
        }

        return true;
    }

    public void RegenPlasma(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 amount)
    {
        if (!_xenoPlasmaQuery.Resolve(xeno, ref xeno.Comp))
            return;

        var old = xeno.Comp.Plasma;
        xeno.Comp.Plasma = FixedPoint2.Min(xeno.Comp.Plasma + amount, xeno.Comp.MaxPlasma);

        if (old == xeno.Comp.Plasma)
            return;

        Dirty(xeno, xeno.Comp);
        UpdateAlert((xeno, xeno.Comp));
    }

    public void RemovePlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        xeno.Comp.Plasma = FixedPoint2.Max(xeno.Comp.Plasma - plasma, FixedPoint2.Zero);
        Dirty(xeno);
        UpdateAlert(xeno);
    }

    public void SetPlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        xeno.Comp.Plasma = plasma;
        Dirty(xeno);
        UpdateAlert(xeno);
    }

    public bool TryRemovePlasma(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma)
    {
        if (!Resolve(xeno, ref xeno.Comp))
            return false;

        if (!HasPlasma((xeno, xeno.Comp), plasma))
            return false;

        RemovePlasma((xeno, xeno.Comp), plasma);
        return true;
    }

    public bool TryRemovePlasmaPopup(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma)
    {
        if (!Resolve(xeno, ref xeno.Comp))
            return false;

        if (TryRemovePlasma((xeno, xeno.Comp), plasma))
            return true;

        _popup.PopupClient(Loc.GetString("cm-xeno-not-enough-plasma"), xeno, xeno);
        return false;
    }
}
