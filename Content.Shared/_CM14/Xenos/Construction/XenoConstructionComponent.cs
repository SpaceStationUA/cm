﻿using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Xenos.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoConstructionSystem))]
public sealed partial class XenoConstructionComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 BuildRange = 1.9;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanBuild = new();

    [DataField, AutoNetworkedField]
    public EntProtoId? BuildChoice;

    [DataField, AutoNetworkedField]
    public TimeSpan BuildDelay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public FixedPoint2 OrderConstructionRange = 1.9;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanOrderConstruction = new();

    [DataField, AutoNetworkedField]
    public EntityCoordinates? OrderingConstructionAt;

    [DataField, AutoNetworkedField]
    public TimeSpan OrderConstructionDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan OrderConstructionAddPlasmaDelay = TimeSpan.FromSeconds(3);

}
