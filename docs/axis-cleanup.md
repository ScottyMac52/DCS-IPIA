# Aircraft axis cleanup

Modules capture the native filenames of connected controllers and existing Saved Games profiles during creation. The list includes units that were not selected as authored devices. No artwork, kneeboard page or assignment is added for those units.

Opening an module, or refreshing Devices, stages newly observed filenames for cleanup. **Save to database** retains them; Discard leaves saved state unchanged. Offline identities are retained on later scans. Generate the package after saving. Controllers first connected after that scan need a refresh and a new package.

**Fly Now!** also captures connected controllers, existing Saved Games profiles and authored native units before deploying any created or imported module. It saves these identities to the database first, including when an imported module is deployed directly from the Modules screen without opening its editor. The generated files and deployment journal include cleanup-only profiles, so Clear also removes those deployed files.

The saved command catalog, aircraft assignments and imported aircraft axis bindings supply exact aircraft command IDs. A separate catalog is not required for commands already present in saved assignments or bindings. Generation emits removal entries for unassigned base combinations of each known axis command and the eight native DirectInput axis keys. On an unused controller the resulting profile contains only removals. This suppresses default bindings even when they are not included in an imported diff. Exact active command/input/chord combinations and tuning remain intact; shifted assignments do not preserve an unwanted base default on the same physical input.

No aircraft command IDs are guessed. Load the aircraft's complete command catalog to cover its axes. Cleanup is limited to captured native units and known commands; an unobserved controller or command absent from the catalog cannot be covered. Existing per-device cleared-axis imports remain useful for other default chords and additional explicit evidence.

The controller list is pinned in module sources and travels through SQLite, portable snapshots and Complete/OvGME exports. Builds never rescan the local machine. Managed native filenames take precedence over stale discovery spellings for the same physical GUID. Keyboard bindings and module-owned UI Layer profiles remain separate. Existing snapshots without a captured cleanup list retain their original output.

After installing a regenerated package, check DCS **Axis Commands**: only the controllers and combinations explicitly assigned in the module should remain for covered commands. For the reported F-15E setup, check MOZA pitch/roll, TPR rudder/brakes and Warthog thrust left/right, and verify that quadrants, ICP, PDCP and other unused units have no inherited flight axes.
