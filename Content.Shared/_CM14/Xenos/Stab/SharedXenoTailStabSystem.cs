﻿using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Xenos.Stab;

public abstract class SharedXenoTailStabSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const int AttackMask = (int) (CollisionGroup.MobMask | CollisionGroup.Opaque);

    protected Box2Rotated LastTailAttack;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoTailStabComponent, XenoTailStabEvent>(OnXenoTailStab);
    }

    private void OnXenoTailStab(Entity<XenoTailStabComponent> stab, ref XenoTailStabEvent args)
    {
        if (!_actionBlocker.CanAttack(stab) ||
            !TryComp(stab, out TransformComponent? transform))
        {
            return;
        }

        var userCoords = _transform.GetMapCoordinates(stab, transform);
        if (userCoords.MapId == MapId.Nullspace)
            return;

        var targetCoords = args.Target.ToMap(EntityManager, _transform);
        if (userCoords.MapId != targetCoords.MapId)
            return;

        if (TryComp(stab, out MeleeWeaponComponent? melee))
        {
            if (_timing.CurTime < melee.NextAttack)
                return;

            melee.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(1);
            Dirty(stab, melee);
        }

        var tailRange = stab.Comp.TailRange.Float();
        var box = new Box2(userCoords.Position.X - 0.10f, userCoords.Position.Y, userCoords.Position.X + 0.10f, userCoords.Position.Y + tailRange);

        var matrix = _transform.GetInvWorldMatrix(transform).Transform(targetCoords.Position);
        var rotation = _transform.GetWorldRotation(stab).RotateVec(-matrix).ToWorldAngle();
        var boxRotated = new Box2Rotated(box, rotation, userCoords.Position);
        LastTailAttack = boxRotated;

        // ray on the left side of the box
        var leftRay = new CollisionRay(boxRotated.BottomLeft, (boxRotated.TopLeft - boxRotated.BottomLeft).Normalized(), AttackMask);

        // ray on the right side of the box
        var rightRay = new CollisionRay(boxRotated.BottomRight, (boxRotated.TopRight - boxRotated.BottomRight).Normalized(), AttackMask);

        var hive = CompOrNull<XenoComponent>(stab)?.Hive;

        bool Ignored(EntityUid uid)
        {
            if (uid == stab.Owner)
                return true;

            if (!HasComp<MobStateComponent>(uid))
                return true;

            if (TryComp(uid, out XenoComponent? otherXeno) &&
                hive == otherXeno.Hive)
            {
                return true;
            }

            return false;
        }

        // dont open allocations ahead
        // entity lookups dont work properly with Box2Rotated
        // so we do one ray cast on each side instead since its narrow enough
        // im sure you could calculate the ray bounds more efficiently
        // but have you seen these allocations either way
        var intersect = _physics.IntersectRayWithPredicate(transform.MapID, leftRay, tailRange, Ignored, false);
        intersect = intersect.Concat(_physics.IntersectRayWithPredicate(transform.MapID, rightRay, tailRange, Ignored, false));
        var results = intersect.Select(r => r.HitEntity).ToHashSet();

        var actualResults = new List<EntityUid>();
        foreach (var result in results)
        {
            if (!_interaction.InRangeUnobstructed(stab, result, range: stab.Comp.TailRange.Float()))
                continue;

            actualResults.Add(result);
            if (actualResults.Count == 3)
                break;
        }

        // TODO CM14 sounds
        // TODO CM14 lag compensation
        var damage = new DamageSpecifier(stab.Comp.TailDamage);
        if (actualResults.Count == 0)
        {
            var missEvent = new MeleeHitEvent(new List<EntityUid>(), stab, stab, damage, null);
            RaiseLocalEvent(stab, missEvent);

            foreach (var action in _actions.GetActions(stab))
            {
                if (TryComp(action.Id, out XenoTailStabActionComponent? actionComp))
                    _actions.SetCooldown(action.Id, actionComp.MissCooldown);
            }
        }
        else
        {
            args.Handled = true;

            var hitEvent = new MeleeHitEvent(actualResults, stab, stab, damage, null);
            RaiseLocalEvent(stab, hitEvent);

            if (!hitEvent.Handled)
            {
                _interaction.DoContactInteraction(stab, stab);

                foreach (var hit in actualResults)
                {
                    _interaction.DoContactInteraction(stab, hit);
                }

                var filter = Filter.Pvs(transform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == stab.Owner);
                foreach (var hit in actualResults)
                {
                    var attackedEv = new AttackedEvent(stab, stab, args.Target);
                    RaiseLocalEvent(hit, attackedEv);

                    var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage + hitEvent.BonusDamage + attackedEv.BonusDamage, hitEvent.ModifiersList);
                    var change = _damageable.TryChangeDamage(hit, modifiedDamage, origin: stab);

                    if (change?.GetTotal() > FixedPoint2.Zero)
                    {
                        _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { hit }, filter);
                    }
                }
            }
        }

        var localPos = transform.LocalRotation.RotateVec(matrix);

        var length = localPos.Length();
        localPos *= tailRange / length;

        DoLunge((stab, stab, transform), localPos, "WeaponArcThrust");

        _audio.PlayPredicted(stab.Comp.TailHitSound, stab, stab);

        var attackEv = new MeleeAttackEvent(stab);
        RaiseLocalEvent(stab, ref attackEv);
    }

    protected virtual void DoLunge(Entity<XenoTailStabComponent, TransformComponent> user, Vector2 localPos, EntProtoId animationId)
    {
    }
}
