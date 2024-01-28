﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Entrenching;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(EntrenchingToolSystem))]
public sealed partial class EmptySandbagComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Filled = "CMSandbagFull";
}
