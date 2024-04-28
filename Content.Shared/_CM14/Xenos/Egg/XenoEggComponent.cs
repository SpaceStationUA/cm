﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CM14.Xenos.Egg;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class XenoEggComponent : Component
{
    [DataField, AutoNetworkedField]
    public XenoEggState State;

    [DataField, AutoNetworkedField]
    public TimeSpan MinTime = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan MaxTime = TimeSpan.FromSeconds(15);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? GrowAt;

    [DataField, AutoNetworkedField]
    public string ItemState = "egg_item";

    [DataField, AutoNetworkedField]
    public string GrowingState = "egg_growing";

    [DataField, AutoNetworkedField]
    public string GrownState = "egg";

    [DataField, AutoNetworkedField]
    public string OpenedState = "egg_opened";

    [DataField, AutoNetworkedField]
    public EntProtoId Spawn = "CMXenoHugger";
}

[Serializable, NetSerializable]
public enum XenoEggState
{
    Item,
    Growing,
    Grown,
    Opened
}

[Serializable, NetSerializable]
public enum XenoEggLayers
{
    Base
}
