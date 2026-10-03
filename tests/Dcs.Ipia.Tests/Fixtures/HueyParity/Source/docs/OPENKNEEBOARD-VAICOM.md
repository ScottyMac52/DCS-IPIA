# OpenKneeboard and VAICOM PRO

## Included kneeboard tab

The OvGME package installs 6 numbered PNG reference pages into `KNEEBOARD\UH-1H`. OpenKneeboard should discover the directory through its DCS Aircraft tab; otherwise add it as a Folder tab.

Planned generated pages:

- `00-UH1H-CONTROL-OVERVIEW.png`
- `01-TM-MFD-1.png`
- `02-TM-MFD-2.png`
- `03-TM-MFD-3.png`
- `04-MOZA-AB9-HORNET-GRIP.png`
- `05-TM-WARTHOG-THROTTLE.png`

## VAICOM PRO

This scaffold does not bundle an AutoHotkey/VAICOM PTT bridge. Installing it does not change VoiceAttack or VAICOM. If a bridge is added, document its physical inputs and chords here and reserve those inputs in the module profile.

## Optional VoiceAttack navigation

OpenKneeboard remote-control programs are normally under `C:\Program Files\OpenKneeboard\utilities`. Useful commands include `NEXT_PAGE`, `PREVIOUS_PAGE`, `NEXT_TAB`, `PREVIOUS_TAB`, `INCREASE_BRIGHTNESS`, `DECREASE_BRIGHTNESS`, `ENABLE_TINT`, and `DISABLE_TINT`. Use phrases that do not overlap VAICOM keywords.
