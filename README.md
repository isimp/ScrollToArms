# ScrollToArms

Pick the weapon or tool in your hand with the mouse wheel. Hold Left Alt and scroll, and a frame ticks along your hotbar with the item's name above it. Let go of Alt and whatever it rests on goes into your hand, or out of it if you were already holding it. A quick tap on Alt goes back to what you held before, and whenever the wheel does something else, such as rotating a building piece, the same spot above the hotbar says so.

![The selection frame and the item name on the hotbar](https://raw.githubusercontent.com/isimp/ScrollToArms/main/docs/images/screenshot.webp)

You decide how the wheel is shared. By default plain scrolling still zooms the camera and Alt with the wheel picks from the hotbar. Swap them, and the wheel picks straight away while Alt with the wheel zooms. A pick goes into your hand when you let go of Alt, or once the wheel has been still for a moment, whichever you prefer. The key, that pause, the scroll direction and wrapping at the ends of the bar can all be changed too.

The game keeps its rules. The wheel passes over food, armour and materials. When the game refuses a pick because you attack, dodge, swim or run, its slot pulses until the item can go into your hand, and flashes red with a short sound if it never gets there. While building, the plain wheel still rotates the piece and Alt with the wheel still switches away.

ScrollToArms only runs on your own machine, and servers do not need it.

## AI notice

Most of ScrollToArms was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Settings

All settings are in BepInEx/config/isimp.ScrollToArms.cfg, each with a description. Besides the modes above, they cover items the wheel should pass over, skipping build tools, tools that keep the wheel for themselves (PlanBuild's by default) and how long a pick may wait. Items are named as the game shows them, such as Bronze Pickaxe. The name, the wheel line, the messages and the sounds can each be switched off.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/ScrollToArms
