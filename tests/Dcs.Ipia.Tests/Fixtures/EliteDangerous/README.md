# Elite Dangerous synthetic native fixture

`Synthetic.4.2.binds` is an independently authored XML test configuration using
native 4.x assignment structures. The device names and assignments are invented;
it contains no uploaded user configuration or hardware inventory.

It covers empty primary/secondary slots, a cross-device modifier, per-binding
Hold, command-level ToggleOn, inversion, deadzone precision, a device index,
duplicate scalar settings and unknown XML. It does not establish that the
invented devices are recognized by the game.

Tests assert unchanged source equality after import, JSON persistence and export.
Additional synthetic inputs in the test class cover targeted edits and invalid
or unknown extensions. Real in-game validation remains manual. The repository
owner's uploaded 4.2 preset was checked locally but is not distributed here.
