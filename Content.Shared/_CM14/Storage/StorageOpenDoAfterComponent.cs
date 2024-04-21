﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Storage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageOpenDoAfterComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(2);
}
