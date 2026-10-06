# Consumer importer risk reduction

The initial coverage report measured `ConsumerSourceImporter.Import` at
cyclomatic complexity **220**, line coverage **90.68%**, and CRAP **259.1826**.
Coverage alone cannot solve that concentration of logic: with 100% coverage,
the CRAP formula still yields the method's complexity, **220**.

The importer now orchestrates explicit stages for request validation, source
collection, JSON parsing, module/scaffold selection, device profile identity,
modifier validation, kneeboard projection, UI Layer utilization, and provenance.
Profile and UI metadata stages delegate their separate validation decisions to
small helpers; supported-path recognition is divided by source family.

This is an extraction of existing behavior, not a change to the module format.
The public interface, generic module identity handling, canonical scaffold
precedence, stable IDs, binding order, diagnostic codes/order, warnings,
read-only source retention, SHA-256 provenance, and all-or-nothing results remain
the same. Helpers return local values; the importer publishes its result only
after every stage succeeds. There is no new database or filesystem access.

Regression tests cover invalid schema/shape failures, unsafe paths, release
source recognition, explicit UI usage, matching instances, upfront diagnostic
ordering, and complete result equivalence for reversed source order. Existing
real-module tests continue to cover repeated devices, modifiers, overlay metadata,
inactive pages, canonical scaffold selection, SQLite round-trips, and Lua output.

Acceptance requires the full existing suite and independent **80% line / 80%
branch gates for every assembly**. Compare method metrics in the CI
`code-coverage-report` artifact's `report/crap-scores.json` rather than relying
only on the aggregate class coverage. Check the extracted methods as well as
`Import`; a smaller entry point must not hide one equally large helper.
Coverlet's complexity reflects IL branches, including compiler-generated LINQ
methods, so these values should be compared using the same collector and build.
