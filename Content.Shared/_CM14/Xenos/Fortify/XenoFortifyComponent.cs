﻿using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Shared._CM14.Xenos.Fortify;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoFortifySystem))]
public sealed partial class XenoFortifyComponent : Component
{
    public const string FixtureId = "cm-xeno-fortify";

    [DataField, AutoNetworkedField]
    public bool Fortified;

    [DataField, AutoNetworkedField]
    public int Armor = 30;

    [DataField, AutoNetworkedField]
    public int FrontalArmor = 5;

    [DataField, AutoNetworkedField]
    public float ExplosionMultiplier = 0.4f;

    [DataField]
    public IPhysShape Shape = new PhysShapeCircle(0.49f);
}
