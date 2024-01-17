﻿using Content.Shared._CM14.Vendors;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static System.StringComparison;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._CM14.Vendors;

[UsedImplicitly]
public sealed class CMAutomatedVendorBui : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;

    private CMAutomatedVendorWindow? _window;

    public CMAutomatedVendorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        _window = new CMAutomatedVendorWindow();
        _window.OnClose += Close;
        _window.Title = EntMan.GetComponentOrNull<MetaDataComponent>(Owner)?.EntityName ?? "ColMarTech Vendor";

        if (EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
        {
            for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
            {
                var section = vendor.Sections[sectionIndex];
                var uiSection = new CMAutomatedVendorSection();
                var message = new FormattedMessage();
                message.PushTag(new MarkupNode("bold", new MarkupParameter(section.Name.ToUpperInvariant()), null));
                message.AddText(section.Name.ToUpperInvariant());
                if (section.Choices is { } choices)
                    message.AddText($" (CHOOSE {choices.Amount})");
                message.Pop();

                uiSection.Label.SetMessage(message);

                for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
                {
                    var entry = section.Entries[entryIndex];
                    var uiEntry = new CMAutomatedVendorEntry();

                    if (_prototype.TryIndex(entry.Id, out var entity))
                    {
                        uiEntry.Texture.Texture = _sprite.Frame0(entity);
                        uiEntry.Panel.Button.Label.Text = entity.Name;

                        var msg = new FormattedMessage();
                        msg.AddText(entity.Description);
                        var tooltip = new Tooltip();
                        tooltip.SetMessage(msg);

                        uiEntry.TooltipLabel.ToolTip = entity.Description;
                        uiEntry.TooltipLabel.TooltipDelay = 0;
                        uiEntry.TooltipLabel.TooltipSupplier = _ => tooltip;

                        var sectionI = sectionIndex;
                        var entryI = entryIndex;
                        uiEntry.Panel.Button.OnPressed += _ => OnButtonPressed(sectionI, entryI);
                    }

                    uiSection.Entries.AddChild(uiEntry);
                }

                _window.Sections.AddChild(uiSection);
            }
        }

        _window.Search.OnTextChanged += OnSearchChanged;

        Refresh();

        _window.OpenCentered();
    }

    private void OnButtonPressed(int sectionIndex, int entryIndex)
    {
        var msg = new CMVendorVendBuiMessage(sectionIndex, entryIndex);
        SendMessage(msg);
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        if (_window == null)
            return;

        foreach (var sectionControl in _window.Sections.Children)
        {
            if (sectionControl is not CMAutomatedVendorSection section)
                continue;

            var any = false;
            foreach (var entriesControl in section.Entries.Children)
            {
                if (entriesControl is not CMAutomatedVendorEntry entry)
                    continue;

                if (string.IsNullOrWhiteSpace(args.Text))
                    entry.Visible = true;
                else
                    entry.Visible = entry.Panel.Button.Label.Text?.Contains(args.Text, OrdinalIgnoreCase) ?? false;

                if (entry.Visible)
                    any = true;
            }

            section.Visible = any;
        }
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
            return;

        var anyEntryWithPoints = false;
        var user = EntMan.GetComponentOrNull<CMVendorUserComponent>(_player.LocalEntity);
        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            var uiSection = (CMAutomatedVendorSection) _window.Sections.GetChild(sectionIndex);
            var sectionDisabled = false;
            if (section.Choices is { } choices)
            {
                if (user?.Choices.GetValueOrDefault(choices.Id) >= choices.Amount ||
                    user == null && choices.Amount <= 0)
                {
                    sectionDisabled = true;
                }
            }

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                var uiEntry = (CMAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                var disabled = sectionDisabled || entry.Amount <= 0;

                if (entry.Points != null)
                {
                    anyEntryWithPoints = true;
                    uiEntry.Amount.Text = $"{entry.Points}P";
                    if (user == null || user.Points < entry.Points)
                    {
                        disabled = true;
                    }
                }
                else
                {
                    uiEntry.Amount.Text = entry.Amount.ToString();
                }

                uiEntry.Amount.Modulate = disabled ? Color.Red : Color.White;
                uiEntry.Panel.Button.Disabled = disabled;
            }
        }

        _window.PointsLabel.Text = anyEntryWithPoints ? $"Points Remaining: {user?.Points ?? 0}" : string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.Dispose();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case CMVendorRefreshBuiMessage:
                Refresh();
                break;
        }
    }
}
