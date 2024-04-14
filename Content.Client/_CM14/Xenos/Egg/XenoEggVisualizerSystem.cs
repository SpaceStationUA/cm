﻿using Content.Shared._CM14.Xenos.Egg;
using Robust.Client.GameObjects;

namespace Content.Client._CM14.Xenos.Egg;

public sealed class XenoEggVisualizerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEggComponent, ComponentStartup>(SetVisuals);
        SubscribeLocalEvent<XenoEggComponent, XenoEggStateChangedEvent>(SetVisuals);
    }

    private void SetVisuals<T>(Entity<XenoEggComponent> ent, ref T args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var state = ent.Comp.State switch
        {
            XenoEggState.Item => ent.Comp.ItemState,
            XenoEggState.Growing => ent.Comp.GrowingState,
            XenoEggState.Grown => ent.Comp.GrownState,
            XenoEggState.Opened => ent.Comp.OpenedState,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(state))
            return;

        sprite.LayerSetState(XenoEggLayers.Base, state);
    }
}
