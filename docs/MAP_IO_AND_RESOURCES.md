# Map IO And Resource Loading

This document summarizes current DBuilder map IO and resource loading behavior. It is a behavior guide, not a claim of full Ultimate Doom Builder parity. Remaining gaps are tracked in `docs/TODO.md` and `docs/PARITY_MATRIX.md`.

## Supported Map Containers

DBuilder can discover and load maps from:

- WAD archives.
- PK3-family archives, including nested WAD maps.
- Directory resources.
- Clipboard-style map streams used by editor copy and paste flows.

Configured game data controls which map markers and map lump names are recognized. DBuilder avoids treating non-map lumps with map-like names as replacement targets during save-back operations.

## Binary And UDMF Formats

Current map IO covers Doom, Hexen, and UDMF map representations.

- Doom-format loading preserves unsigned binary ids, references, and thing flags above signed-short range.
- Hexen-format loading and writing preserve action arguments and Hexen-specific map data.
- UDMF loading preserves unknown top-level collections and custom fields.
- UDMF writing emits elements in UDB-style order and preserves supported custom field types.
- Format conversion clears or folds UDMF-only fields when writing to binary formats.

Invalid or near-zero linedefs and invalid sidedef references are filtered where UDB does the same. Referenced sidedefs are materialized from valid linedef references instead of kept as standalone orphans.

## Save-Back Behavior

Save-back behavior is intentionally conservative:

- Existing map blocks can be replaced, inserted, or renamed through configured map marker rules.
- Duplicate target map blocks are removed before replacement.
- Required config map lumps are created when missing.
- Configured script and blind-copy lumps are preserved.
- Existing Hexen `BEHAVIOR` bytes are preserved during save-back when required.
- Save-back is blocked when a rename target already exists.
- Map marker names are validated against configured map-lump names.

Nodebuilder output is accepted only when required output lumps are present. Temporary nodebuilder-generated lumps are cleaned up before writing final map data.

## Resource Stack Inputs

DBuilder resource loading supports:

- WAD resources.
- PK3-family archives.
- Nested PK3-family archives.
- Directory resources.
- Mixed resource stacks with configured priority rules.

Resource options cover root PK3 texture and flat exposure, WAD strict patch behavior, map resource warnings, required archives, and test-exclusion defaults.

## Resource Resolution Rules

Resource loading follows UDB-style precedence where implemented:

- Later GLDEFS resources override earlier actor light associations.
- Nested WAD flats, textures, sprites, and voxels can override folder resources within a PK3.
- Root PK3 files are preferred for singular text resources where UDB prefers them over nested WAD data.
- Directory and PK3 resources honor configured ignored directory names and ignored extensions.
- Files with UDB-unsupported path characters are skipped for PK3 and directory resources.
- Long PK3 namespace file titles can match classic 8-character prefixes.

Palette and colormap behavior includes gray fallback PLAYPAL data, active main colormap resolution, named colormap lookup, and indexed-image generation through the active palette.

## Known Gaps

- Full UDB DataManager parity remains incomplete.
- Lazy and threaded resource loading remains incomplete.
- Broader real-world map format round-trip coverage remains incomplete.
- Golden-file WAD output tests remain incomplete.
- Representative copyrighted IWAD, PWAD, and PK3 assets are not committed.

Use synthetic fixtures and repo-owned data for automated tests. Use local game assets only for manual QA.
