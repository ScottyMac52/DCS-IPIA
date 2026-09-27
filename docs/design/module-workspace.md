# DCS-IPIA module workspace navigation

Navigation simplification: [#48](https://github.com/ScottyMac52/DCS-IPIA/issues/48). The module chooser opens one module workspace directly, with Assignments as its default destination.

## Navigation

| Location | User action |
| --- | --- |
| Module chooser | Select a saved module and **Open module**, or **Import module sources**. Settings and theme are available here. |
| Assignments | Select device, modifier layer, and scope (**Module controls** or **Module UI Layer**), then edit a physical control with the command catalog and inspector. |
| Kneeboard | Inspect existing rendered device pages. The editor also provides a contextual rendered kneeboard action. |
| Diagnostics | Review import and graphic issues; opens automatically when an import has errors. |
| Sources | Inspect committed source provenance. |
| Import details | Expand the read-only Profiles and Bindings lists in the workspace header when needed. |

The module chooser does not repeat the workspace destinations. Import opens the workspace immediately; saved modules reopen from SQLite. Only a scope change resets the command Category filter to All. Other selection and draft changes preserve Category.

The controller canvas uses shared hardware geometry. Repeated MFD instances remain distinct. Assignment drafts are saved explicitly to the local database; source repositories are read-only.

## Remaining work

The detailed plan and acceptance criteria for further navigation polish, including direct links from diagnostics to controls and richer empty states, are in #48. Validation and package building remain unavailable until their application contracts are connected. Do not expose inactive destinations as a substitute for those workflows.
