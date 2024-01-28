﻿using Content.Shared._CM14.Entrenching;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using static Content.Shared._CM14.Entrenching.EntrenchingToolComponentVisualLayers;

namespace Content.Client._CM14.Entrenching;

public sealed class EntrenchingToolVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EntrenchingToolComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<EntrenchingToolComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnHandleState(Entity<EntrenchingToolComponent> tool, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(tool);
    }

    private void OnAppearanceChange(Entity<EntrenchingToolComponent> tool, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(tool);
    }

    private void UpdateVisuals(Entity<EntrenchingToolComponent> tool)
    {
        if (!TryComp(tool, out SpriteComponent? sprite))
            return;

        if (_appearance.TryGetData(tool, ToggleVisuals.Toggled, out bool toggled) && toggled)
        {
            if (sprite.LayerMapTryGet(Base, out var baseLayer))
                sprite.LayerSetVisible(baseLayer, true);

            if (sprite.LayerMapTryGet(Folded, out var foldedLayer))
                sprite.LayerSetVisible(foldedLayer, false);

            if (tool.Comp.TotalLayers > 0)
            {
                if (sprite.LayerMapTryGet(Dirt, out var dirtLayer))
                {
                    sprite.LayerSetVisible(dirtLayer, true);

                    // TODO CM14 color per dirt type
                    sprite.LayerSetColor(dirtLayer, Color.FromHex("#C04000"));
                }
                else
                {
                    sprite.LayerSetVisible(dirtLayer, false);
                }
            }
            else
            {
                if (sprite.LayerMapTryGet(Dirt, out var dirtLayer))
                    sprite.LayerSetVisible(dirtLayer, false);
            }
        }
        else
        {
            if (sprite.LayerMapTryGet(Base, out var baseLayer))
                sprite.LayerSetVisible(baseLayer, false);

            if (sprite.LayerMapTryGet(Folded, out var foldedLayer))
                sprite.LayerSetVisible(foldedLayer, true);

            if (sprite.LayerMapTryGet(Dirt, out var dirtLayer))
            {
                sprite.LayerSetVisible(dirtLayer, false);
            }
        }
    }
}
