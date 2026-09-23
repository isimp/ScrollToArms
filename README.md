# ScrollToArms

Pick the weapon or tool in your hand with the mouse wheel. Hold Left Alt and scroll, and a frame moves along your hotbar. Let go of Alt and whatever it rests on goes into your hand.

![The selection frame on the hotbar](https://raw.githubusercontent.com/isimp/ScrollToArms/main/docs/images/screenshot.webp)

You decide how the wheel is shared. By default plain scrolling still zooms the camera and Alt with the wheel picks from the hotbar. Swap them, and the wheel picks straight away while Alt with the wheel zooms. A pick goes into your hand when you let go of Alt, or once the wheel has been still for a moment, whichever you prefer. The key, that pause, the scroll direction and wrapping at the ends of the bar can all be changed too.

The game keeps its rules. The wheel passes over food, armour and materials. When the game refuses a pick because you attack, dodge, swim or run, its slot pulses until the item can go into your hand, and flashes red if it never gets there. While building, the plain wheel still rotates the piece and Alt with the wheel still switches away.

ScrollToArms only runs on your own machine, and servers do not need it.

## AI notice

Most of ScrollToArms was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Settings

All settings are in BepInEx/config/isimp.ScrollToArms.cfg, each with a description. Besides the modes above, they cover skipping build tools, tools that keep the wheel for themselves (PlanBuild's by default) and how long a pick may wait.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/ScrollToArms
