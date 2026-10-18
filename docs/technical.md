# Technical notes

## How it works

Two places in the game act on the mouse wheel while the hotbar can be used: the camera zoom in `GameCamera.UpdateCamera` and the rotation of the piece being placed in `Player.UpdatePlacement`. Both read it through `ZInput.GetMouseScrollWheel`. ScrollToArms redirects those reads, and only those, through a function that returns zero while the hotbar owns the wheel. The minimap and other mods keep reading the real value.

The redirect is judged when the transpiler runs rather than when the patch is requested, because a patcher can defer Harmony patches until later in startup. Until the camera's reads are found, the hotbar never takes the wheel, and if the game no longer reads it where expected the mod stays off and leaves the camera untouched. Without the piece rotation's read, hotbar scrolling stays off in build mode only.

The hotbar owns the wheel only where the game's own hotbar cursor would work: not in menus, the inventory, the map, chat, the radial menu or the piece menu. Which half of the wheel is plain and which needs the modifier is the `PlainScroll` setting. In build mode the plain wheel always rotates the piece, so there the hotbar is always on the modifier. One notch is the same step the game uses to rotate a building piece.

Items are equipped through `Player.ToggleEquipped`, never through the hotbar's own `UseHotbarItem`. That path uses the item on whatever you are looking at, eats food and unequips what is already in hand, none of which a wheel passing over a slot should do. `ToggleEquipped` keeps the game's rules: items with an equip time are queued with their progress bar, and the hand rules decide what the new item replaces. A pick that lands on the slot already in hand unequips that item through the same call, as its number key would, so a shield in the other hand stays. Going back to the held item from a pick still on its way only withdraws that pick. A notch that cannot move the frame off where it starts, past the end of the bar with `Wrap` off or with the held item the only one the wheel stops on, starts no pick, so letting go of the modifier or the pause never puts away what is in hand without the frame having moved.

A pick is followed until the item is in hand, or out of it for an unequip. The game refuses any equip during an attack, a dodge or while swimming, and it clears its equip queue, unequips included, on every frame the player runs (`Player.CheckRun`) as well as on a jump, a dodge or the start of an attack, so an item with an equip time cannot arrive while running. A pick refused or cleared that way is held for `WaitWhileBusy` seconds, with its slot pulsing, and asked for again as soon as the game allows it. The game's own equip bar does not count against that time. A pick the game refuses without queueing it, such as a broken item, is dropped rather than asked for again. A pick lost either way flashes its slot red and fades; a pick the player replaces or cancels with a number key does not.

The pick is shown with markers the vanilla hotbar already has: the selection frame the game uses for its gamepad cursor, and the queued marker for an item waiting to be equipped. The frame pulses from the moment a wait begins, and a lost pick tints it red while it fades. They are written every frame after all updates, because the bar is not guaranteed to refresh its icons every frame, and restored to the game's own state when there is nothing to show. The item's name is a copy of a slot's own number text, so it uses the hotbar's font. It belongs to the bar rather than to a slot and sits at a fixed spot above the bar's left end, left-aligned and slightly transparent, so a long name never runs off the screen. When no pick is shown, the same line says what the wheel does if it differs from ordinary play or the modifier is held; its text is rebuilt only when that state changes.

With both hands empty after the Hide key, the cursor starts from the stowed item's slot, which the game remembers until another hand item is equipped. Picking that slot calls the game's own `ShowHandItems`, which brings back both hands.
A tap on the modifier goes back to what the hands held before, through the same path as a pick. A tap is a press shorter than 0.3 seconds with no other key, mouse button or wheel movement in between and the game window in focus, so chords with the modifier and holding it for other mods never count. While a pick is on its way, a tap goes back to what is in hand, which withdraws the pick; with empty hands it goes back to the last thing held, which brings stowed items back.

The tick and the lost-pick sound are copies of the game's own sound prefabs, `sfx_gui_inventory_open` and `sfx_gui_inventory_close`, found once on entering the world, from the scene or from an index of the game's effect lists. Each copy is made under an inactive holder, its audio sent to the game's interface mixer group and made two-dimensional, and only then activated, so its `ZSFX` plays it as the game plays its own sounds, including `AudioMan`'s limit on the same sound repeating. The game sets that group's level from the master and sound effects volume. The copy's network view is kept from starting, so only the local player hears it. Ticks are at least 40 milliseconds apart.

The modifier is read as a plain key rather than a BepInEx shortcut, because a shortcut does not fire while any other key is held.

`SkipItems` and `StepAsideTools` match an item by the name the game shows for it in the current language, by its name token or by its prefab name, ignoring case.

## Limits

