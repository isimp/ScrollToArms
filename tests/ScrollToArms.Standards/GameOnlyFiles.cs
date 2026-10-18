using System.Collections.Generic;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// The files under src/ the unit tests leave out, as tests/ScrollToArms.Tests/ScrollToArms.Tests.csproj
    /// compiles only what it names, each with why it needs the game.
    /// </summary>
    internal static class GameOnlyFiles
    {
        public static readonly Dictionary<string, string> List = new Dictionary<string, string>
        {
            ["src/Plugin.cs"] = "the plugin class, which BepInEx starts with the game, with its settings",
            ["src/Picker.cs"] = "reads the wheel and the player's hands and equips through the game's Player; src/HotbarWalk.cs holds its walk along the bar",
            ["src/WheelReads.cs"] = "patches the game's camera zoom and piece rotation with a Harmony transpiler",
            ["src/HotbarView.cs"] = "draws the pick on the game's own hotbar with Unity UI",
            ["src/WheelMode.cs"] = "the line saying what the wheel does, read from Unity's input",
            ["src/BuildToolHint.cs"] = "a message on the game's screen when a build tool arrives in hand",
            ["src/Stow.cs"] = "reads and restores the items the game's Hide key put away, by reflection on the Player",
            ["src/StepAside.cs"] = "matches the tools in the player's hands",
            ["src/ItemNameList.cs"] = "matches the game's items by prefab, token and the name the game's Localization shows",
            ["src/KeyText.cs"] = "names a key through Unity's key codes and the game's ZInput",
            ["src/UnityNull.cs"] = "Unity's own check for an object that is gone, which needs UnityEngine",
            ["src/Sound.cs"] = "copies the game's sound prefabs and plays them through Unity's audio",
        };
    }
}
