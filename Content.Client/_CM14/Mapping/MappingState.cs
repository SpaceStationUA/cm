﻿using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.ContextMenu.UI;
using Content.Client.Decals;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.Verbs;
using Content.Shared._CM14.Input;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static System.StringComparison;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.LineEdit;
using static Robust.Client.UserInterface.Controls.OptionButton;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client._CM14.Mapping;

public sealed class MappingState : GameplayStateBase
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntityNetworkManager _entityNetwork = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly MappingManager _mapping = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityMenuUIController _entityMenuController = default!;

    private DecalPlacementSystem _decal = default!;
    private SpriteSystem _sprite = default!;
    private TransformSystem _transform = default!;
    private VerbSystem _verbs = default!;

    private readonly ISawmill _sawmill;
    private readonly GameplayStateLoadController _loadController;
    private bool _setup;
    private readonly List<MappingPrototype> _allPrototypes = new();
    private readonly Dictionary<IPrototype, MappingPrototype> _allPrototypesDict = new();
    private readonly Dictionary<Type, Dictionary<string, MappingPrototype>> _idDict = new();
    private readonly List<MappingPrototype> _prototypes = new();
    private (TimeSpan At, MappingSpawnButton Button)? _lastClicked;
    private Control? _scrollTo;
    private bool _updatePlacement;
    private bool _updateEraseDecal;

    private MappingScreen Screen => (MappingScreen) UserInterfaceManager.ActiveScreen!;
    private MainViewport Viewport => UserInterfaceManager.ActiveScreen!.GetWidget<MainViewport>()!;

    public CursorState State { get; set; }

    public MappingState()
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _log.GetSawmill("mapping");
        _loadController = UserInterfaceManager.GetUIController<GameplayStateLoadController>();
    }

    protected override void Startup()
    {
        EnsureSetup();
        base.Startup();

        UserInterfaceManager.LoadScreen<MappingScreen>();
        _loadController.LoadScreen();
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingUnselect);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.SaveMap);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingEnablePick);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingEnableDelete);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingPick);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingRemoveDecal);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingCancelEraseDecal);
        _input.Contexts.GetContext("common").AddFunction(CMKeyFunctions.MappingOpenContextMenu);

        Screen.DecalSystem = _decal;
        Screen.Prototypes.SearchBar.OnTextChanged += OnSearch;
        Screen.Prototypes.CollapseAllButton.OnPressed += OnCollapseAll;
        Screen.Prototypes.ClearSearchButton.OnPressed += OnClearSearch;
        Screen.Prototypes.GetPrototypeData += OnGetData;
        Screen.Prototypes.SelectionChanged += OnSelected;
        Screen.Prototypes.CollapseToggled += OnCollapseToggled;
        Screen.Pick.OnPressed += OnPickPressed;
        Screen.Delete.OnPressed += OnDeletePressed;
        Screen.EntityReplaceButton.OnToggled += OnEntityReplacePressed;
        Screen.EntityPlacementMode.OnItemSelected += OnEntityPlacementSelected;
        Screen.EraseEntityButton.OnToggled += OnEraseEntityPressed;
        Screen.EraseDecalButton.OnToggled += OnEraseDecalPressed;
        _placement.PlacementChanged += OnPlacementChanged;

        CommandBinds.Builder
            .Bind(CMKeyFunctions.MappingUnselect, new PointerInputCmdHandler(HandleMappingUnselect, outsidePrediction: true))
            .Bind(CMKeyFunctions.SaveMap, new PointerInputCmdHandler(HandleSaveMap, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingEnablePick, new PointerStateInputCmdHandler(HandleEnablePick, HandleDisablePick, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingEnableDelete, new PointerStateInputCmdHandler(HandleEnableDelete, HandleDisableDelete, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingPick, new PointerInputCmdHandler(HandlePick, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingRemoveDecal, new PointerInputCmdHandler(HandleEditorCancelPlace, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingCancelEraseDecal, new PointerInputCmdHandler(HandleCancelEraseDecal, outsidePrediction: true))
            .Bind(CMKeyFunctions.MappingOpenContextMenu, new PointerInputCmdHandler(HandleOpenContextMenu, outsidePrediction: true))
            .Register<MappingState>();

        _overlays.AddOverlay(new MappingOverlay(this));

        Screen.Prototypes.UpdateVisible(_prototypes);
    }

    private bool HandleOpenContextMenu(in PointerInputCmdArgs args)
    {
        var coords = args.Coordinates.ToMap(_entityManager, _transform);
        if (_verbs.TryGetEntityMenuEntities(coords, out var entities))
            _entityMenuController.OpenRootMenu(entities);

        return true;
    }

    protected override void Shutdown()
    {
        CommandBinds.Unregister<MappingState>();

        Screen.Prototypes.SearchBar.OnTextChanged -= OnSearch;
        Screen.Prototypes.CollapseAllButton.OnPressed -= OnCollapseAll;
        Screen.Prototypes.ClearSearchButton.OnPressed -= OnClearSearch;
        Screen.Prototypes.GetPrototypeData -= OnGetData;
        Screen.Prototypes.SelectionChanged -= OnSelected;
        Screen.Prototypes.CollapseToggled -= OnCollapseToggled;
        Screen.Pick.OnPressed -= OnPickPressed;
        Screen.Delete.OnPressed -= OnDeletePressed;
        Screen.EntityReplaceButton.OnToggled -= OnEntityReplacePressed;
        Screen.EntityPlacementMode.OnItemSelected -= OnEntityPlacementSelected;
        Screen.EraseEntityButton.OnToggled -= OnEraseEntityPressed;
        Screen.EraseDecalButton.OnToggled -= OnEraseDecalPressed;
        _placement.PlacementChanged -= OnPlacementChanged;

        UserInterfaceManager.ClearWindows();
        _loadController.UnloadScreen();
        UserInterfaceManager.UnloadScreen();
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingUnselect);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.SaveMap);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingEnablePick);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingEnableDelete);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingPick);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingRemoveDecal);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingCancelEraseDecal);
        _input.Contexts.GetContext("common").RemoveFunction(CMKeyFunctions.MappingOpenContextMenu);

        _overlays.RemoveOverlay<MappingOverlay>();

        base.Shutdown();
    }

    private void EnsureSetup()
    {
        if (_setup)
            return;

        _setup = true;

        _entityMenuController = UserInterfaceManager.GetUIController<EntityMenuUIController>();

        _decal = _entityManager.System<DecalPlacementSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _verbs = _entityManager.System<VerbSystem>();

        var entities = new MappingPrototype(null, "Entities") { Children = new List<MappingPrototype>() };
        _prototypes.Add(entities);

        var mappings = new Dictionary<string, MappingPrototype>();
        foreach (var entity in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            Register(entity, entity.ID, entities);
        }

        Sort(mappings, entities);
        mappings.Clear();

        var tiles = new MappingPrototype(null, "Tiles") { Children = new List<MappingPrototype>() };
        _prototypes.Add(tiles);

        foreach (var tile in _prototypeManager.EnumeratePrototypes<ContentTileDefinition>())
        {
            Register(tile, tile.ID, tiles);
        }

        Sort(mappings, tiles);
        mappings.Clear();

        var decals = new MappingPrototype(null, "Decals") { Children = new List<MappingPrototype>() };
        _prototypes.Add(decals);

        foreach (var decal in _prototypeManager.EnumeratePrototypes<DecalPrototype>())
        {
            Register(decal, decal.ID, decals);
        }

        Sort(mappings, decals);
        mappings.Clear();

        foreach (var prototype in _allPrototypes)
        {
            prototype.Children?.Sort((a, b) => string.Compare(a.Name, b.Name, OrdinalIgnoreCase));
        }
    }

    private void Sort(Dictionary<string, MappingPrototype> prototypes, MappingPrototype topLevel)
    {
        static int Compare(MappingPrototype a, MappingPrototype b)
        {
            return string.Compare(a.Name, b.Name, OrdinalIgnoreCase);
        }

        topLevel.Children ??= new List<MappingPrototype>();

        foreach (var prototype in prototypes.Values)
        {
            if (prototype.Parents == null && prototype != topLevel)
            {
                prototype.Parents = new List<MappingPrototype> { topLevel };
                topLevel.Children.Add(prototype);
            }

            prototype.Parents?.Sort(Compare);
            prototype.Children?.Sort(Compare);
        }

        topLevel.Children.Sort(Compare);
    }

    private MappingPrototype? Register<T>(T? prototype, string id, MappingPrototype topLevel) where T : class, IPrototype, IInheritingPrototype
    {
        {
            if (prototype == null &&
                _prototypeManager.TryIndex(id, out prototype) &&
                prototype is EntityPrototype entity)
            {
                if (entity.HideSpawnMenu || entity.Abstract)
                    prototype = null;
            }
        }

        if (prototype == null)
        {
            if (!_prototypeManager.TryGetMapping(typeof(T), id, out var node))
            {
                _sawmill.Error($"No {nameof(T)} found with id {id}");
                return null;
            }

            var ids = _idDict.GetOrNew(typeof(T));
            if (ids.TryGetValue(id, out var mapping))
            {
                return mapping;
            }
            else
            {
                var name = node.TryGet("name", out ValueDataNode? nameNode)
                    ? Loc.GetString(nameNode.Value)
                    : id;

                if (node.TryGet("suffix", out ValueDataNode? suffix))
                    name = $"{name} [{suffix.Value}]";

                mapping = new MappingPrototype(prototype, name);
                _allPrototypes.Add(mapping);
                ids.Add(id, mapping);

                if (node.TryGet("parent", out ValueDataNode? parentValue))
                {
                    var parent = Register<T>(null, parentValue.Value, topLevel);

                    if (parent != null)
                    {
                        mapping.Parents ??= new List<MappingPrototype>();
                        mapping.Parents.Add(parent);
                        parent.Children ??= new List<MappingPrototype>();
                        parent.Children.Add(mapping);
                    }
                }
                else if (node.TryGet("parent", out SequenceDataNode? parentSequence))
                {
                    foreach (var parentNode in parentSequence.Cast<ValueDataNode>())
                    {
                        var parent = Register<T>(null, parentNode.Value, topLevel);

                        if (parent != null)
                        {
                            mapping.Parents ??= new List<MappingPrototype>();
                            mapping.Parents.Add(parent);
                            parent.Children ??= new List<MappingPrototype>();
                            parent.Children.Add(mapping);
                        }
                    }
                }
                else
                {
                    topLevel.Children ??= new List<MappingPrototype>();
                    topLevel.Children.Add(mapping);
                    mapping.Parents ??= new List<MappingPrototype>();
                    mapping.Parents.Add(topLevel);
                }

                return mapping;
            }
        }
        else
        {
            var ids = _idDict.GetOrNew(typeof(T));
            if (ids.TryGetValue(id, out var mapping))
            {
                return mapping;
            }
            else
            {
                var entity = prototype as EntityPrototype;
                var name = entity == null ? prototype.ID : Loc.GetString(entity.Name);

                if (!string.IsNullOrWhiteSpace(entity?.EditorSuffix))
                    name = $"{name} [{entity.EditorSuffix}]";

                mapping = new MappingPrototype(prototype, name);
                _allPrototypes.Add(mapping);
                _allPrototypesDict.Add(prototype, mapping);
                ids.Add(prototype.ID, mapping);
            }

            if (prototype.Parents == null)
            {
                topLevel.Children ??= new List<MappingPrototype>();
                topLevel.Children.Add(mapping);
                mapping.Parents ??= new List<MappingPrototype>();
                mapping.Parents.Add(topLevel);
                return mapping;
            }

            foreach (var parentId in prototype.Parents)
            {
                var parent = Register<T>(null, parentId, topLevel);

                if (parent != null)
                {
                    mapping.Parents ??= new List<MappingPrototype>();
                    mapping.Parents.Add(parent);
                    parent.Children ??= new List<MappingPrototype>();
                    parent.Children.Add(mapping);
                }
            }

            return mapping;
        }
    }

    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        _updatePlacement = true;
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
        if (args.Viewport == null)
            base.OnKeyBindStateChanged(new ViewportBoundKeyEventArgs(args.KeyEventArgs, Viewport.Viewport));
        else
            base.OnKeyBindStateChanged(args);
    }

    private void OnSearch(LineEditEventArgs args)
    {
        if (string.IsNullOrEmpty(args.Text))
        {
            Screen.Prototypes.PrototypeList.Visible = true;
            Screen.Prototypes.SearchList.Visible = false;
            return;
        }

        var matches = new List<MappingPrototype>();
        foreach (var prototype in _allPrototypes)
        {
            if (prototype.Name.Contains(args.Text, OrdinalIgnoreCase))
                matches.Add(prototype);
        }

        matches.Sort(static (a, b) => string.Compare(a.Name, b.Name, OrdinalIgnoreCase));

        Screen.Prototypes.PrototypeList.Visible = false;
        Screen.Prototypes.SearchList.Visible = true;
        Screen.Prototypes.Search(matches);
    }

    private void OnCollapseAll(ButtonEventArgs args)
    {
        foreach (var child in Screen.Prototypes.PrototypeList.Children)
        {
            if (child is not MappingSpawnButton button)
                continue;

            Collapse(button);
        }

        Screen.Prototypes.ScrollContainer.SetScrollValue(new Vector2(0, 0));
    }

    private void OnClearSearch(ButtonEventArgs obj)
    {
        Screen.Prototypes.SearchBar.Text = string.Empty;
        OnSearch(new LineEditEventArgs(Screen.Prototypes.SearchBar, string.Empty));
    }

    private void OnGetData(IPrototype prototype, List<Texture> textures)
    {
        switch (prototype)
        {
            case EntityPrototype entity:
                textures.AddRange(SpriteComponent.GetPrototypeTextures(entity, _resources).Select(t => t.Default));
                break;
            case DecalPrototype decal:
                textures.Add(_sprite.Frame0(decal.Sprite));
                break;
            case ContentTileDefinition tile:
                if (tile.Sprite?.ToString() is { } sprite)
                    textures.Add(_resources.GetResource<TextureResource>(sprite).Texture);
                break;
        }
    }

    private void OnSelected(MappingPrototype mapping)
    {
        if (mapping.Prototype == null)
            return;

        var chain = new Stack<MappingPrototype>();
        chain.Push(mapping);

        var parent = mapping.Parents?.FirstOrDefault();
        while (parent != null)
        {
            chain.Push(parent);
            parent = parent.Parents?.FirstOrDefault();
        }

        _lastClicked = null;

        Control? last = null;
        var children = Screen.Prototypes.PrototypeList.Children;
        foreach (var prototype in chain)
        {
            foreach (var child in children)
            {
                if (child is MappingSpawnButton button &&
                    button.Prototype == prototype)
                {
                    UnCollapse(button);
                    OnSelected(button, prototype.Prototype);
                    children = button.ChildrenPrototypes.Children;
                    last = child;
                    break;
                }
            }
        }

        if (last != null && Screen.Prototypes.PrototypeList.Visible)
            _scrollTo = last;
    }

    private void OnSelected(MappingSpawnButton button, IPrototype? prototype)
    {
        var time = _timing.CurTime;
        if (prototype is DecalPrototype)
            Screen.SelectDecal(prototype.ID);

        if (_lastClicked is { } lastClicked &&
            lastClicked.Button == button &&
            lastClicked.At > time - TimeSpan.FromSeconds(0.333) &&
            string.IsNullOrEmpty(Screen.Prototypes.SearchBar.Text))
        {
            button.CollapseButton.Pressed = !button.CollapseButton.Pressed;
            ToggleCollapse(button);
            button.Button.Pressed = true;
            Screen.Prototypes.Selected = button;
            _lastClicked = null;
            return;
        }

        _lastClicked = (time, button);

        if (button.Prototype == null)
            return;

        if (Screen.Prototypes.Selected is { } oldButton &&
            oldButton != button)
        {
            Deselect();
        }

        Screen.EntityContainer.Visible = false;
        Screen.DecalContainer.Visible = false;

        switch (prototype)
        {
            case EntityPrototype entity:
            {
                var placement = new PlacementInformation
                {
                    PlacementOption = entity.PlacementMode,
                    EntityType = entity.ID,
                    IsTile = false
                };

                Screen.EntityContainer.Visible = true;
                _decal.SetActive(false);
                _placement.BeginPlacing(placement);
                break;
            }
            case DecalPrototype decal:
                _placement.Clear();

                _decal.SetActive(true);
                _decal.UpdateDecalInfo(decal.ID, Color.White, 0, true, 0, false);
                Screen.DecalContainer.Visible = true;
                break;
            case ContentTileDefinition tile:
            {
                var placement = new PlacementInformation
                {
                    PlacementOption = "AlignTileAny",
                    TileType = tile.TileId,
                    IsTile = true
                };

                _decal.SetActive(false);
                _placement.BeginPlacing(placement);
                break;
            }
            default:
                _placement.Clear();
                break;
        }

        Screen.Prototypes.Selected = button;
        button.Button.Pressed = true;
    }

    private void Deselect()
    {
        if (Screen.Prototypes.Selected is { } selected)
        {
            selected.Button.Pressed = false;
            Screen.Prototypes.Selected = null;

            if (selected.Prototype?.Prototype is DecalPrototype)
            {
                _decal.SetActive(false);
                Screen.DecalContainer.Visible = false;
            }
        }
    }

    private void OnCollapseToggled(MappingSpawnButton button, ButtonToggledEventArgs args)
    {
        ToggleCollapse(button);
    }

    private void OnPickPressed(ButtonEventArgs args)
    {
        if (args.Button.Pressed)
            EnablePick();
        else
            DisablePick();
    }

    private void OnDeletePressed(ButtonEventArgs obj)
    {

    }

    private void OnEntityReplacePressed(ButtonToggledEventArgs args)
    {
        _placement.Replacement = args.Pressed;
    }

    private void OnEntityPlacementSelected(ItemSelectedEventArgs args)
    {
        Screen.EntityPlacementMode.SelectId(args.Id);

        if (_placement.CurrentMode != null)
        {
            var placement = new PlacementInformation
            {
                PlacementOption = EntitySpawnWindow.InitOpts[args.Id],
                EntityType = _placement.CurrentPermission!.EntityType,
                TileType = _placement.CurrentPermission.TileType,
                Range = 2,
                IsTile = _placement.CurrentPermission.IsTile,
            };

            _placement.BeginPlacing(placement);
        }
    }

    private void OnEraseEntityPressed(ButtonEventArgs args)
    {
        if (args.Button.Pressed == _placement.Eraser)
            return;

        if (args.Button.Pressed)
            EnableEraser();
        else
            DisableEraser();
    }

    private void OnEraseDecalPressed(ButtonToggledEventArgs args)
    {
        _placement.Clear();
        Deselect();
        Screen.EraseEntityButton.Pressed = false;
        _updateEraseDecal = true;
    }

    private void EnableEraser()
    {
        if (_placement.Eraser)
            return;

        _placement.Clear();
        _placement.ToggleEraser();
        Screen.EntityPlacementMode.Disabled = true;
        Screen.EraseDecalButton.Pressed = false;
        Deselect();
    }

    private void DisableEraser()
    {
        if (!_placement.Eraser)
            return;

        _placement.ToggleEraser();
        Screen.EntityPlacementMode.Disabled = false;
    }

    private void EnablePick()
    {
        Screen.UnPressActionsExcept(Screen.Pick);
        State = CursorState.Pick;
    }

    private void DisablePick()
    {
        Screen.Pick.Pressed = false;
        State = CursorState.None;
    }

    private void EnableDelete()
    {
        Screen.UnPressActionsExcept(Screen.Delete);
        State = CursorState.Delete;
        EnableEraser();
    }

    private void DisableDelete()
    {
        Screen.Delete.Pressed = false;
        State = CursorState.None;
        DisableEraser();
    }

    private bool HandleMappingUnselect(in PointerInputCmdArgs args)
    {
        if (Screen.Prototypes.Selected is not { Prototype.Prototype: DecalPrototype })
            return false;

        Deselect();
        return true;
    }

    private bool HandleSaveMap(in PointerInputCmdArgs args)
    {
#if FULL_RELEASE
        return false;
#endif
        if (!_admin.IsAdmin(true) || !_admin.HasFlag(AdminFlags.Host))
            return false;

        SaveMap();
        return true;
    }

    private bool HandleEnablePick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        EnablePick();
        return true;
    }

    private bool HandleDisablePick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        DisablePick();
        return true;
    }

    private bool HandleEnableDelete(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        EnableDelete();
        return true;
    }

    private bool HandleDisableDelete(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        DisableDelete();
        return true;
    }

    private bool HandlePick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (State != CursorState.Pick)
            return false;

        if (uid == EntityUid.Invalid ||
            _entityManager.GetComponentOrNull<MetaDataComponent>(uid) is not { EntityPrototype: { } prototype } ||
            !_allPrototypesDict.TryGetValue(prototype, out var button))
        {
            // we always block other input handlers if pick mode is enabled
            // this makes you not accidentally place something in space because you
            // miss-clicked while holding down the pick hotkey
            return true;
        }

        OnSelected(button);
        return true;
    }

    private bool HandleEditorCancelPlace(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (!Screen.EraseDecalButton.Pressed)
            return false;

        _entityNetwork.SendSystemNetworkMessage(new RequestDecalRemovalEvent(_entityManager.GetNetCoordinates(coords)));
        return true;
    }

    private bool HandleCancelEraseDecal(in PointerInputCmdArgs args)
    {
        if (!Screen.EraseDecalButton.Pressed)
            return false;

        Screen.EraseDecalButton.Pressed = false;
        return true;
    }

    private async void SaveMap()
    {
        await _mapping.SaveMap();
    }

    private void ToggleCollapse(MappingSpawnButton button)
    {
        if (button.CollapseButton.Pressed)
        {
            if (button.Prototype?.Children != null)
            {
                foreach (var child in button.Prototype.Children)
                {
                    Screen.Prototypes.Insert(button.ChildrenPrototypes, child, true);
                }
            }

            button.CollapseButton.Label.Text = "▼";
        }
        else
        {
            button.ChildrenPrototypes.DisposeAllChildren();
            button.CollapseButton.Label.Text = "▶";
        }
    }

    private void Collapse(MappingSpawnButton button)
    {
        if (!button.CollapseButton.Pressed)
            return;

        button.CollapseButton.Pressed = false;
        ToggleCollapse(button);
    }


    private void UnCollapse(MappingSpawnButton button)
    {
        if (button.CollapseButton.Pressed)
            return;

        button.CollapseButton.Pressed = true;
        ToggleCollapse(button);
    }

    public EntityUid? GetHoveredEntity()
    {
        if (UserInterfaceManager.CurrentlyHovered is not IViewportControl viewport ||
            _input.MouseScreenPosition is not { IsValid: true } position)
        {
            return null;
        }

        var mapPos = viewport.PixelToMap(position.Position);
        return GetClickedEntity(mapPos);
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        if (_updatePlacement)
        {
            _updatePlacement = false;

            if (!_placement.IsActive && _decal.GetActiveDecal().Decal == null)
                Deselect();

            Screen.EraseEntityButton.Pressed = _placement.Eraser;
            Screen.EraseDecalButton.Pressed = _updateEraseDecal;
            Screen.EntityPlacementMode.Disabled = _placement.Eraser;

            _updateEraseDecal = false;
        }

        if (_scrollTo is not { } scrollTo)
            return;

        // this is not ideal but we wait until the control's height is computed to use
        // its position to scroll to
        if (scrollTo.Height > 0 && Screen.Prototypes.PrototypeList.Visible)
        {
            var y = scrollTo.GlobalPosition.Y - Screen.Prototypes.ScrollContainer.Height / 2 + scrollTo.Height;
            var scroll = Screen.Prototypes.ScrollContainer;
            scroll.SetScrollValue(scroll.GetScrollValue() + new Vector2(0, y));
            _scrollTo = null;
        }
    }


    // TODO CM14 this doesn't handle pressing down multiple state hotkeys at the moment
    public enum CursorState
    {
        None,
        Pick,
        Delete
    }
}
