# Draw.io exporter parity

Source behavior: DCS-Common `f1c968e606414225db837e75426c0c930cc815c9`; source file identities and license are in [PROVENANCE.json](../../scripts/asset-authoring/PROVENANCE.json). Assets retain the existing bundled source pin `0e9ed14a9189b712477f2c647a348da0d8af321b`.

The original 12 outputs retain their published SVG bytes. Four IPIA-authored Sol-R layouts extend the catalog. All 16 published outputs reproduce their deterministic generated SVG bytes (LF newlines) exactly, twice, and when their graphs are compressed. This retains embedded raster bytes, dimensions, callout IDs, label text/anchors, connectors, masks, MFD category fields and footers. Identical SVG bytes establish no exporter-induced visual change; this does not establish whole consumer PNG/package parity or acceptance of newly edited artwork.

| Device | Source draw.io SHA-256 | Published/generated SVG SHA-256 | Comparison |
| --- | --- | --- | --- |
| moza-ab9 | `c41f05d8d4fc2bb002c8deb67926d4b353c9cc7edf4ec282dbce9f6bb796c9d0` | `167f21455228bd0294429a95091f391e6df939b89c66a2567ffe01d164bbb1d3` | Exact |
| vkb-f14-gunfighter | `4ce871d0cd9314606d9cf8592c15a1d31446040bd652c01e3dc5da783e8247b4` | `196e98dc30bba54c8bcd275fca1cdfcdfa0101700d103191567ae6c0879d5596` | Exact |
| tm-warthog-grip | `3c95800f3a09bcdab950c65c79522e62445c3b468708e0dd15b707b7b0237400` | `3c6149ac676959d2572729fc5f4c640b0180bdfdd2e90cd153325f1672bf6841` | Exact |
| grip-f18c | `31a2cbd578fb431c12c02e9a6d7e4c619a3759a5556ea9f05fd3e852dbd7cd4d` | `bcf1f0c62e0fad509d257c177e6a68b68844048f8f536176bd894137a3cd2206` | Exact |
| tm-mfd | `d64c72c8716d33f8642e6cb3c53720fa158932814014dd63e6b029249d624781` | `83fe26875e8007e9d0bde5617c4c29c910c1efb9780a6e7e15dc2fb2bfd4f9da` | Exact |
| onyourtwelve-pdcp | `01e8ae7e98d779453c1675fc1379cdcc67601c0a45715a5e6fe51077586874c3` | `399c1f6805293cc20cd7d9f3e81a3852cbf7fb954abb7e32ff5920bd11e97004` | Exact |
| winctrl-pto2 | `880c7461b7b42b9e0eea4748792aeb482e2bcbdea1f3f972a2604d700bba758f` | `8d664cf31ef13fdb6a8f1de4238719736f74f25ea42881a0246cb46d88d21b95` | Exact |
| winctrl-icp | `cbf64db730a421926c7492bef8857d33e6e588ee9ef1c03c6d6dd26f30154101` | `625b1983b3170e0c719eefc877a1e18d23c33cdbd716dfc50c3ddf1fd91688a0` | Exact |
| tm-tpr | `699c6526744b18d2c01bef46453bb213e394d97ac58bcf140b911e9bab155041` | `942b1aa95226bd5aa35279be04b4f84069132d716e54c8573728f700df9b37ac` | Exact |
| tm-warthog-throttle | `068258b329ade81b72595c0d95d33b52977fde1bbcf571aa81d1638ed0715e39` | `38e64e6be0a091e97fe93683c5ecc369cd190d9685ab5fed05c0c2db85b082ce` | Exact |
| logitech-throttle-quadrant | `ced31703347f0b8d7710635f1d5dda493d262e6543ab67ca8e80a9287c8b21f5` | `83cdd3656d4e0b0ad53fb9859471bc7e136df282a2a68c29fd6f23ae916f814c` | Exact |
| viper-tqs-mission-pack | `a0ade4270443ded143df8aa5f8dc1c4da3e6c00c9ec3cfdc49d27f54830b03fd` | `a2373b29b21a85abcf94fb55992295740b1f6ebe29272d86fb939f256d07f1b8` | Exact |

| tm-sol-r-ava-r-pov | `4c008396fe3297a3f9877a93596652995529aa1e90e7dfaf49698395e8562d3a` | `691e6b41790e3093098ff174fbdbb252d883c8417d3e3e8df00915b2e4517976` | Exact (IPIA-authored; mini-stick RX/RY) |
| tm-sol-r-ava-r-buttons | `dbbe92573c662cb6f88f4be8713e35a38e65882d21d3f3410ab0743d95e3bbd2` | `7c58f429c38556dfbbfb1090ea811114992f85c3fea522cf0b28e42a642e9aef` | Exact (IPIA-authored; mini-stick RX/RY) |
| tm-sol-r-ava-l-pov | `b456ff338bb6ffe2d98a5f2cd6cac4f18a1e1d251cc2918467428207c4aed86a` | `953761faed2de48c01a64d04a06c821046ab33efb4d21882a45b5310293df967` | Exact (IPIA-authored) |
| tm-sol-r-ava-l-buttons | `a78a9774a5cc3bd2813a90b01d1d593f74cfd6de4df63472667666526c86a142` | `6b51f49f5bdc3e6cb5462c356396d1524bd3daca6915dd322fd1972039f551a2` | Exact (IPIA-authored) |

## Intentional behavior changes

- IPIA owns the exporter source; authoring does not download or execute Common. It uses the bundled inputs by default and has no npm dependencies.
- CLI defaults to read-only verification. Generation requires a new explicit output folder, validates all sources first and protects existing folders and bundled assets. Native and Windows path escape attempts and duplicate case-insensitive targets are rejected.
- Ambiguous multiple diagrams, duplicate IDs, invalid rendered geometry and incomplete/noncanonical embedded images fail rather than silently producing bad output. UserObject-wrapped connector IDs are resolved like label IDs.
- Optional watermarks remain opt-in; their mode is tested separately from clean default golden SVGs.

## Reproduce

Run `node --test tests/asset-authoring/*.test.mjs` and `node scripts/asset-authoring/export-hardware.mjs --check` from the repository root with Node 24. CI on Linux and Windows adds independent 80% line and branch coverage for each of the four exporter modules. Existing .NET per-layer gates remain in place. Source creation and hardware graphic editing use desktop draw.io. Legacy repository scaffold/version/workflow generation is outside the agreed application completion scope. Existing IPIA creation/import, shared revision adoption/impact and package workflows replace the application-facing behavior. See [application completion](application-completion.md) and [Huey release acceptance](huey-release-acceptance.md).
