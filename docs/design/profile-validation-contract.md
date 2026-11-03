# Shared file-backed profile validation

PR6 adds mandatory `ValidationDefinition` and `Validate(profile, definition, cancellationToken)` members to `ISimProfileBackend`. DCS, Elite Dangerous, Squadrons and War Thunder implement the same contract. `GameProfileSession` validates loaded workspaces, staged edits and export plans before publishing them. Rejected edits leave both the current and saved workspace intact.

The shared Application engine evaluates versioned declarative rules. It does not parse native game formats or contain game-specific command cases. Each adapter retains its native evidence, preservation and semantic checks; War Thunder also exposes a normalized inspection snapshot for independent native range/parameter rules. Validation returns structured code, path, expected value, actual value, message and severity. Warnings preserve usable output; errors prevent a usable export plan. Cancellation propagates. Validation writes no files and never installs a configuration into a game.

## File contract

The [versioned definitions](../../data/validation/) are embedded Application resources. Every game file includes the [portable rules](../../data/validation/portable.v1.json) once through `includePortable`. Portable rules enforce identities, collection shapes and command references. Adapter files add evidence requirements; [War Thunder's file](../../data/validation/war-thunder.v1.json) adds non-overlapping device ranges and unique ownership of each assigned global input index. The [definition schema](../../data/schemas/profile-validation.v1.schema.json) documents the JSON format; the loader also checks cross-field constraints, finite numeric bounds and size limits.

Definitions select a camel-case inspection root:

| Root | Meaning |
| --- | --- |
| `/profile` | Loaded portable document, including adapter-owned `nativeData` |
| `/catalog` | Captured adapter command identities, types, contexts and metadata |
| `/native` | Optional adapter inspection snapshot; War Thunder exposes devices, inputs and typed command parameters |
| `/files` | Relative names in the native export plan |

Paths follow JSON Pointer escaping (`~1` for `/`, `~0` for `~`), with `*` selecting array elements or object properties. Rules support required/optional fields, primitive/container types, inclusive numeric bounds, length bounds, allowed scalar values, unique array identities and references to declared identities. Empty wildcard collections match no elements; require a nonempty parent with `minimumLength` when necessary. Range rules use half-open intervals `[offset, offset + count)`, reject invalid/overflowing bounds and overlap, and require exactly one owner per reference. The optional unbound sentinel defaults to `-1`.

War Thunder parameters can be constrained without adding code for each command. For example, a numeric rule at `/native/commands/axes~1pitch/*/parameters/kMul/*/value` validates every captured pitch multiplier. Repeated records and parameter occurrences remain arrays. Each parameter exposes `type`, decoded `value` and preserved `raw` literal. Unknown native syntax remains preserved and reported by the adapter; a rule file does not fabricate a complete inherited command dictionary.

External definitions are explicit inputs: read their text and call `ProfileValidationDefinitions.Parse`. Pass the result to the adapter or the optional `GameProfileSession` constructor argument. Unknown definition members and unsupported versions fail closed. Packaged rules are the normal session default; there is no new UI file picker or automatic rule download.

## Known-good comparison

`expectedFiles` is an optional dictionary of relative export names to SHA-256 digests of UTF-8 native output text. If supplied, validation checks the exact file set and contents, reporting missing, changed or unexpected files. This mode intentionally rejects legitimate binding changes too; it is useful for regression/reference comparisons. Normal editing uses schema/range rules without exact reference hashes. An imported source is never silently treated as an immutable known-good reference.

The rules provide independent structural and native-reference checks alongside existing adapter semantics. They do not establish game recognition or replace in-game acceptance. Native device reordering stays disabled in War Thunder; validation does not repair or renumber saved ranges.

## Local acceptance

Run `./tests/validate-pr6.ps1` from PowerShell 7 on Windows. It uses the existing independent **80% line / 80% branch / CRAP ceiling 50** gates, then the Windows smoke suite. No assembly exclusions or thresholds are relaxed. The contract tests cover all four adapters, file parsing, malformed definitions, required/type/bounds/reference rules, range arithmetic, typed native parameters, native failures, cancellation, exact reference comparison and atomic staged rejection.

The implementation environment has no .NET SDK or Windows runtime. Added .NET tests, formatting, WPF smoke and measured 80/80/50 results require local execution; source checks cannot certify those measurements. GitHub Actions are not part of this validation run.
