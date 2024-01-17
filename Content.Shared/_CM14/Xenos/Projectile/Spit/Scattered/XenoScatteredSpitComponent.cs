﻿using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Xenos.Projectile.Spit.Scattered;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class XenoScatteredSpitComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 30;

    [DataField, AutoNetworkedField]
    public float Speed = 10;

    [DataField, AutoNetworkedField]
    public EntProtoId ProjectileId = "XenoScatteredSpitProjectile";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoSpitAcid");

    [DataField, AutoNetworkedField]
    public int MaxProjectiles = 5;

    [DataField, AutoNetworkedField]
    public Angle MaxDeviation = Angle.FromDegrees(60);
}
