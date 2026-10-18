# Live input testing (first version)

Open a saved module's **Input tester** workspace view. Select **Current edits** or **Saved profile**, then click **Start test**. IPIA generates the same Lua payload used by export and freezes it for the session. Pending assignments are included only in Current edits. Stop/Start after editing to capture a fresh payload. Testing never saves edits or installs a package.

Select the device you want to test, then operate that controller. Feedback is filtered by its exact physical GUID; inputs and idle axis noise from other controllers are hidden. Other controllers and keyboard modifiers remain sampled for cross-device Hold/Toggle chords. Changing the selected device clears the prior device's log and axis highlights. An unresolved device selection produces no input feedback.

The event log shows native instance GUID, DCS input key, press/release or normalized axis position, active scope modifiers, and every matching command name/ID. Multiple results expose Module/UI overlap and duplicate bindings. Colored callout outlines show physical activity on the current diagram; testing does not select controls or change scope/layer. Unassigned means absent from the generated diff profiles; DCS's built-in defaults may still apply.

Live outlines and legend swatches share this palette. Each outline and swatch has white and black contrast edges, including over photographic artwork. Only the applicable physical callout lights; its center stays transparent. Inactive swatches are dimmed and active entries also show a dot, so the modifier name and activity marker accompany the color.

| Live assignment | Color |
| --- | --- |
| Aircraft Base (no modifier) | Blue `#007ACC` |
| Aircraft modifier/chord slots 1–5 | Red `#dc2626`, orange `#ea580c`, magenta `#c026d3`, green `#16a34a`, violet `#9333ea` |
| UI Layer Base | Cyan `#0891b2` |
| UI Layer modifier/chord slots 1–3 | Purple `#7c3aed`, rose `#be185d`, teal `#0f766e` |

Chord slots follow the current assignment chords in ordinal name order within each scope. Additional aircraft/UI chords reuse violet/teal; always read the actual chord name. The shared shift colors also appear on exported kneeboards. Printed aircraft Base lettering retains its authored/native appearance; blue identifies live Base activity. A UI assignment keeps cyan or its own shift color when it is the applicable highlight; it is not recolored as aircraft Base.

Hold and Toggle modifiers are resolved separately per scope and may live on another controller. Supported foreground keyboard modifier keys are also sampled. Keyboard activity is identified as modifier input; keyboard command profiles are outside v1. Toggles change only on a new press, including after release; an initially held toggle does not latch. Modifier state clears at Stop, focus loss or device loss. Release results retain the command matched at press.

Axes show raw normalized position from zero to one and the actual exported tuning metadata when assigned. This is not an exact DCS curve/detent simulation. Significant axis changes use a one-percent threshold. Input is sampled approximately every 16 ms on the UI dispatcher; very brief pulses between samples may be missed. This first version does not claim buffered encoder-event or in-game execution verification.

Physical matching uses DirectInput instance GUIDs from generated native filenames. Identical units remain distinct. Missing devices, unqualified filenames, unsupported mapped inputs and unresolved modifiers are reported as a **Partial test**. No device-name guessing or hardware-axis defaults are used. Only enumerated absolute axes, up to 128 buttons and four eight-direction POV hats are read. Other input objects are unsupported in v1. Connect/reconnect devices and restart the session to enumerate them again.

Testing uses foreground nonexclusive DirectInput acquisition and sends no input or force-feedback commands. Switching away from IPIA stops the test and releases native handles. Invalid Lua generation prevents Start; acquisition loss stops the whole session with a diagnostic. DCS installation is not required. An actual DCS walkthrough is needed to verify cockpit behavior.

Automated acceptance uses fake input sequences and real SQLite/generated-payload round trips, plus Windows UI smoke tests for Start/Stop, saved/current selection, highlight/log feedback, focus loss and disconnect, and rendered contrast rings on black and white backgrounds for every palette color. Both interactive training scenarios verify blue Base, matching red/orange sample modifier highlights, and cyan UI Base swatches. Physical-controller verification remains the user's acceptance step.
