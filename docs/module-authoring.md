# Author commands and pages

Open a saved module and choose **Commands** to add aircraft commands. Enter the exact DCS command code, a display name, category and key/button or axis type. **Stage command** updates the current draft and assignment palette. Command codes stay fixed after creation. Assigned commands cannot be removed or switched between key and axis until their assignments are cleared; unmapped imported bindings also protect their command.

Name and category edits retain labels, axis tuning, modifier chords, removed history and the module-owned UI Layer. The shared default catalog is edited separately.

Choose **Pages** to author kneeboards. **Add missing device pages** supplies one combined page for every pinned device, including inherited UI Layer devices. Select a device page to change its output name (without .png), heading, subtitle and order. Its hardware geometry remains pinned; MFD category captions remain editable in Devices. Remove a physical device through Devices rather than deleting its required page.

For an overview, choose **Summary**, enter a safe output name, heading, subtitle and order, then add one to fourteen cards. Each card has a heading, description and palette accent. Cards render in listed order, in the existing two-column layout. Edit or remove summary pages as needed.

**Preview draft** renders the page from current form values and pending assignments without saving. **Stage page** validates and stages the page. Existing unedited manifest fields, callout/category labels and per-card metadata remain preserved.

Use **Save to database** to persist all staged command, page, device, modifier, assignment and label changes together. **Discard draft** restores the last saved workspace. Reopen a module to load these definitions from SQLite; source repositories are not required.

This is the second Step 3 slice under #83. The final creation-to-validation/preview/immutable-snapshot/restore/OvGME workflow and user acceptance remain separate requirements.
