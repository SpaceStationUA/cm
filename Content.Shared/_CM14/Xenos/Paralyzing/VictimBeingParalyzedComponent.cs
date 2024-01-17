﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Xenos.Paralyzing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoParalyzingSlashSystem))]
public sealed partial class VictimBeingParalyzedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeAt;

    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeDuration = TimeSpan.FromSeconds(4);
}
