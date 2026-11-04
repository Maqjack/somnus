# 0012. Text follows Windows' Text size
Date: 2026-11-02 · Status: Accepted (Mac's decision in #104, 2026-11-02)

## Context
SWPA 3.11 asks for text scaling. Somnus followed the display scale (PerMonitorV2), but not Settings > Accessibility > Text size, because its font sizes were fixed tokens used as static resources. Windows keeps that setting as `TextScaleFactor` (100 to 225) under `HKCU\Software\Microsoft\Accessibility` and announces a change as a settings change. WPF does not apply it to fixed font sizes. Mac chose to support it.

## Decision
- At start, and whenever Windows announces a settings change, Somnus reads the text size (`TextScaleSetting`) and multiplies the font size tokens of the theme by it (`TextScale`). The views use the tokens as dynamic resources, so open windows follow at once.
- Widths that hold text grow by the same factor, through `b:TextScale.Width`: the windows (380, 360, 400) and the number, unit, clock and language boxes. The window height follows its content as before.
- Margins, check marks, radio dots and the logo keep their size, and so does the signature: it is decoration and would otherwise run into the buttons above it.
- `ViewSmokeTests` renders the main window, the settings and the warning in Polish at 150% and 225%.

## Consequences
- At 225% the main window is about 855 by 700 pixels at 100% display scale; it still fits a 1080p screen. With a large display scale as well, it can be taller than the screen.
- A new width in a view that holds text must use `b:TextScale.Width`, or it will cut text at large sizes; the 225% pictures show such a mistake.
- Reading the setting is a registry read in the user's hive; Somnus never writes it.
