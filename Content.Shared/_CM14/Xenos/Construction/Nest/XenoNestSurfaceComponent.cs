﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Xenos.Construction.Nest;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoNestSystem))]
public sealed partial class XenoNestSurfaceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Nest = "XenoNest";

    [DataField, AutoNetworkedField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public Dictionary<Direction, EntityUid> Nests = new();
}