Only the eight slots of the main hotbar are used. Extra bars added by other mods are left alone. The mod is for keyboard and mouse; a gamepad already has the game's own hotbar cursor. A mod that reads the modifier and the wheel together in its own code, with one of its tools in hand, would see both its own action and a hotbar pick. Such a use cannot be detected from outside, so `StepAsideTools` lists the tools for which the modifier and the wheel are left alone. It defaults to PlanBuild's `BlueprintRune` and `PlanHammer`. When the wheel puts a listed tool in hand, a message names the Hide key that puts it away, and a tap on the modifier also switches back.

## Building

Requires the .NET SDK 8 or newer. To build against the reference stubs in `lib/`, with no game installation needed:

```
dotnet build -c Release -p:LibsDir=lib
```

To build against a local installation and copy the result into a BepInEx profile:

```
dotnet build -c Release -p:ValheimDir="<Valheim folder>" -p:ProfileDir="<profile folder>"
```

The `VALHEIM_DIR` environment variable can be used instead of `ValheimDir`. Without these, a default Steam installation and a default Gale profile are assumed.

The files in `lib/` contain metadata only: every method body is replaced and resources are removed, so they can be compiled against but not run. Regenerate them after a game update with `tools/strip-references.ps1`. Releasing is described in [releasing.md](releasing.md).

## Tests

Everything the mod decides is in game-free classes the unit tests drive, against the metadata-only game assemblies in `lib/`, of which they use only the game's enums: the pick from the first notch until the item is in hand (`src/PickFlow.cs`, played frame by frame through a fake game: the frame moving from stop to stop, letting go or the pause taking it in hand, the latest pick winning, a pick on the held slot putting that item away and a notch that cannot move the frame doing nothing, waiting while the game would refuse or drop a pick and losing it after `WaitWhileBusy`, a refusal outright, the Hide key's items picked back, a tap going back and what spoils a tap), what counts as a stop and what holds a pick back (`src/PickRules.cs`), the walk along the bar and how the wheel's readings make notches (`src/HotbarWalk.cs`), what the wheel line, the tool message and the patch's log lines say and when (`src/WheelWords.cs`), how the bar marks a pick, its pulse and its flash (`src/PickLook.cs`), the spacing of ticks and how `SkipItems` and `StepAsideTools` match items. The game-side files read the game for them and carry out what they decide; `GameOnlyFiles.cs` names each with why it needs the game.

```
dotnet test tests/ScrollToArms.Tests
```

The build itself runs every .NET code analysis rule and fails on any finding (`BuildChecks.props`), but for the few that misread a Unity plugin, which `Analysis.globalconfig` turns off with the reason for each. It also compiles the doc comments, so a link to something renamed fails it. `tests/ScrollToArms.Standards` holds what the compiler cannot. It compiles the source as the build does, against the stubs in `lib/`, and reads it with the compiler's own types. Text is compared and sorted ordinally, never in the PC's language, and a fraction or a time becomes text the same way on every PC. A Unity object is checked for none by Unity's own test. A failure caught is told in the log, or the catch says what the failure means. Comments carry no dates, each member has one summary of its own, every file under `src/` is compiled into the unit tests or named in `GameOnlyFiles.cs` with why it needs the game, every test-only `Reset()` forgets all its class holds, and the plugin, the project and `manifest.json` name one version. They need no `BepInEx.dll`, so the build workflow runs them on every push:

```
dotnet test tests/ScrollToArms.Standards
```

`.githooks/pre-push` runs both sets before every push and stops the push when one fails, and `.githooks/commit-msg` refuses a message that carries a clock time. Git uses them once told to, per clone: `git config core.hooksPath .githooks`.

`selftest/` is an in-game self-test, a plugin of its own that reaches into ScrollToArms through `InternalsVisibleTo`; build it with `dotnet build selftest` and type `scrolltoarms selftest` in the console, or with a slash in chat, then close the console. It checks that the camera zoom and the piece rotation hand the wheel over and that the game's equip call, the Hide key's memory, the hotbar's markers and the two sounds were found, and walks your own hotbar through the picker's walk: every slot it stops on holds a weapon, a bow, a shield, a tool or a torch, scrolling left stops on the same slots as scrolling right, and the end of the bar wraps or stays as `Wrap` says. With two weapons or tools on the bar it then picks between them the way a notch and a tap drive the picker: the frame on the next weapon, the weapon in hand after letting go, the queued marker while it is on its way, a tap back, a pick on the held slot putting it away, the Hide key's items picked back, and, with `Wrap` off for that part in memory only, a notch past the last weapon leaving it in hand. What your hands held, and what the Hide key had put away, is put back at the end. It writes `[selftest]` lines to `LogOutput.log` and shows the result on screen. It is never packaged; a release takes `ScrollToArms.dll` alone.
