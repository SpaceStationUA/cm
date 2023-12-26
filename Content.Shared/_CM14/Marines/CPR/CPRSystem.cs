﻿using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Marines.CPR;

public sealed class CPRSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // TODO CM14 move this to a component
    [ValidatePrototypeId<DamageTypePrototype>]
    private const string HealType = "Asphyxiation";

    private static readonly FixedPoint2 HealAmount = FixedPoint2.New(10);

    public override void Initialize()
    {
        base.Initialize();

        // TODO CM14 something more generic than "marine"
        SubscribeLocalEvent<MarineComponent, InteractHandEvent>(OnMarineInteractHand);
        SubscribeLocalEvent<MarineComponent, CPRDoAfterEvent>(OnMarineDoAfter);

        SubscribeLocalEvent<ReceivingCPRComponent, ReceiveCPRAttemptEvent>(OnReceivingCPRAttempt);
        SubscribeLocalEvent<CPRReceivedComponent, ReceiveCPRAttemptEvent>(OnReceivedCPRAttempt);
        SubscribeLocalEvent<MobStateComponent, ReceiveCPRAttemptEvent>(OnMobStateCPRAttempt);

        // TODO CM14 pending PR upstream https://github.com/space-wizards/space-station-14/pull/22395
        // SubscribeLocalEvent<MaskComponent, ReceiveCPRAttemptEvent(OnMaskCPRAttempt);
    }

    private void OnMarineInteractHand(Entity<MarineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = StartCPR(args.User, args.Target);
    }

    private void OnMarineDoAfter(Entity<MarineComponent> ent, ref CPRDoAfterEvent args)
    {
        var performer = args.User;

        if (args.Target != null)
            RemComp<ReceivingCPRComponent>(args.Target.Value);

        if (args.Cancelled ||
            args.Handled ||
            args.Target is not { } target ||
            !CanCPRPopup(performer, target, false, out var damage))
        {
            return;
        }

        args.Handled = true;

        if (!TryComp(target, out DamageableComponent? damageable) ||
            !damageable.Damage.DamageDict.TryGetValue(HealType, out damage) ||
            damage <= FixedPoint2.Zero)
        {
            return;
        }

        var heal = -FixedPoint2.Min(damage, HealAmount);
        var healSpecifier = new DamageSpecifier();
        healSpecifier.DamageDict.Add(HealType, heal);
        _damageable.TryChangeDamage(target, healSpecifier, true);
        EnsureComp<CPRReceivedComponent>(target).Last = _timing.CurTime;

        if (_net.IsClient)
            return;

        // TODO CM14 move this value to a component
        var selfPopup = Loc.GetString("cm-cpr-self-perform", ("target", target), ("seconds", 7));
        _popups.PopupEntity(selfPopup, target, performer);

        var othersPopup = Loc.GetString("cm-cpr-other-perform", ("performer", performer), ("target", target));
        var othersFilter = Filter.Pvs(performer).RemoveWhereAttachedEntity(e => e == performer);
        _popups.PopupEntity(othersPopup, performer, othersFilter, true);
    }

    private void OnReceivingCPRAttempt(Entity<ReceivingCPRComponent> ent, ref ReceiveCPRAttemptEvent args)
    {
        args.Cancelled = true;

        if (_net.IsClient)
            return;

        var popup = Loc.GetString("cm-cpr-already-being-performed", ("target", ent.Owner));
        _popups.PopupEntity(popup, ent, args.Performer);
    }

    private void OnReceivedCPRAttempt(Entity<CPRReceivedComponent> ent, ref ReceiveCPRAttemptEvent args)
    {
        if (args.Start)
            return;

        var target = ent.Owner;
        var performer = args.Performer;

        // TODO CM14 move this value to a component
        if (ent.Comp.Last > _timing.CurTime - TimeSpan.FromSeconds(7))
        {
            args.Cancelled = true;

            if (_net.IsClient)
                return;

            var selfPopup = Loc.GetString("cm-cpr-self-perform-fail-received-too-recently", ("target", target));
            _popups.PopupEntity(selfPopup, target, performer);

            var othersPopup = Loc.GetString("cm-cpr-other-perform-fail", ("performer", performer), ("target", target));
            var othersFilter = Filter.Pvs(performer).RemoveWhereAttachedEntity(e => e == performer);
            _popups.PopupEntity(othersPopup, performer, othersFilter, true);
        }
    }

    private void OnMobStateCPRAttempt(Entity<MobStateComponent> ent, ref ReceiveCPRAttemptEvent args)
    {
        if (_mobState.IsAlive(ent))
        {
            args.Cancelled = true;
            return;
        }

        if (_mobState.IsCritical(ent))
        {
            if (!TryComp(ent, out DamageableComponent? damageable) ||
                !damageable.Damage.DamageDict.TryGetValue(HealType, out var damage) ||
                damage <= FixedPoint2.Zero)
            {
                args.Cancelled = true;
                return;
            }
        }

        if (_mobState.IsDead(ent))
        {
            // TODO CM14 extend revivable time after death, upstream this is rotting, downstream it needs to be different
            args.Cancelled = true;
        }
    }

    private bool CanCPRPopup(EntityUid performer, EntityUid target, bool start, out FixedPoint2 damage)
    {
        damage = default;

        if (!HasComp<MarineComponent>(target))
            return false;

        var performAttempt = new PerformCPRAttemptEvent(target);
        RaiseLocalEvent(performer, ref performAttempt);

        if (performAttempt.Cancelled)
            return false;

        var receiveAttempt = new ReceiveCPRAttemptEvent(performer, start);
        RaiseLocalEvent(target, ref receiveAttempt);

        if (receiveAttempt.Cancelled)
            return false;

        if (!_hands.TryGetEmptyHand(performer, out _))
            return false;

        return true;
    }

    private bool StartCPR(EntityUid performer, EntityUid target)
    {
        if (!CanCPRPopup(performer, target, true, out _))
            return false;

        EnsureComp<ReceivingCPRComponent>(target);

        // TODO CM14 less time for skilled doctors
        var doAfter = new DoAfterArgs(EntityManager, performer, TimeSpan.FromSeconds(4), new CPRDoAfterEvent(), performer, target)
        {
            BreakOnUserMove = true,
            BreakOnTargetMove = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfter);

        if (_net.IsClient)
            return true;

        var selfPopup = Loc.GetString("cm-cpr-self-start-perform", ("target", target));
        _popups.PopupEntity(selfPopup, target, performer);

        var othersPopup = Loc.GetString("cm-cpr-other-start-perform", ("performer", performer), ("target", target));
        var othersFilter = Filter.Pvs(performer).RemoveWhereAttachedEntity(e => e == performer);
        _popups.PopupEntity(othersPopup, performer, othersFilter, true);

        return true;
    }
}
