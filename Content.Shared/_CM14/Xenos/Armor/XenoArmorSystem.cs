﻿using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._CM14.Xenos.Armor;

public sealed class XenoArmorSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, DamageModifyEvent>(OnXenoDamageModify);
        SubscribeLocalEvent<XenoArmorComponent, XenoGetArmorEvent>(OnXenoGetArmor);
        SubscribeLocalEvent<CMArmorPiercingComponent, XenoGetArmorEvent>(OnPiercingXenoGetArmor);
    }

    private void OnXenoDamageModify(Entity<XenoComponent> xeno, ref DamageModifyEvent args)
    {
        var ev = new XenoGetArmorEvent();
        RaiseLocalEvent(xeno, ref ev);

        if (args.Tool != null)
            RaiseLocalEvent(args.Tool.Value, ref ev);

        var armor = Math.Max(ev.Armor, 0);
        if (args.Origin is { } origin)
        {
            var originCoords = _transform.GetMapCoordinates(origin);
            var xenoCoords = _transform.GetMapCoordinates(xeno);

            if (originCoords.MapId == xenoCoords.MapId)
            {
                var diff = (originCoords.Position - xenoCoords.Position).ToWorldAngle().GetCardinalDir();
                if (diff == _transform.GetWorldRotation(xeno).GetCardinalDir())
                {
                    armor += ev.FrontalArmor;
                }
            }
        }

        if (armor <= 0)
            return;

        var resist = Math.Pow(1.1, armor / 5.0);
        args.Damage /= resist;

        var newDamage = args.Damage.GetTotal();
        if (newDamage < armor * 2)
        {
            var damageWithArmor = FixedPoint2.Max(0, newDamage * 4 - armor);
            args.Damage *= damageWithArmor / (newDamage * 4);
        }
    }

    private void OnXenoGetArmor(Entity<XenoArmorComponent> xeno, ref XenoGetArmorEvent args)
    {
        args.Armor += xeno.Comp.Armor;
    }

    private void OnPiercingXenoGetArmor(Entity<CMArmorPiercingComponent> ent, ref XenoGetArmorEvent args)
    {
        args.Armor -= ent.Comp.Amount;
    }
}
