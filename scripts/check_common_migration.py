#!/usr/bin/env python3
"""Validate the pinned Common migration inventory and render its deterministic report."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = Path("docs/migration/dcs-common-capabilities.json")
REPORT = Path("docs/migration/dcs-common-capabilities.md")
STATES = {"implemented-parity-pending", "migration-pending", "preserve-reference"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe_path(path):
    require(isinstance(path, str) and path and "\\" not in path and ":" not in path
            and all(part not in {"", ".", ".."} for part in path.split("/"))
            and not any(ord(char) < 32 for char in path), f"Unsafe path: {path!r}")
    return path


def git_blob(content):
    # Git object interoperability only; package and inventory digests use SHA-256.
    return hashlib.sha1(f"blob {len(content)}\0".encode() + content, usedforsecurity=False).hexdigest()


def digest(data):
    canonical = json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(canonical.encode()).hexdigest()


def validate(data, root, source_root=None):
    """Require exhaustive ownership, explicit pending gates and existing target/bundle evidence."""
    require(set(data) == {"schemaVersion", "sourceRepository", "sourceCommit", "ipiaBaseline",
                         "bundledSourceCommit", "files", "capabilities", "knownGaps"}, "Unknown inventory fields")
    require(data["schemaVersion"] == 1, "Unsupported inventory schema")
    require(data["sourceRepository"] == "ScottyMac52/DCS-Common", "Unexpected source repository")
    for key in ("sourceCommit", "ipiaBaseline", "bundledSourceCommit"):
        require(isinstance(data[key], str) and re.fullmatch(r"[0-9a-f]{40}", data[key]), f"Invalid {key}")
    require(isinstance(data["files"], list) and data["files"], "Empty source inventory")
    files = {}
    for entry in data["files"]:
        require(set(entry) == {"path", "mode", "gitBlob", "size"}, "Unknown source file fields")
        path = safe_path(entry["path"])
        require(path not in files, f"Duplicate source: {path}")
        require(entry["mode"] in {"100644", "100755"}, f"Unsupported source mode: {path}")
        require(isinstance(entry["gitBlob"], str) and re.fullmatch(r"[0-9a-f]{40}", entry["gitBlob"]), f"Invalid blob: {path}")
        require(type(entry["size"]) is int and entry["size"] >= 0, f"Invalid size: {path}")
        files[path] = entry
        if source_root is not None:
            actual = source_root / path
            require(actual.is_file(), f"Source checkout file is missing: {path}")
            content = actual.read_bytes()
            require(len(content) == entry["size"] and git_blob(content) == entry["gitBlob"], f"Source checkout differs: {path}")
    require(list(files) == sorted(files), "Source files must be ordered by exact path")
    require(isinstance(data["capabilities"], list) and data["capabilities"], "Empty capability inventory")
    assigned = {}
    ids = set()
    for capability in data["capabilities"]:
        require(set(capability) == {"id", "title", "status", "owner", "sourcePaths", "targetEvidence", "current", "exitCriteria"}, "Unknown capability fields")
        identity = capability["id"]
        require(isinstance(identity, str) and re.fullmatch(r"[a-z0-9-]+", identity) and identity not in ids, "Invalid or duplicate capability ID")
        ids.add(identity)
        require(capability["status"] in STATES, f"Unsupported status (parity cannot be inferred): {identity}")
        for key in ("title", "owner", "current", "exitCriteria"):
            require(isinstance(capability[key], str) and capability[key].strip(), f"Missing {key}: {identity}")
        require(isinstance(capability["sourcePaths"], list) and capability["sourcePaths"], f"No sources: {identity}")
        require(capability["sourcePaths"] == sorted(capability["sourcePaths"]), f"Sources are unordered: {identity}")
        for path in capability["sourcePaths"]:
            safe_path(path)
            require(path in files, f"Unpinned source: {path}")
            require(path not in assigned, f"Source has multiple owners: {path}")
            assigned[path] = identity
        require(isinstance(capability["targetEvidence"], list) and capability["targetEvidence"], f"No target evidence: {identity}")
        for path in capability["targetEvidence"]:
            require((root / safe_path(path)).is_file(), f"Target evidence is missing: {path}")
    require(set(assigned) == set(files), "Unclassified sources: " + ", ".join(sorted(set(files) - set(assigned))))
    require(isinstance(data["knownGaps"], list), "Invalid known gaps")
    gap_ids = set()
    for gap in data["knownGaps"]:
        require(set(gap) == {"id", "evidence", "missingPath", "description"}, "Unknown gap fields")
        require(gap["id"] not in gap_ids, "Duplicate gap ID")
        gap_ids.add(gap["id"])
        require(gap["evidence"] in files and safe_path(gap["missingPath"]) not in files, "Gap differs from pinned source inventory")
        require(isinstance(gap["description"], str) and gap["description"].strip(), "Missing gap description")

    bundle = root / "data/shared-assets"
    provenance = json.loads((bundle / "provenance.json").read_text(encoding="utf-8"))
    require(provenance["sourceCommit"] == data["bundledSourceCommit"], "Bundle source pin changed; refresh the migration inventory")
    drift = []
    bundled = set()
    for item in provenance["files"]:
        path = safe_path(item["path"])
        require(path not in bundled, f"Duplicate bundled path: {path}")
        bundled.add(path)
        actual = bundle / path
        require(actual.is_file(), f"Bundled file is missing: {path}")
        content = actual.read_bytes()
        require(len(content) == item["size"] and git_blob(content) == item["gitBlob"], f"Bundled file differs from provenance: {path}")
        source_path = "LICENSE" if path == "DCS-Common-LICENSE.txt" else path
        entry = files.get(source_path)
        status = "missing" if entry is None else "unchanged" if entry["gitBlob"] == item["gitBlob"] else "changed"
        drift.append((source_path, status))
    require(bundled, "Empty asset bundle")
    return assigned, sorted(drift)


def md(value):
    return value.replace("|", "\\|").replace("\n", " ")


def render(data, assigned, drift):
    """Generate the same report bytes on every machine, without timestamps or network reads."""
    unchanged = sum(status == "unchanged" for _, status in drift)
    source_url = f"https://github.com/{data['sourceRepository']}/blob/{data['sourceCommit']}/"
    lines = ["# DCS-Common capability migration — Step 4", "",
             "Generated by `python3 scripts/check_common_migration.py --write-report`. Edit the JSON inventory, then regenerate.", "",
             f"- Source repository: `{data['sourceRepository']}` at `{data['sourceCommit']}`.",
             f"- IPIA implementation baseline: `{data['ipiaBaseline']}` (merged #117).",
             f"- Bundled source pin: `{data['bundledSourceCommit']}`.",
             f"- Complete pinned source tree: **{len(data['files'])} files**, each with a Git blob ID, size and one primary owner.",
             f"- Inventory SHA-256: `{digest(data)}`.", "",
             "## What this proves", "",
             "The inventory covers every blob in the retrieved, untruncated Common tree. CI checks exhaustive classification, target evidence paths, bundled asset bytes/provenance and this report's deterministic contents. It does not query GitHub, authenticate a remote tree or establish behavior/visual/package parity. An optional `--source-root` checks every inventoried source file against a checkout of the pinned Common commit; it does not discover newer commits or unlisted files. Refresh the complete tree before changing the source pin.", "",
             "Production generation uses the IPIA bundle and frozen workspace; this audit adds no runtime Common checkout/download dependency. Existing consumer builds and repository histories remain operational. No capability is marked parity-complete and no consumer is retired by this report.", "",
             "## Shared asset drift", "",
             f"**{unchanged} of {len(drift)} bundled files** match the current pinned Common blobs exactly, including the renamed MIT license. The bundle's earlier commit is retained; matching asset bytes do not mean the remaining Common authored code has migrated.", ""]
    changed = [(path, status) for path, status in drift if status != "unchanged"]
    lines += ["| Source path | State |", "| --- | --- |"]
    lines += [f"| `{md(path)}` | {status} |" for path, status in changed] if changed else ["| All bundled paths | unchanged |"]
    lines += ["", "## Capability ownership and gates", "",
              "Each source has one primary inventory owner; a shared script may support several behaviors. IPIA evidence links demonstrate available implementation boundaries, not measured equivalence. Legacy tests and examples remain characterization inputs.", "",
              "| Capability | Files | State | IPIA owner |", "| --- | ---: | --- | --- |"]
    for c in data["capabilities"]:
        lines.append(f"| {md(c['title'])} | {len(c['sourcePaths'])} | {c['status']} | {md(c['owner'])} |")
    for c in data["capabilities"]:
        lines += ["", f"### {c['title']}", "", c["current"], "", "Exit criteria: " + c["exitCriteria"], "", "IPIA evidence:", ""]
        lines += [f"- [{p}](../../{quote(p, safe='/')})" for p in c["targetEvidence"]]
    lines += ["", "## Source gaps found", ""]
    for gap in data["knownGaps"]:
        lines += [f"- **{gap['id']}**: {gap['description']} Evidence: [{gap['evidence']}]({source_url}{quote(gap['evidence'], safe='/')}); absent path `{gap['missingPath']}`."]
    lines += ["", "## Next reviewable slices", "",
              "1. Review snapshot-derived module documentation against pinned consumer golden cases, including per-device output differences. Reviewed JSON/Markdown instruction imports preserve explicit frozen guidance; approved integration-asset/configuration adapters remain pending.",
              "2. Review the implemented, independently verified Complete release against pinned consumer layouts and approved integration-asset requirements; deployable OvGME output stays separate from Documentation and snapshots.",
              "3. Internalize draw.io export/asset validation and remaining scaffold/export/version adapters with pinned legacy golden cases. Repair or deliberately replace dangling legacy entrypoints instead of presenting them as working IPIA commands.",
              "4. Refresh and pin consumer inputs/releases, starting with Huey, then compare every output file/profile/page and document every intentional difference. Verify clean rebuild and prior-snapshot rollback; record a per-module go/no-go before changing authoritative workflows.", "",
              "Steps #83/#9 remain open. Positive user feedback on summary/module authoring is recorded separately from in-game hardware acceptance (#111), full consumer parity and retirement approval. Do not infer manual DCS success from CI.", "",
              "## Full source inventory", "", "| Source at pinned commit | Primary capability | Git blob | Bytes |", "| --- | --- | --- | ---: |"]
    for item in data["files"]:
        path = item["path"]
        lines.append(f"| [{md(path)}]({source_url}{quote(path, safe='/')}) | {assigned[path]} | `{item['gitBlob']}` | {item['size']} |")
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--check", action="store_true")
    action.add_argument("--write-report", action="store_true")
    parser.add_argument("--source-root", type=Path, help="Optional checkout of the pinned Common commit")
    args = parser.parse_args(argv)
    try:
        data = json.loads((ROOT / INVENTORY).read_text(encoding="utf-8"))
        assigned, drift = validate(data, ROOT, args.source_root)
        report = render(data, assigned, drift)
        if args.write_report:
            (ROOT / REPORT).write_text(report, encoding="utf-8", newline="\n")
        else:
            require((ROOT / REPORT).read_text(encoding="utf-8") == report, "Migration report is stale; regenerate with --write-report")
        print(f"Checked {len(assigned)} pinned Common files, {len(data['capabilities'])} capabilities and {len(drift)} bundled paths; parity remains pending.")
        return 0
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"Migration inventory failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
