# Live input testing (first version)

Open a saved module's **Assignments** tab. Select **Current edits** or **Saved profile**, then click **Test Inputs**. IPIA generates the same Lua payload used by export and freezes it for the session. Pending assignments are included only in Current edits. Stop/Start after editing to capture a fresh payload. Testing never saves edits or installs a package.

Operate a controller. The event log shows native instance GUID, DCS input key, press/release or normalized axis position, active scope modifiers, and every matching command name/ID. Multiple results expose Module/UI overlap and duplicate bindings. Blue callouts show physical activity on the current diagram; testing does not select controls or change scope/layer. Unassigned means absent from the generated diff profiles; DCS's built-in defaults may still apply.

Hold and Toggle modifiers are resolved separately per scope and may live on another controller. Supported foreground keyboard modifier keys are also sampled. Keyboard activity is identified as modifier input; keyboard command profiles are outside v1. Toggles change only on a new press, including after release; an initially held toggle does not latch. Modifier state clears at Stop, focus loss or device loss. Release results retain the command matched at press.

Axes show raw normalized position from zero to one and the actual exported tuning metadata when assigned. This is not an exact DCS curve/detent simulation. Significant axis changes use a one-percent threshold. Input is sampled approximately every 16 ms on the UI dispatcher; very brief pulses between samples may be missed. This first version does not claim buffered encoder-event or in-game execution verification.

Physical matching uses DirectInput instance GUIDs from generated native filenames. Identical units remain distinct. Missing devices, unqualified filenames, unsupported mapped inputs and unresolved modifiers are reported as a **Partial test**. No device-name guessing or hardware-axis defaults are used. Only enumerated absolute axes, up to 128 buttons and four eight-direction POV hats are read. Other input objects are unsupported in v1. Connect/reconnect devices and restart the session to enumerate them again.

Testing uses foreground nonexclusive DirectInput acquisition and sends no input or force-feedback commands. Switching away from IPIA stops the test and releases native handles. Invalid Lua generation prevents Start; acquisition loss stops the whole session with a diagnostic. DCS installation is not required. An actual DCS walkthrough is needed to verify cockpit behavior.

Automated acceptance uses fake input sequences and real SQLite/generated-payload round trips, plus a Windows UI smoke test for Start/Stop, saved/current selection, highlight/log feedback, focus loss and disconnect. Physical-controller verification remains the user's acceptance step.
