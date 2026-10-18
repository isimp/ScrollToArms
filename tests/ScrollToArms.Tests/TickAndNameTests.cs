using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// Ticks are at least 40 milliseconds apart, so fast scrolling does not stack them into a buzz.
/// </summary>
public class TickGateTests
{
    [Fact]
    public void TheFirstTickPlays()
    {
        Assert.True(new TickGate().Allow(now: 0f));
    }

    [Fact]
    public void ATickCloserThan40MillisecondsToTheLastIsDropped()
    {
        var gate = new TickGate();
        gate.Allow(now: 5f);

        Assert.False(gate.Allow(now: 5.03f));
        Assert.True(gate.Allow(now: 5.05f));
    }

    [Fact]
    public void ADroppedTickDoesNotPushTheNextOneBack()
    {
        var gate = new TickGate();
        gate.Allow(now: 5f);
        gate.Allow(now: 5.03f);

        Assert.True(gate.Allow(now: 5.041f));
    }
}

/// <summary>
/// SkipItems and StepAsideTools name items, separated by commas: by the name the game shows in the
/// current language, by the name token or by the prefab name, ignoring case. The list is read again
/// whenever the setting's text changes.
/// </summary>
public class ItemNameListTests
{
    private string _setting = "";

    private ItemNameList List() => new ItemNameList(() => _setting);

    private static string Shown(string token)
    {
        return token == "$item_hammer" ? "Hammer" : token == "$item_pickaxe_bronze" ? "Bronze Pickaxe" : token;
    }

    [Fact]
    public void AnEmptySettingMatchesNothing()
    {
        Assert.False(List().Contains("Hammer", "$item_hammer", Shown));
    }

    [Fact]
    public void AnItemIsMatchedByItsShownName()
    {
        _setting = "Bronze Pickaxe";
        Assert.True(List().Contains("PickaxeBronze", "$item_pickaxe_bronze", Shown));
    }

    [Fact]
    public void AnItemIsMatchedByItsPrefabName()
    {
        _setting = "BlueprintRune";
        Assert.True(List().Contains("BlueprintRune", "$item_blueprintrune", Shown));
    }

    [Fact]
    public void AnItemIsMatchedByItsToken()
    {
        _setting = "$item_hammer";
        Assert.True(List().Contains("Hammer", "$item_hammer", Shown));
    }

    [Fact]
    public void CaseDoesNotMatter()
    {
        _setting = "bronze pickaxe, blueprintrune";
        Assert.True(List().Contains("PickaxeBronze", "$item_pickaxe_bronze", Shown));
        Assert.True(List().Contains("BlueprintRune", "$item_blueprintrune", Shown));
    }

    [Fact]
    public void NamesAreSeparatedByCommasWithSpacesAroundThemIgnored()
    {
        _setting = " Hammer ,  , Bronze Pickaxe ";
        var list = List();

        Assert.True(list.Contains("Hammer", "$item_hammer", Shown));
        Assert.True(list.Contains("PickaxeBronze", "$item_pickaxe_bronze", Shown));
        Assert.False(list.Contains("Club", "$item_club", Shown));
    }

    [Fact]
    public void APartOfANameIsNoMatch()
    {
        _setting = "Pickaxe";
        Assert.False(List().Contains("PickaxeBronze", "$item_pickaxe_bronze", Shown));
    }

    [Fact]
    public void ChangingTheSettingChangesWhatMatches()
    {
        var list = List();
        _setting = "Hammer";
        Assert.True(list.Contains("Hammer", "$item_hammer", Shown));

        _setting = "Bronze Pickaxe";
        Assert.False(list.Contains("Hammer", "$item_hammer", Shown));
        Assert.True(list.Contains("PickaxeBronze", "$item_pickaxe_bronze", Shown));
    }

    [Fact]
    public void AnItemWithoutAPrefabOrATokenIsMatchedByWhatItHas()
    {
        _setting = "Hammer";
        Assert.True(List().Contains(null, "$item_hammer", Shown));
        Assert.False(List().Contains("Club", null, Shown));
        Assert.False(List().Contains(null, "", Shown));
    }
}
