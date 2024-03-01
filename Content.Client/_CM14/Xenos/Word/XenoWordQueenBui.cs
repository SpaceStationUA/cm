﻿using Content.Shared._CM14.Xenos.Word;
using JetBrains.Annotations;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._CM14.Xenos.Word;

[UsedImplicitly]
public sealed class XenoWordQueenBui : BoundUserInterface
{
    [ViewVariables]
    private XenoWordQueenWindow? _window;

    public XenoWordQueenBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _window = new XenoWordQueenWindow();
        _window.OnClose += Close;

        _window.SendButton.OnPressed += Send;

        _window.OpenCentered();
    }

    private void Send(ButtonEventArgs args)
    {
        if (_window == null)
            return;

        var text = Rope.Collapse(_window.Text.TextRope);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var msg = new XenoWordQueenBuiMessage(text);
        SendMessage(msg);
        _window.Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.Dispose();
    }
}
