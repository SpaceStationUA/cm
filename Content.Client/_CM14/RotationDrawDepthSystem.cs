﻿using Robust.Client.GameObjects;

namespace Content.Client._CM14;

public sealed class RotationDrawDepthSystem : EntitySystem
{
    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<RotationDrawDepthComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out _, out var rotation, out var sprite, out var xform))
        {
            // TODO CM14 this needs to support rotated viewports eventually
            var dir = xform.LocalRotation.GetCardinalDir();
            switch (dir)
            {
                case Direction.South:
                    sprite.DrawDepth = rotation.SouthDrawDepth;
                    break;
                default:
                    sprite.DrawDepth = rotation.DefaultDrawDepth;
                    break;
            }
        }
    }
}
