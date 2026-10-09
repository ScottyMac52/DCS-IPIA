# Distribution readiness and support

Feature #163 adds acceptance tooling. It does not certify hardware behavior, in-DCS behavior or distribution rights. Record the exact release version, source commit and TEST-EVIDENCE.json before broader distribution. Outstanding rights/notice decisions are tracked in [#183](https://github.com/ScottyMac52/DCS-IPIA/issues/183).

## Support report contract

Help → Export support report (Ctrl+Shift+D) captures saved state for the selected profile, previews the complete JSON and saves exactly that capture to a new local file. Nothing is sent/uploaded. F1 opens offline workflow help.

Format 1 includes four-part application/runtime versions, supported/observed schema, read-only SQLite quick_check/foreign-key results, numbered saved device capability/unsupported-object/template-hint counts and historical discovery failure flags, numbered module profile/assignment/bundled/generic/custom/page counts, verified custom-library count, and fixed failure stages/categories/next steps. Historical inventory is not a live connectivity check. Template hints are not confirmed mappings. Missing/unreadable scopes are null; per-module failures retain the readable modules.

Explicit projections omit names, paths, GUIDs, persistent identifiers/hashes, command text, assignment contents, preferences, native names/object evidence, artwork/source, database bytes, exception messages/stacks and raw logs. The report cannot reproduce private input content by itself. A maintainer may ask for a separately reviewed module snapshot or log; users choose what to share. See logging.md for local physical log location, assembly levels and sensitive fields.

Collection uses read-only SQLite connections, deferred inventory reads and legacy workspace upgrades in memory with persistence disabled. It never rescans controllers, migrates, saves drafts or repairs data. Startup database failures expose the same support preview from the recovery window. A new destination is required; overwrite attempts fail.

## Automated evidence

The release candidate's executable seeds three real modules: bundled default hardware, an unbundled generic controller with a saved button assignment, and the same controller with a pinned custom draw.io revision. It reopens SQLite, verifies drawing source and inventory/library evidence, renders PNGs and generates native Lua. A saved baseline hashes reopened snapshots, PNG dimensions/decoded RGBA pixels and Lua. The installed/reinstalled executable reopens copies of those same profiles and compares behavior with the original baseline. Regular and bold Arial/sans-serif SVG captions resolve to the matching shipped DejaVu face rather than a machine-installed font; display and scaled rendering share this policy. Failed synthetic comparisons retain baseline/current PNGs and SVG in a CI diagnostic artifact. PNG comparisons use dimensions and decoded pixels rather than encoding/compression bytes; saved sources and native Lua remain exact hash comparisons. Installer checks also hash every profile file, including the verified custom revision, across setup reruns, uninstall and reinstall. This tests current-candidate replacement and reinstall; an upgrade from an older released version requires the manual matrix below.

SupportReportTests covers allow-list privacy against deliberately sensitive content, exact preview export/no overwrite, partial corrupted sources, absent/corrupt/newer databases, cancellation, legacy read immutability and migration transaction rollback/retry. UserDataRecoveryTests covers verified full restore, retained corrupt originals, unsafe/incompatible backups, fault injection at prepared/old-moved/installed boundaries, startup recovery of interrupted swaps and verification of completed replacements. Automated fault injection does not simulate every power-loss/filesystem failure.

## Attribution inventory shipped with the payload

The notices folder is copied to portable and installed payloads and checked by exact installed-file hashes. Write-DistributionNotices.ps1 inventories the actual restored application graph from project.assets.json; it includes transitive/native and build dependencies, exact versions, NuGet license metadata, project URLs, copyrights and available license/notice files, with missing evidence marked reviewRequired. It also includes installed .NET distribution notices. This is a metadata inventory, not a certification that every native embedded component's notice is complete; review #183 before wider distribution.

| Evidence | Scope and source |
| --- | --- |
| IPIA-LICENSE.txt | Repository license |
| DCS-Common-LICENSE.txt, shared-assets-provenance.json | Pinned shared asset commit and per-file Git blobs |
| hardware-SOURCES.md | Manufacturer/user-provided image sources; expressly does not relicense manufacturer images |
| asset-authoring-provenance.json | Pinned DCS-Common authoring scripts and local changes; developer tooling, not a runtime prerequisite |
| DejaVuSans-IPIA.LICENSE.txt | Bundled bold and regular DejaVu font notices |
| dependencies.json, packages/ | Resolved application NuGet metadata and available notices |
| .NET-LICENSE.txt, .NET-ThirdPartyNotices.txt, build-notices.json | Installed .NET distribution notices and SDK/source evidence |

Current direct production families are Microsoft.Data.Sqlite, Microsoft.Extensions.DependencyInjection/Configuration.Json, SkiaSharp, Svg.Skia and Serilog.Extensions.Hosting/Settings.Configuration/Sinks.File. Exact resolved versions belong to the candidate inventory, not a stale hand-maintained table. xUnit, test SDK and coverage tooling are development/test dependencies and do not form the app graph.

User draw.io sources/images are user-provided content. Import only content you may use and redistribute; retain permission/source evidence when sharing snapshots or backups. Validation checks file/geometry integrity, not ownership. Embedded images need the same review as standalone images. Repository software licenses alone do not resolve manufacturer-photo, trademark or other redistribution decisions.

## Manual acceptance record — pending

Copy this matrix into the release evidence, complete each row with actual observations and attachments, and record tester/date, Windows/DCS versions, app version/source commit, controller model/firmware, baseline app version (for upgrades) and intended scope. Do not put private paths/GUIDs into public evidence unless explicitly reviewed.

| Scenario | Required check | Current result |
| --- | --- | --- |
| Clean Windows user, no Common/Node/developer tools | Install candidate; onboard an unbundled controller; test actual supported buttons/hats/axes; create/save/build generic output | Pending |
| Scott's mixed and repeated units | Give repeated units distinct labels; select only intended units; verify independent assignments; unplug/reconnect/rescan without losing pins | Pending |
| F-15E and another aircraft | Fly Now! directly after creation/import/reopen; unused controllers have no assigned axes; intended assigned axes retain filters and modifiers; verify both aircraft and UI Layer in DCS | Pending |
| Custom artwork | Import owned drawing/images; explicitly apply revision; save, reopen, build and verify physical inputs correspond to the preview | Pending |
| Older-release upgrade | Back up actual bundled/custom modules using old released app; install candidate; compare sources, pins, assignments, previews/native output and DCS behavior | Pending |
| Uninstall/reinstall | Close app; uninstall/reinstall; reopen the same Production/test profiles and verify the same outputs and drawings | Pending |
| Recovery | In an isolated disposable profile, restore verified backup; inspect retained original/review deployment; restart after interrupted restore; retain logs/report for failures | Pending |
| Keyboard and themes | In light/dark/high-contrast and chosen scaling, navigate menus, setup, editor, help, backup and report by keyboard; confirm focus/readability/F1/version | Pending |
| Attribution decision | Review candidate notices and photo/embedded-image source evidence; link decisions in #183 | Pending |

Use isolated --test-profile NAME for non-deployment checks; named profiles disable Fly Now. For actual DCS use a reviewed Production setup and back up the DCS input folder separately. Full user-data backups do not back up DCS deployments. Keep #163 and parent #155 open until the complete acceptance evidence is recorded.

