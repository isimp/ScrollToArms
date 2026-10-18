namespace ScrollToArms
{
    /// <summary>What the mouse wheel does when the modifier is not held.</summary>
    public enum PlainScroll
    {
        Zoom,
        Hotbar
    }

    /// <summary>When a slot picked with the wheel is put in the player's hand.</summary>
    public enum Commit
    {
        OnRelease,
        AfterPause
    }
}
