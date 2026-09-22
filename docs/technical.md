# Technical notes

## How it works

`GameCamera.UpdateCamera` reads the mouse wheel itself through `ZInput.GetMouseScrollWheel`. ScrollToArms redirects those reads, and only those, through a function that returns zero while the hotbar owns the wheel. The minimap, the placement ghost's rotation and other mods keep reading the real value. If the game no longer reads the wheel where expected, the mod turns itself off for the session and leaves the camera untouched.

The hotbar owns the wheel only where the game's own hotbar cursor would work: not in menus, the inventory, the map, chat, the radial menu or build mode. Which half of the wheel is plain and which needs the modifier is the `PlainScroll` setting. One notch is the same step the game uses to rotate a building piece.

Items are equipped through `Player.ToggleEquipped`, never through the hotbar's own `UseHotbarItem`. That path uses the item on whatever you are looking at, eats food and unequips what is already in hand, none of which a wheel passing over a slot should do. `ToggleEquipped` keeps the game's rules: items with an equip time are queued with their progress bar, and the hand rules decide what the new item replaces. A pick refused by an attack, a dodge or swimming is held for `WaitWhileBusy` seconds and applied as soon as the game allows it.

The pick is shown with markers the vanilla hotbar already has: the selection frame the game uses for its gamepad cursor, and the queued marker for an item waiting to be equipped. They are written every frame after all updates, because the bar is not guaranteed to refresh its icons every frame, and restored to the game's own state when there is nothing to show.

With both hands empty after the Hide key, the cursor starts from the stowed item's slot, which the game remembers until another hand item is equipped. Picking that slot calls the game's own `ShowHandItems`, which brings back both hands.

The modifier is read as a plain key rather than a BepInEx shortcut, because a shortcut does not fire while any other key is held.

## Limits

Only the eight slots of the main hotbar are used. Extra bars added by other mods are left alone. The mod is for keyboard and mouse; a gamepad already has the game's own hotbar cursor. In build mode the wheel belongs to the game, so a build tool picked with the wheel can only be put away with the Hide key or a number key. The mod says so when it happens, and skips build tools by default.

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
