﻿using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CM14.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedRequisitionsSystem))]
public sealed partial class RequisitionsRailingComponent : Component
{
    [DataField, AutoNetworkedField]
    public RequisitionsRailingMode Mode;

    [DataField, AutoNetworkedField]
    public string LoweredState = "lowered";

    [DataField, AutoNetworkedField]
    public string RaisedState = "raised";

    [DataField, AutoNetworkedField]
    public string LoweringState = "lowering";

    [DataField, AutoNetworkedField]
    public string RaisingState = "raising";

    [DataField, AutoNetworkedField]
    public TimeSpan RailingRaiseDelay = TimeSpan.FromSeconds(1);

    public object LowerAnimation = default!;

    public object RaiseAnimation = default!;
}

[Serializable, NetSerializable]
public enum RequisitionsRailingLayers
{
    Base
}

[Serializable, NetSerializable]
public enum RequisitionsRailingMode
{
    Lowered,
    Raised,
    Lowering,
    Raising
}
