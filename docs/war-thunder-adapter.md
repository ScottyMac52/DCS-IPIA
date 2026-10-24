# War Thunder adapter foundation

This isolated project is a starting point for War Thunder `.blk` support. It does not register a simulator, change existing adapters, or write user configuration.

## Safety gates

- Preserve all unknown BLK properties and original bytes on no-op round trips.
- Verify numeric fast/long/double-tap activation codes before interpreting them.
- Verify device offsets and global axis/button indices before assigning controls.
- Preserve advanced axis tuning fields, including unknown parameters.
- Add fixture tests, guarded editing/export, and regression tests for DCS, ED and Squadrons before integration.

**Draft foundation only — not a finished War Thunder adapter.**
