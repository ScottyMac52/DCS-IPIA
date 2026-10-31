# Coverage and CRAP gates

The pinned shared `reusable-dotnet-ci.yml` remains responsible for restore, format,
build, test, collecting Cobertura/OpenCover/TRX, and Coverlet's existing minimum 80/80 gates.
The consumer `test-reports` job uses those artifacts; it does not rerun .NET tests.

Like ProjectHopeLynden, reporting now checks each expected production assembly independently:
Domain, Application, Infrastructure, Dcs, EliteDangerous, Squadrons, WarThunder, Rendering, and Packaging.
ReportGenerator emits merged Cobertura and HTML from the Linux OpenCover artifact.
Missing, duplicate, empty or invalid packages fail; measured assemblies without branch points
show N/A and still require 80% line coverage. Raw percentages, rather than rounded displayed values, determine pass/fail.

A second gate applies ProjectHope's Razor ceiling of **CRAP <= 50** to every measured
method in those same nine assemblies using the existing OpenCover metrics:
`complexity² × (1 − method line coverage fraction)³ + complexity`.
Fully covered complexity 51 still fails. Generated methods remain included;
the App/WPF assembly is outside the provider's current coverage scope and is covered by the existing Windows smoke suite.
This is a new enforcing gate: existing hotspots may block merging until code or tests improve.
Thresholds and the shared workflow pin are not weakened.

The job publishes `QualityGateSummary.md`, console diagnostics and an Actions summary
with independent PASS/FAIL rows and every method exceeding the ceiling.
HTML, raw data, and searchable method CRAP metrics continue to be uploaded even when gates fail.
Missing artifacts fail explicitly. Coverage is measured only from the Linux run;
Windows TRX results remain separately reported.

Run gate regression tests with:
```powershell
python -m unittest discover -s tests/ci -p "test_*.py"
```

After downloading and extracting the `code-coverage-report` artifact:
```powershell
python scripts/assert_quality_gates.py --coverage TestResults/report/Cobertura.xml --raw TestResults/raw/linux --output TestResults/report --summary TestResults/local-summary.md --crap-ceiling 50
```

The existing `publish_test_results.py` still calculates and displays all method metrics;
`assert_quality_gates.py` owns enforcement and uses that same reader/formula.
