﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Xenos.Stab;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoTailStabSystem))]
public sealed partial class XenoTailStabActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan MissCooldown = TimeSpan.FromSeconds(1);
}
