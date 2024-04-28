﻿using Content.Client.Message;
using Content.Shared._CM14.Dropship;
using Content.Shared.Shuttles.Systems;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client._CM14.Dropship;

[UsedImplicitly]
public sealed class DropshipNavigationBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [ViewVariables]
    private DropshipNavigationWindow? _window;

    private readonly Dictionary<DropshipButton, string> _destinations = new();
    private NetEntity? _selected;

    public DropshipNavigationBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        if (_window == null)
        {
            _window = new DropshipNavigationWindow();
            _window.OnClose += Close;
            SetHeader("Flight Controls");

            if (_entities.TryGetComponent(Owner, out TransformComponent? transform) &&
                _entities.TryGetComponent(transform.ParentUid, out MetaDataComponent? metaData))
            {
                _window.Title = $"{metaData.EntityName} {_window.Title}";
            }

            _window.OpenCentered();
        }

        _window.CancelButton.Button.OnPressed += _ =>
        {
            SetCancelLaunchDisabled(true);
            _selected = null;
            ResetDestinationButtons();
        };

        _window.LaunchButton.Button.OnPressed += _ =>
        {
            if (_selected != null)
                SendMessage(new DropshipNavigationLaunchMsg(_selected.Value));

            SetCancelLaunchDisabled(true);
            _selected = null;
            ResetDestinationButtons();
        };

        _entities.System<DropshipSystem>().Uis.Add(this);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null)
        {
            _window = new DropshipNavigationWindow();
            _window.OnClose += Close;
            SetHeader("Flight Controls");
            _window.OpenCentered();
        }

        switch (state)
        {
            case DropshipNavigationDestinationsBuiState s:
                Set(s);
                return;
            case DropshipNavigationTravellingBuiState s:
                Set(s);
                return;
        }
    }

    protected override void Dispose(bool disposing)
    {
        _entities.System<DropshipSystem>().Uis.Remove(this);

        if (disposing)
            _window?.Dispose();
    }

    private void Set(DropshipNavigationDestinationsBuiState destinations)
    {
        if (_window == null)
            return;

        SetHeader("Flight Controls");

        _window.DestinationsContainer.Visible = true;
        _window.ProgressBarContainer.Visible = false;
        _window.CancelButton.Visible = true;
        _window.LaunchButton.Visible = true;

        _window.DestinationsContainer.DisposeAllChildren();

        _destinations.Clear();
        foreach (var destination in destinations.Destinations)
        {
            var button = new DropshipButton();
            button.Text = destination.Name;
            button.BorderColor = Color.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Button.ToggleMode = true;
            button.Button.OnPressed += _ =>
            {
                SetCancelLaunchDisabled(false);
                _selected = destination.Id;
                ResetDestinationButtons();
                button.Text = $"> {destination.Name}";
            };

            _destinations[button] = destination.Name;
            _window.DestinationsContainer.AddChild(button);
        }
    }

    private void Set(DropshipNavigationTravellingBuiState travelling)
    {
        if (_window == null)
            return;

        _window.DestinationsContainer.Visible = false;
        _window.ProgressBarContainer.Visible = true;
        _window.CancelButton.Visible = false;
        _window.LaunchButton.Visible = false;
        _window.ProgressBar.Margin = new Thickness(0, 5, 0, 0);

        var time = Math.Ceiling((travelling.Time.End - _timing.CurTime).TotalSeconds);
        if (time < 0.01)
            time = 0;

        var destination = travelling.Destination;
        string Msg(string msg) => $"[color=#02E74E][bold]{msg}[/bold][/color]";

        switch (travelling.State)
        {
            case FTLState.Starting:
                SetHeader("Launch in progress"); // 10s
                _window.ProgressBarHeader.SetMarkup(Msg($"Launching in T-{time}s to {destination}"));
                break;
            case FTLState.Travelling:
                SetHeader($"In flight: {destination}"); // 100s
                _window.ProgressBarHeader.SetMarkup(Msg($"Time until destination: T-{time}s"));
                break;
            case FTLState.Arriving:
                SetHeader($"Final Approach: {destination}"); // 10s
                _window.ProgressBarHeader.SetMarkup(Msg($"Time until landing: T-{time}s"));
                break;
            case FTLState.Cooldown:
                SetHeader("Refueling in progress"); // 120s
                _window.ProgressBarHeader.SetMarkup(Msg($"Ready to launch in T-{time}s"));
                break;
            default:
                return;
        }

        var startEndTime = travelling.Time;
        _window.ProgressBar.MinValue = 0;
        _window.ProgressBar.MaxValue = (float) startEndTime.Length.TotalSeconds;
        _window.ProgressBar.SetAsRatio(1 - startEndTime.ProgressAt(_timing.CurTime));
    }

    private void SetHeader(string label)
    {
        _window?.Header.SetMarkup($"[color=#0BDC49][font size=16][bold]{label}[/bold][/font][/color]");
    }

    private void SetCancelLaunchDisabled(bool disabled)
    {
        if (_window == null)
            return;

        _window.CancelButton.Button.Disabled = disabled;
        _window.LaunchButton.Button.Disabled = disabled;
    }

    private void ResetDestinationButtons()
    {
        if (_window == null)
            return;

        foreach (var destination in _window.DestinationsContainer.Children)
        {
            if (destination is not DropshipButton button ||
                !_destinations.TryGetValue(button, out var name))
            {
                continue;
            }

            button.Text = name;
        }
    }

    public void Update()
    {
        if (_window == null || _window.Disposed)
            return;

        if (State is DropshipNavigationTravellingBuiState s)
            Set(s);
    }
}
