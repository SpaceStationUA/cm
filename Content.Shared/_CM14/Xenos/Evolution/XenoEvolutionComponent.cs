﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CM14.Xenos.Evolution;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoEvolutionSystem))]
public sealed partial class XenoEvolutionComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> EvolvesTo = new();

    [DataField, AutoNetworkedField]
    public TimeSpan EvolutionDelay = TimeSpan.FromSeconds(3);
}
