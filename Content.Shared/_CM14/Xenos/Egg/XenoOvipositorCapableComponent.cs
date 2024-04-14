﻿using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Xenos.Egg;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoOvipositorCapableComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AttachedState = "normal";

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public EntProtoId Spawn = "XenoEgg";

    [DataField, AutoNetworkedField]
    public Vector2 Offset = new(-1, -1);
}
