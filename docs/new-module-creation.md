# Create a module locally

Choose **New module** in the Modules window. Enter the exact aircraft input-folder name used by DCS and a display name. Reserved Windows names, unsafe paths and an identity already stored in the database are rejected.

Select any initial aircraft hardware, or leave the selection empty and add hardware later in **Devices**. Use Devices to edit native filenames, physical GUIDs, repeated instances and roles. Continue with the existing Modifiers and Assignments editors.

Creation saves and reopens a complete workspace in SQLite. No consumer repository or Saved Games configuration is required. The current saved shared-default revision is copied and pinned: later default changes require reviewed adoption. Each newly created module receives its own profile identities. Canonical Lua, keyboard sources, modifier definitions, unmapped inputs and removal history remain in the snapshot.

If an independently authored shared joystick has no unambiguous hardware mapping, creation reports the profile requiring a choice. Open **Shared default UI Layer**, choose its hardware in Devices, save that revision, then retry.

This is the first Step 3 slice of issue #83. Aircraft command catalog authoring, page-layout authoring and full newly authored snapshot/package acceptance remain subsequent slices. An empty aircraft command catalog is expected at this stage; importing a command catalog remains available.
