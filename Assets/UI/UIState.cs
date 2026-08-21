// Tracks which full-panel UI is currently open, so opening one closes any other.
// Add a case here whenever a new full-panel UI is introduced.
public enum UIPanel { None, Build, Wiring, Code }

public static class UIState
{
    public static UIPanel Open { get; private set; } = UIPanel.None;

    // Toggles `panel` open/closed against whatever else is currently open. Returns the
    // new visibility for `panel` (true = now open), matching typical toggle-key usage.
    public static bool Toggle(UIPanel panel)
    {
        bool nowOpen = Open != panel;
        Open = nowOpen ? panel : UIPanel.None;
        return nowOpen;
    }

    public static void Close(UIPanel panel)
    {
        if (Open == panel) Open = UIPanel.None;
    }
}
