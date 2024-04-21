﻿using Content.Client.Weapons.Melee;
using Content.Shared._CM14.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;

namespace Content.Client._CM14.Weapons.Melee;

public sealed class CMMeleeWeaponSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MeleeWeaponSystem _melee = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMXenoWideSwing,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (session?.AttachedEntity != null)
                        TryPrimaryHeavyAttack();
                }, handle: false))
            .Register<CMMeleeWeaponSystem>();
    }

    private void TryPrimaryHeavyAttack()
    {
        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        var grid = _mapManager.TryFindGridAt(mousePos, out var gridUid, out _)
            ? gridUid
            : _mapManager.GetMapEntityId(mousePos.MapId);

        if (grid == EntityUid.Invalid)
            return;

        var coordinates = EntityCoordinates.FromMap(grid, mousePos, _transform, EntityManager);

        if (_player.LocalEntity is not { } entity)
            return;

        if (!_melee.TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (weapon.WidePrimary)
                _melee.ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
    }
}
