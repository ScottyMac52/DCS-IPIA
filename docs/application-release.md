# Tested application releases

Every PR/main CI run builds a self-contained Windows candidate using the four-component version in `release/version.json`. Independent jobs run .NET quality gates, Windows solution/tests/rendered WPF smoke, documentation/layout/theme/inventory and asset-authoring checks. The candidate job verifies its exact executable, installer and portable payload; passing that job alone does not replace the other required checks.

The executable initializes isolated data, checks shipped hardware and native PNG rendering, and exercises saved bundled/generic/custom fixtures. The packaging script installs setup.exe into a temporary folder, checks installed payload hashes, reopens/reinstalls saved fixture data, uninstalls, then extracts and starts the portable ZIP. Snapshot/native Lua/decoded PNG comparisons and setup-state preservation are automated. Candidate artifacts include TEST-EVIDENCE.json, DISTRIBUTION-EVIDENCE.json and SHA256SUMS.txt.

## Version and notes contract

`release/version.json` and the first line of `release/RELEASE-NOTES.md` must name the same application version. The documented baseline is currently 1.1.1.0. A candidate built from a later commit with that baseline number is a CI candidate, not the already-published v1.1.1.0 artifact; always identify its full source commit and checksums.

Before publishing a new version:

1. Update the four-component version and write accurate notes headed `# DCS-IPIA vX.X.X.X` in a reviewed PR. Describe Free/Owner availability, user-visible changes and known limitations. Do not copy a historical release description unchanged.
2. Run `python scripts/check_documentation.py` and the appropriate [local checks](../tests/README.md). CI also checks local guide links and the matching notes/version. Keep the independent 80/80/50 and Windows/package checks enabled.
3. Merge the release request to main. A version-file change triggers the Release workflow. It revalidates notes against the actual requested version and runs its quality, Windows and inventory gates before building/verifying and publishing the exact assets.
4. Explicit `vX.X.X.X` tag pushes remain supported, but the tag must match both the source version file and release-note heading. A mismatched tag fails before packaging/publication.
5. Record artifact identities, hashes, actual acceptance results and the maintainer decision using [release acceptance](release-acceptance.md). Publication does not supply missing physical-controller or distribution evidence.

A main-branch request naming an already-published version succeeds without packaging or publishing again. This permits correcting the documented baseline without replacing a release. Existing tag-triggered releases and un-released existing tags remain protected from replacement. Bump the version and notes together for another release. This documentation PR does not request a new application release.

## Published release descriptions

Earlier release pages reused notes headed v0.0.0.5, including now-closed migration issues. The corrected source notes describe v1.1.1.0; they do not retroactively describe older tags. The workflow checks prevent version-mismatched notes in future publication, but they cannot prove the factual completeness of a changelog.

Repository commits do not edit existing GitHub Release descriptions. To correct the current page separately, a maintainer with GitHub CLI access can run:

```powershell
gh release edit v1.1.1.0 --repo ScottyMac52/DCS-IPIA --notes-file release/RELEASE-NOTES.md
```

For an older page, review that exact tag's changes and supply its own notes file. Retain tags, binaries, checksums and historical fixture documents. Do not paste the latest release's changes into every older description.

## Diagnostic and acceptance boundaries

`--release-smoke <isolated-folder>` is the explicit packaged-app diagnostic option. It writes evidence in its own folder and exits; normal startup uses the selected user's data. Synthetic generic/custom fixture acceptance uses controlled test infrastructure and does not grant normal Free users access to reserved workflows.

Notice collection captures the resolved dependency graph, available full notices, exact runtime packs and artwork/font provenance. Hashes establish packaged identity, not notice completeness or image redistribution permission. Real hardware/DCS, older-release upgrades, display/accessibility and final distribution decisions remain separately recorded in [distribution readiness](distribution-readiness.md) and [source review](distribution-review.md).
