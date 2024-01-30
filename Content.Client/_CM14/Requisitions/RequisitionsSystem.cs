﻿using Content.Shared._CM14.Requisitions;
using Content.Shared._CM14.Requisitions.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._CM14.Requisitions;

public sealed class RequisitionsSystem : SharedRequisitionsSystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private const string AnimationKey = "cm_requisitions_animation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsElevatorComponent, ComponentStartup>(OnElevatorStartup);
        SubscribeLocalEvent<RequisitionsElevatorComponent, AfterAutoHandleStateEvent>(OnElevatorHandleState);
        SubscribeLocalEvent<RequisitionsGearComponent, AfterAutoHandleStateEvent>(OnGearHandleState);
        SubscribeLocalEvent<RequisitionsRailingComponent, ComponentStartup>(OnRailingStartup);
        SubscribeLocalEvent<RequisitionsRailingComponent, AfterAutoHandleStateEvent>(OnRailingHandleState);
    }

    private void OnElevatorStartup(Entity<RequisitionsElevatorComponent> elevator, ref ComponentStartup args)
    {
        var lowerAnimation = new Animation { Length = TimeSpan.FromSeconds(2.1f) };
        elevator.Comp.LoweringAnimation = lowerAnimation;
        lowerAnimation.AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = RequisitionsElevatorLayers.Base,
            KeyFrames =
            {
                new AnimationTrackSpriteFlick.KeyFrame(elevator.Comp.LoweringState, 0)
            }
        });

        var raiseAnimation = new Animation { Length = TimeSpan.FromSeconds(2.1f) };
        elevator.Comp.RaisingAnimation = raiseAnimation;
        raiseAnimation.AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = RequisitionsElevatorLayers.Base,
            KeyFrames =
            {
                new AnimationTrackSpriteFlick.KeyFrame(elevator.Comp.RaisingState, 0)
            }
        });
    }

    private void OnElevatorHandleState(Entity<RequisitionsElevatorComponent> elevator, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(elevator, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(RequisitionsElevatorLayers.Base, out var layer))
        {
            return;
        }

        if (elevator.Comp.Mode != RequisitionsElevatorMode.Preparing)
            _animation.Stop(elevator, AnimationKey);

        switch (elevator.Comp.Mode)
        {
            case RequisitionsElevatorMode.Lowered:
                sprite.LayerSetState(layer, elevator.Comp.LoweredState);
                break;
            case RequisitionsElevatorMode.Raised:
                sprite.LayerSetState(layer, elevator.Comp.RaisedState);
                break;
            case RequisitionsElevatorMode.Lowering:
                _animation.Play(elevator, (Animation) elevator.Comp.LoweringAnimation, AnimationKey);
                break;
            case RequisitionsElevatorMode.Raising:
                _animation.Play(elevator, (Animation) elevator.Comp.RaisingAnimation, AnimationKey);
                break;
        }
    }

    private void OnGearHandleState(Entity<RequisitionsGearComponent> gear, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(gear, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(RequisitionsGearLayers.Base, out var layer))
        {
            return;
        }

        var state = gear.Comp.Mode switch
        {
            RequisitionsGearMode.Static => gear.Comp.StaticState,
            RequisitionsGearMode.Moving => gear.Comp.MovingState,
            _ => gear.Comp.StaticState
        };

        sprite.LayerSetState(layer, state);
    }

    private void OnRailingStartup(Entity<RequisitionsRailingComponent> railing, ref ComponentStartup args)
    {
        railing.Comp.LowerAnimation = new Animation { Length = TimeSpan.FromSeconds(1.2f) };
        ((Animation) railing.Comp.LowerAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = RequisitionsRailingLayers.Base,
            KeyFrames =
            {
                new AnimationTrackSpriteFlick.KeyFrame(railing.Comp.LoweringState, 0)
            }
        });

        railing.Comp.RaiseAnimation = new Animation { Length = TimeSpan.FromSeconds(1.2f) };
        ((Animation) railing.Comp.RaiseAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = RequisitionsRailingLayers.Base,
            KeyFrames =
            {
                new AnimationTrackSpriteFlick.KeyFrame(railing.Comp.RaisingState, 0)
            }
        });
    }

    private void OnRailingHandleState(Entity<RequisitionsRailingComponent> railing, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(railing, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(RequisitionsRailingLayers.Base, out var layer))
        {
            return;
        }

        _animation.Stop(railing, AnimationKey);
        switch (railing.Comp.Mode)
        {
            case RequisitionsRailingMode.Lowered:
                sprite.LayerSetState(layer, railing.Comp.LoweredState);
                break;
            case RequisitionsRailingMode.Raised:
                sprite.LayerSetState(layer, railing.Comp.RaisedState);
                break;
            case RequisitionsRailingMode.Lowering:
                _animation.Play(railing, (Animation) railing.Comp.LowerAnimation, AnimationKey);
                break;
            case RequisitionsRailingMode.Raising:
                _animation.Play(railing, (Animation) railing.Comp.RaiseAnimation, AnimationKey);
                break;
        }
    }

    private void Animate(SpriteComponent sprite, object layerKey)
    {
        if (!sprite.LayerExists(layerKey) ||
            sprite[layerKey] is not Layer layer ||
            layer.ActualState?.DelayCount is not { } delays)
        {
            return;
        }

        if (layer.AnimationFrame >= delays - 1)
            layer.AutoAnimated = false;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var elevatorQuery = EntityQueryEnumerator<RequisitionsElevatorComponent, SpriteComponent>();
        while (elevatorQuery.MoveNext(out var elevator, out var sprite))
        {
            Animate(sprite, elevator.Mode);
        }

        var railingQuery = EntityQueryEnumerator<RequisitionsRailingComponent, SpriteComponent>();
        while (railingQuery.MoveNext(out var gear, out var sprite))
        {
            Animate(sprite, gear.Mode);
        }
    }
}
