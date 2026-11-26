using System.Windows.Input;

namespace Drawski;

// Mouse..Ellipse share their names with Tool, OverlayWindow.Execute relies on that. Keep ShortcutInfo.ALL in this order
public enum Shortcut
{
    ToggleDraw, ClearAll, HideInk,
    Mouse, Pen, Highlighter, Laser, Eraser, Line, Arrow, Rectangle, Ellipse,
    Color1, Color2, Color3, Color4, Color5, Color6, SizeDown, SizeUp,
    Whiteboard, Clear, Undo, Redo, Save, Open, Export,
}

public record ShortcutInfo(Shortcut Id, string Label, Hotkey Default, bool IsGlobal)
{
    const ModifierKeys CTRL = ModifierKeys.Control, CTRL_ALT = ModifierKeys.Control | ModifierKeys.Alt, NONE = ModifierKeys.None;

    public static readonly ShortcutInfo[] ALL =
    {
        new(Shortcut.ToggleDraw, "Switch between drawing and mouse", new(Key.D, CTRL_ALT), true),
        new(Shortcut.ClearAll, "Clear all ink", new(Key.C, CTRL_ALT), true),
        new(Shortcut.HideInk, "Hide / show ink", new(Key.H, CTRL_ALT), true),

        new(Shortcut.Mouse, "Mouse mode", new(Key.Escape, NONE), false),
        new(Shortcut.Pen, "Pen", new(Key.P, NONE), false),
        new(Shortcut.Highlighter, "Highlighter", new(Key.H, NONE), false),
        new(Shortcut.Laser, "Laser", new(Key.X, NONE), false),
        new(Shortcut.Eraser, "Eraser", new(Key.E, NONE), false),
        new(Shortcut.Line, "Line", new(Key.L, NONE), false),
        new(Shortcut.Arrow, "Arrow", new(Key.A, NONE), false),
        new(Shortcut.Rectangle, "Rectangle", new(Key.R, NONE), false),
        new(Shortcut.Ellipse, "Ellipse", new(Key.O, NONE), false),

        new(Shortcut.Color1, "Colour 1", new(Key.D1, NONE), false),
        new(Shortcut.Color2, "Colour 2", new(Key.D2, NONE), false),
        new(Shortcut.Color3, "Colour 3", new(Key.D3, NONE), false),
        new(Shortcut.Color4, "Colour 4", new(Key.D4, NONE), false),
        new(Shortcut.Color5, "Colour 5", new(Key.D5, NONE), false),
        new(Shortcut.Color6, "Colour 6", new(Key.D6, NONE), false),
        new(Shortcut.SizeDown, "Thinner", new(Key.OemOpenBrackets, NONE), false),
        new(Shortcut.SizeUp, "Thicker", new(Key.OemCloseBrackets, NONE), false),

        new(Shortcut.Whiteboard, "Whiteboard", new(Key.B, NONE), false),
        new(Shortcut.Clear, "Clear", new(Key.Delete, NONE), false),
        new(Shortcut.Undo, "Undo", new(Key.Z, CTRL), false),
        new(Shortcut.Redo, "Redo", new(Key.Y, CTRL), false),
        new(Shortcut.Save, "Save", new(Key.S, CTRL), false),
        new(Shortcut.Open, "Open PDF or ink file", new(Key.O, CTRL), false),
        new(Shortcut.Export, "Export PDF with ink", new(Key.E, CTRL), false),
    };

    public static ShortcutInfo Of(Shortcut id) => ALL[(int)id];
}
