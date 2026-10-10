# Community distribution review

Related: [#183](https://github.com/ScottyMac52/DCS-IPIA/issues/183), artwork [#192](https://github.com/ScottyMac52/DCS-IPIA/issues/192), dependencies [#193](https://github.com/ScottyMac52/DCS-IPIA/issues/193).

## Recorded source review — 2026-10-09

Codex inspected IPIA source commit `077965fdb2743adcdcbe89bfcfa9ed95539b06b6` and the pinned DCS-Common commit `0e9ed14a9189b712477f2c647a348da0d8af321b`. This is a source audit, not approval of an existing release artifact. The reviewed source has candidate version 0.0.0.5; an exact release decision must identify the actual candidate version, commit, artifact hashes and reviewer.

All 12 canonical hardware SVGs contain embedded raster images: 14 image occurrences in total. Their 12 draw.io sources contain the corresponding 14 occurrences; the Warthog throttle source is compressed and was decoded before inspection. No external HTTP image references were found in those SVGs. The pinned source-image files also accompany the shared asset inventory. Git blob identities and per-template findings are recorded in [distribution-artwork-review.json](distribution-artwork-review.json).

The software MIT license and pinned source hashes do not establish permission for the underlying images. SOURCES.md expressly retains manufacturer image copyrights. User-provided or annotated content likewise needs its underlying ownership recorded.

| Template | Current evidence | Remaining permission evidence |
| --- | --- | --- |
| MOZA AB9 | Manufacturer product/image URLs | Redistribution grant or replacement artwork |
| TM TPR | Manufacturer product/image URLs | Redistribution grant or replacement artwork |
| VKB F-14 Gunfighter | Issue #54 user-supplied views | Original photographer/author and redistribution permission |
| TM Warthog grip, throttle and MFD | Issue #103 Inkscape templates; throttle Blank2 attachment | Original artwork/image ownership and redistribution permission |
| F/A-18C grip | Annotated user-supplied JPEG | Original artwork/image ownership and redistribution permission |
| WINCTRL PTO2 | Owner confirms WINCTRL documentation; issue #87 annotated source | Manufacturer reuse terms or replacement artwork |
| OnYourTwelve PDCP | Scott McIntosh confirms he took the original photo | Credit Scott; record inclusion in final candidate decision |
| WINCTRL ICP | Owner confirms WINCTRL documentation | Manufacturer reuse terms or replacement artwork |
| Logitech quadrant | Pinned f4u-logitech-throttle-quadrant.png | Original artwork/image source and redistribution permission |
| Viper TQS and Mission Pack | Owner confirms Thrustmaster documentation | Manufacturer reuse terms or replacement artwork |

Scott supplied these source clarifications on 2026-10-09. His PDCP authorship resolves the unidentified-photographer gap; it does not imply permission for manufacturer documentation. Official source references located for the manual-derived images: [ICP V1.0](https://eu.winctrl.com/static/assets/files/en/ICP-User%20Manual%20EN%20V1.0.pdf), [PTO2 V1.1](https://winctrl.com/static/assets/files/en/PTO2-User%20Manual%20EN%20V1.1.pdf), and [Viper Mission Pack](https://ts.thrustmaster.com/download/accessories/Manuals/Viper/Viper_Mission_Pack_User_Manual.pdf). Exact original excerpt/version matching and reuse terms remain to be recorded; public availability of a manual is not treated as a reuse grant.

Issue numbers in this table refer to DCS-Common source history. No templates are removed or changed by this audit. Record permission evidence in #192 against the affected Git blobs; otherwise replace the affected art and update provenance. Ownership of annotations alone must not be assumed to cover the underlying photo.

**Current decision:** wider Community and commercial distribution remain pending these artwork decisions and the exact candidate dependency/native review. Payment/Pro implementation is deferred. A maintainer responsible for distribution must record the final decision in #183; this automated/mechanical review does not substitute for that decision.

## Dependency and runtime evidence

Write-DistributionNotices.ps1 collects notices from the actual restored application graph, including transitive/native and build packages. Entries with missing metadata or no collected notice files remain reviewRequired. A collected license is evidence, not an automatic approval of every embedded native component.

The former script copied LICENSE.txt and ThirdPartyNotices.txt from the installed SDK root. That did not establish correspondence with the self-contained runtime being shipped. The updated script reads exact win-x64 runtime versions from project.assets.json downloadDependencies and collects notice files from those exact Microsoft.NETCore.App and Microsoft.WindowsDesktop.App runtime packs. Missing packs, non-exact/conflicting versions, missing pack licenses or missing Core runtime third-party notices fail packaging. runtime-packs.json records the resolved identities and SHA256 of each copied notice. SDK-root notice files are never substituted.

The official NuGet packages inspected on 2026-10-09 provide these files:

| Package inspected | Included notice evidence |
| --- | --- |
| Microsoft.NETCore.App.Runtime.win-x64 10.0.12 | LICENSE.TXT; THIRD-PARTY-NOTICES.TXT |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 10.0.12 | LICENSE (the package has no separate third-party notice file) |
| SkiaSharp.NativeAssets.Win32 4.153.1 | LICENSE.txt; THIRD-PARTY-NOTICES.txt |

Sources: [Core runtime](https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/10.0.12), [Desktop runtime](https://www.nuget.org/packages/Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.12), [Skia native assets](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32/4.153.1). These inspections establish package contents, not which runtime version a future SDK resolves. The build inventory is authoritative for a candidate.

DISTRIBUTION-EVIDENCE.json accompanies the tested release artifacts and contains the actual package metadata/notice inventory, exact runtime notice hashes and unresolved-review flags. Complete #193 using this record and the notices in the portable payload:

- Review every reviewRequired package and applicable complete notices.
- Review Skia's native third-party notices against its packaged native assets.
- Identify the resolved SQLite native provider/version and its upstream notice/public-domain evidence; the managed Microsoft.Data.Sqlite license alone is insufficient evidence for native SQLite.
- Confirm the actual Core and Desktop runtime notices, bundled font notices and shared-asset notices match the candidate.
- Record reviewer, date, candidate version/commit, artifact hashes, decisions and restrictions. Hash comparisons establish packaging integrity, not permission or completeness.

## User-imported content

Import artwork only when you have permission to use it. Before sharing an exported module, drawing, snapshot or backup, review all included images and sources and retain evidence that permits that redistribution. A personal-use permission may have a narrower scope than sharing.

IPIA's importer validates file structure, geometry and integrity. It does not verify ownership or grant rights. Preserve attribution and restrictions required by the original source. Repository maintainers cannot clear user-imported content automatically.

## Closure of #183

The artwork/source inventory review and user-import expectations are now recorded. #183 remains open until unresolved artwork permissions/replacements, the exact release package/native/runtime review and the responsible maintainer's candidate-specific distribution decision are recorded. Do not treat this document, CI success or generated metadata as release approval.
