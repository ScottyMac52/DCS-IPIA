# Interactive training

Choose **Help → Training / Guided workflows…** from the Modules window. Training
shows the actual New module/import forms and module feature views in a separate
window with a narration panel. The sample aircraft is called **TRAINING**, and
its Logitech quadrant has an intentionally fictional physical GUID.

Select either **Starting from scratch** or **Starting with a preconfigured module
in DCS**. Choose **Play / Resume** to demonstrate actions, or **Next step** to
perform one action and pause. Reading checkpoints pause automatically. **Pause**
lets the active operation finish before stopping; it never interrupts a database
save halfway through. The explanation and yellow/black target outline remain
visible while paused. Playback speed changes the interval between automatic
steps; explicit checkpoints still require Resume or Next.

**Restart scenario** creates a fresh sample database and files. Changing scenario
also starts fresh. **Exit training**, or closing the window, settles active work
and removes the training files. An error identifies the failed step; restart to
retry. Training does not support moving backward through completed edits.

The workflows cover module creation or Saved Games import, command-catalog import,
base and shifted assignments, reserved modifier controls, labels, axis tuning,
module-owned UI Layer assignments, save/discard/reopen, device and summary pages,
real kneeboard preview, validation, snapshot export, and OvGME packaging.

## Sample data and normal use

Training creates a temporary directory under `DCS-IPIA-Training`, with separate
SQLite storage, input fixtures, and outputs. It uses shipped hardware graphics,
a supplied UI default revision and a fictional disconnected controller; it needs
neither a DCS installation nor connected hardware, a repository checkout, network
access or a test SDK. Application controls in the demonstration area are operated
by playback; use the panel's controls and keyboard navigation to control it.

Training is automatic demonstration, not a practice editor. The original database,
shared defaults, settings, open drafts, and Saved Games configuration are not
passed into the training session. The existing application is retained behind the
training window. No live deployment action is run. The final checkpoint explains
Fly Now!/OvGME deployment and verification for the learner to perform outside
training against their own backed-up configuration.

The sample command codes illustrate DCS profile syntax. They are instructional
examples, not a supported aircraft configuration to install in DCS. Replace the
sample aircraft identity, command catalog, and physical GUIDs with verified values
for your aircraft/devices in normal use. Importing command JSON/HTML loads command
definitions, not assignments. Native diff files omit unchanged DCS defaults; an
empty diff does not clear inherited axes. Review the
[module authoring guide](module-authoring.md) before deploying real controls.
DCS itself, including generating Controls HTML/default captures and in-game input
verification, is not automated by training.

## Maintenance and validation

Scenario narration/targets/checkpoints live in versioned JSON under
`src/Dcs.Ipia.App/Training/Assets`. Assets are embedded in the application assembly,
so installed and portable distributions do not depend on source-tree paths.
Only actions registered in `TrainingActions` can execute. Schema and metadata
validation run before playback; each action verifies completion, and asynchronous
forms/outputs have bounded waits. Delays are pacing only.

The training shell hosts real WPF content and uses normal event handlers or
application operations, without referencing the smoke-test executable. The UI
Layer step currently uses the normal draft assignment service and its change
notifications because the embedded command manager lists aircraft commands. The
scope/callout and resulting assignment are shown in the real Assignments view.
This limitation should be removed when the assignment command palette supports
UI commands directly.

Core runner tests exercise pause/checkpoint/next/stop/restart/error semantics.
Fixture integration tests use real SQLite, imports and package validation. The
Windows `TrainingSmoke` runs both scenarios, checks missing-file recovery and
restart, and verifies actual snapshot/package output and cleanup. Published
application smoke validates that both embedded scenarios can load. Existing
coverage and CRAP gates remain unchanged. Manual review on Windows is still
needed for narration pacing, theme contrast, resizing/scaling and keyboard focus.

The small Lua/JSON fixtures and scenario narration were authored for this project;
shipped hardware artwork retains its existing provenance and attribution.
