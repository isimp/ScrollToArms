using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// What the line above the hotbar says the wheel does, and the message when the wheel takes out a
/// tool that changes it. The line speaks only when it is worth knowing: while the modifier is held,
/// while a build tool takes the plain wheel for rotating the piece with PlainScroll on Hotbar,
/// while the rotation could not be redirected, and while a tool on StepAsideTools keeps the wheel.
/// Keys are named as the player reads them.
/// </summary>
public class WheelWordsTests
{
    private static WheelState State(bool tool = false, bool held = false, bool placing = false, bool placementReady = true, PlainScroll plain = PlainScroll.Zoom)
        => WheelWords.State(tool, held, placing, placementReady, plain);

    [Fact]
    public void InOrdinaryPlayTheLineSaysNothing()
    {
        Assert.Equal(WheelState.None, State());
        Assert.Equal(WheelState.None, State(plain: PlainScroll.Hotbar));
        Assert.Null(WheelWords.Line(WheelState.None, "Left Alt", "Z", flipBack: true, tool: null));
    }

    [Fact]
    public void HoldingTheModifierSaysWhatTheWheelDoesNow()
    {
        Assert.Equal(WheelState.ModifierPicks, State(held: true));
        Assert.Equal(WheelState.ModifierZooms, State(held: true, plain: PlainScroll.Hotbar));
        Assert.Equal("Wheel: hotbar", WheelWords.Line(WheelState.ModifierPicks, "Left Alt", "Z", true, null));
        Assert.Equal("Wheel: zoom", WheelWords.Line(WheelState.ModifierZooms, "Left Alt", "Z", true, null));
    }

    [Fact]
    public void InBuildModeTheModifierPicksWhateverPlainScrollSays()
    {
        Assert.Equal(WheelState.ModifierPicks, State(held: true, placing: true, plain: PlainScroll.Hotbar));
    }

    [Fact]
    public void ABuildToolWithPlainScrollOnHotbarSaysThePlainWheelRotates()
    {
        Assert.Equal(WheelState.BuildRotates, State(placing: true, plain: PlainScroll.Hotbar));
        Assert.Equal(WheelState.None, State(placing: true, plain: PlainScroll.Zoom));
        Assert.Equal("Wheel: rotate  |  [Left Alt] + wheel: hotbar", WheelWords.Line(WheelState.BuildRotates, "Left Alt", "Z", true, null));
    }

    [Fact]
    public void WithoutTheRotationRedirectBuildModeSaysTheWheelRotatesAndHowToGetOut()
    {
        Assert.Equal(WheelState.BuildStuck, State(placing: true, placementReady: false, held: true));
        Assert.Equal("Wheel: rotate  |  [Z] put away  |  tap [Left Alt] back", WheelWords.Line(WheelState.BuildStuck, "Left Alt", "Z", true, null));
    }

    [Fact]
    public void AToolThatKeepsTheWheelSaysSoBeforeAnythingElse()
    {
        Assert.Equal(WheelState.ToolKeeps, State(tool: true, held: true, placing: true));
        Assert.Equal("Blueprint Rune keeps the wheel  |  [Z] put away  |  tap [Left Alt] back",
            WheelWords.Line(WheelState.ToolKeeps, "Left Alt", "Z", true, "Blueprint Rune"));
    }

    [Fact]
    public void AnUnboundHideKeyAndFlipBackOffLeaveTheirPartsOut()
    {
        Assert.Equal("Blueprint Rune keeps the wheel", WheelWords.Line(WheelState.ToolKeeps, "Left Alt", "", false, "Blueprint Rune"));
        Assert.Equal("Wheel: rotate", WheelWords.Line(WheelState.BuildStuck, "Left Alt", null, false, null));
    }

    [Fact]
    public void AToolOnStepAsideToolsGetsTheKeepsTheWheelMessage()
    {
        Assert.Equal(HintKind.KeepsTheWheel, WheelWords.HintKindOf(listed: true, buildTool: false, placementReady: true, PlainScroll.Zoom));
    }

    [Fact]
    public void ABuildToolKeepsTheWheelWhenTheRotationCouldNotBeRedirected()
    {
        Assert.Equal(HintKind.KeepsTheWheel, WheelWords.HintKindOf(listed: false, buildTool: true, placementReady: false, PlainScroll.Zoom));
    }

    [Fact]
    public void ABuildToolWithPlainScrollOnHotbarSaysThePlainWheelNowRotates()
    {
        Assert.Equal(HintKind.WheelRotates, WheelWords.HintKindOf(listed: false, buildTool: true, placementReady: true, PlainScroll.Hotbar));
    }

    [Fact]
    public void ABuildToolWithPlainScrollOnZoomNeedsNoMessageAsTheWheelWorksAsInTheGame()
    {
        Assert.Equal(HintKind.None, WheelWords.HintKindOf(listed: false, buildTool: true, placementReady: true, PlainScroll.Zoom));
        Assert.Equal(HintKind.None, WheelWords.HintKindOf(listed: false, buildTool: false, placementReady: true, PlainScroll.Hotbar));
    }

    [Fact]
    public void TheRotatesMessageNamesTheToolAndTheModifier()
    {
        Assert.Equal("Hammer is out: the wheel rotates the piece. [<color=yellow><b>Left Alt</b></color>] with the wheel switches.",
            WheelWords.Hint(HintKind.WheelRotates, "Hammer", "Left Alt", "Z", flipBack: true));
    }

    [Fact]
    public void TheKeepsMessageNamesTheHideKeyAndTheTapBack()
    {
        Assert.Equal("Plan Hammer keeps the wheel. [<color=yellow><b>Z</b></color>] puts it away. A tap on [<color=yellow><b>Left Alt</b></color>] goes back.",
            WheelWords.Hint(HintKind.KeepsTheWheel, "Plan Hammer", "Left Alt", "Z", flipBack: true));
    }

    [Fact]
    public void WithoutAHideKeyTheKeepsMessageSaysToPutTheToolAway()
    {
        Assert.Equal("Plan Hammer keeps the wheel. Put it away to scroll the hotbar again.",
            WheelWords.Hint(HintKind.KeepsTheWheel, "Plan Hammer", "Left Alt", "", flipBack: false));
    }

    [Theory]
    [InlineData("LeftAlt", "Left Alt")]
    [InlineData("RightControl", "Right Control")]
    [InlineData("BackQuote", "Back Quote")]
    [InlineData("F1", "F1")]
    [InlineData("Mouse3", "Mouse3")]
    [InlineData("Z", "Z")]
    public void KeysAreNamedWithSpacesBetweenTheirWords(string key, string shown)
    {
        Assert.Equal(shown, WheelWords.Spaced(key));
    }
}
