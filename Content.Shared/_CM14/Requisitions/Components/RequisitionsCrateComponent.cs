﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRequisitionsSystem))]
public sealed partial class RequisitionsCrateComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Reward = 200;
}
