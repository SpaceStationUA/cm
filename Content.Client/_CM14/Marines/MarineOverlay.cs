﻿using System.Numerics;
using Content.Shared._CM14.Marines.Squads;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using MarineComponent = Content.Shared._CM14.Marines.MarineComponent;

namespace Content.Client._CM14.Marines;

public sealed class MarineOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly MarineSystem _marine;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public MarineOverlay()
    {
        IoCManager.InjectDependencies(this);

        _marine = _entity.System<MarineSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entity.HasComponent<MarineComponent>(_players.LocalEntity))
            return;

        var handle = args.WorldHandle;

        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        var query = _entity.AllEntityQueryEnumerator<MarineComponent, StatusIconComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var status, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var bounds = status.Bounds ?? sprite.Bounds;

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var icon = _marine.GetMarineIcon(uid);
            if (icon.Icon == null)
                continue;

            var worldMatrix = Matrix3.CreateTranslation(worldPos);
            Matrix3.Multiply(scaleMatrix, worldMatrix, out var scaledWorld);
            Matrix3.Multiply(rotationMatrix, scaledWorld, out var matrix);
            handle.SetTransform(matrix);

            var texture = _sprite.Frame0(icon.Icon);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter;

            var position = new Vector2(xOffset, yOffset);
            if (icon.Background != null)
            {
                var background = _sprite.Frame0(icon.Background);
                handle.DrawTexture(background, position, icon.BackgroundColor);
            }

            handle.DrawTexture(texture, position);
        }

        handle.UseShader(null);
    }
}
