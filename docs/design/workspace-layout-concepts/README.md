# Compact module workspace — concept references

The compact shell, split Assignments editor and layout persistence were delivered by merged PR #265. Read [current behavior and acceptance](../compact-workspace.md); these prototypes retain the original design exploration.

## Whole-workspace interactive mockup

[Open the mockup source](workspace-interactive.html) or
[download the HTML](https://github.com/ScottyMac52/DCS-IPIA/raw/refs/heads/main/docs/design/workspace-layout-concepts/workspace-interactive.html)
and open it locally in a browser. All eight feature tabs are interactive, with
sample edits, simulated input testing, Undo/Save, and local appearance preferences.
Only browser preferences persist; sample draft content resets on reload.
This is an illustrative design prototype, not an implementation specification.

Scott selected **Split workspace** on 18 October 2026: shared top feature navigation across all eight module views; commands on the left and controller selection/artwork on the right in Assignments; top-left canvas anchoring, whole-control fit and expandable selected-input details.

The images are AI-generated layout explorations, not screenshots of implemented behavior or a pixel-perfect specification. Hardware artwork, icons, sample assignments, counts, aircraft procedures and document content are illustrative. Reuse actual application assets and existing feature behavior; extra features suggested by the images need separate agreement. Do not ship the sample procedures as aircraft instructions. Input Test depicts unlocked access; this proposal does not change edition policy.

| View | Image |
| --- | --- |
| Assignments — approved baseline | [assignments.jpg](images/assignments.jpg) |
| Devices | [devices.jpg](images/devices.jpg) |
| Modifiers | [modifiers.jpg](images/modifiers.jpg) |
| Input Test — unlocked concept | [input-test.jpg](images/input-test.jpg) |
| Commands | [commands.jpg](images/commands.jpg) |
| Pages | [pages.jpg](images/pages.jpg) |
| Kneeboards | [kneeboards.jpg](images/kneeboards.jpg) |
| Documentation | [documentation.jpg](images/documentation.jpg) |

Images preserve the generated originals' dimensions and content, encoded as high-quality JPEG for issue embedding.

## Interactive Assignments prototype

Download `split-workspace-interactive.html` using GitHub's **Download raw file** action, then open the downloaded file in Edge, Chrome or Firefox. GitHub's source view does not execute it. It is a standalone design prototype, not a hosted deployment or the WPF application.

- Search/select a sample command and drag it onto a schematic input.
- Select an input, expand its detail strip, and use Assign selected or Clear.
- Try Undo and the simulated Save status.
- Resize the browser to inspect the layout reflow.

State is held only in memory. Reloading resets it; no database, controller or DCS files are accessed. Device artwork is schematic. Fit/100%, Change, Manage, module details and Build/export are explanatory placeholders; they do not implement the corresponding application workflows. The support shell may load optional icon/tooltip utilities from public CDNs.

Only the approved split layout is included; the initial alternative concepts are not implementation requirements.
