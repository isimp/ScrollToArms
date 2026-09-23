# ScrollToArms

Hold Left Alt and turn the mouse wheel to pick a weapon or tool from your hotbar. A frame moves along the bar as you scroll, and whatever it rests on goes into your hand when you let go of Alt. Plain scrolling still zooms the camera. The settings can swap the two, so that the wheel picks straight away and Alt with the wheel zooms.

The game keeps its rules. The wheel passes over food, armour, materials and broken items. A pick the game refuses right now, because you attack, dodge or swim, or because you run while the item needs time to equip, waits with its slot pulsing and goes into your hand as soon as the game allows it. With the hammer, hoe or cultivator out, the plain wheel still rotates the piece you are placing, and Alt with the wheel still switches away. After you put your weapons away with the Hide key, the wheel carries on from the stowed weapon, and scrolling back onto it brings both hands back, the same as pressing Hide again.

ScrollToArms only runs on your own machine. Nothing it changes is sent to other players, and servers do not need it.

## AI notice

Most of ScrollToArms was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Settings

All settings are in BepInEx/config/isimp.ScrollToArms.cfg, each with a description. They include the modifier key, whether plain scrolling zooms or picks, whether a pick is equipped when you let go or after a short pause, wrapping and direction, skipping build tools, tools that keep Alt with the wheel for themselves (PlanBuild's by default), and how long a pick waits while you are busy.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/ScrollToArms
