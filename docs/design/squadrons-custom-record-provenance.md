# Squadrons blank-slate command metadata

The blank-slate contract is: a version-four profile with controller declarations
and **zero binding records** can assign every known catalog command inside
HEIMDALL. No command must first be assigned in the game, and no personal template
import is required. Unknown imported concepts still retain their captured rules.

## Evidence and representation

The original bundle held 95 nonzero-identifier factory joystick records. The
earlier complete capture supplied on 2026-10-29 also contains a custom
`IncomStarshipInputConcepts.ConceptShieldFront.1` joystick record with
`identifier 0`, `modifier 0`, `altbutton 0`, `negate 0` and `type 3`. The later
`SOL-R.PITCH` capture omits that custom record; it cannot be the sole source for
all known command creation metadata.

The supplied version-four capture contains 15 zero-identifier custom joystick
records. An independent published version-two profile contains 28 custom joystick
records with
the same zero-identifier representation. The two captures overlap on 13 custom
records; together they provide 30 observed custom descriptors, including Shield Front, Shield Back,
Shield Balance, engine/shield/weapon power actions, targeting, freelook and chat:

- [Pinned profile in MetaRefCard test data](https://github.com/ankurkotwal/metarefcard/blob/b1545ca3e260001e39ff4f724c0a4e94c3d0f267/testdata/sws/Saitek_Pro_Flight_X-55_Rhino.profile)
- Profile blob: `45f227e063d6c196cb5b3d1e444159cc90383eb3`.
- Its [README](https://github.com/ankurkotwal/metarefcard/blob/b1545ca3e260001e39ff4f724c0a4e94c3d0f267/README.md)
  identifies `testdata` as sample game input files.

The bundle now contains 149 descriptors covering all 125 known concepts:

| Descriptor source | Records | Treatment |
| --- | ---: | --- |
| Existing factory capture | 95 | Preserve native indices, identifiers and negate fields |
| Observed custom joystick records across both captures | 30 | Preserve zero identifiers and native indices |
| Additional known digital concepts | 24 | Apply the observed custom-record format at the next concept-local index after its captured default records |

The last row is a generalization of the observed custom-record representation;
it does **not** claim those 24 joystick assignments were all present in the
published profile. The eight comm-menu reactions have index-zero keyboard
defaults in the supplied `SOL-R.PITCH` capture, so their custom descriptor index
is one. No keyboard/mouse identifier is copied into a custom joystick record.
The older published sample corroborates the representation; it is not evidence
that every version-four command was bound there. Actual game acceptance of the
expanded command set still requires a Squadrons run.

Device references in bundled descriptors are placeholders. They and input codes
are replaced by the explicit assignment target before any record becomes active.
Only the requested record or axis pair is materialized. Inspecting the catalog,
importing or exporting an unchanged profile never creates active assignments.
No controller names or personal scalar settings are included.

## Factory choice versus user evidence

Some factory commands contain alternate physical bindings or multiple native
axis families. Without user joystick evidence, normal authoring chooses the
first complete factory axis family, or a canonical digital record ordered by
negate and native index. Both identifiers of an axis remain intact.

This preference is limited to factory metadata. Existing and archived user
joystick records retain the original ambiguity checks; imported partial pairs
and fixed factory addresses are not silently repaired or overwritten. If a custom
zero-identifier descriptor collides with an existing system record, it appends at
the next concept-local address, preserving the system record.

## Regression contract

`SquadronsBlankSlateTests` loops over all 125 known commands from the empty-table
fixture, stages each through a verified session, verifies that only its requested
record(s) are exported, round-trips the export, and discards back to the original.
Shield Front/Back/Balance additionally check their zero identifiers, persistence,
clear and reassignment. Windows fresh-workspace smoke assigns Shield Front first
and Pitch second with no template import, then saves and reopens the workspace.

These checks validate HEIMDALL's native/session behavior. They do not substitute
for game acceptance or establish support for unknown future command concepts.
