# Phone spec presets

Reference `spec_json` documents for `generate_phone_from_spec` (and `parse_spec` /
`set_parameters`). Each file is a COMPLETE, validated spec that exercises the full
generation surface — pass its contents verbatim as the `spec_json` argument, or say
to the LLM: "generate the iphone-like preset from Examples/presets".

| Preset | Exercises |
|---|---|
| `iphone-like.json` | Flat back, rounded-rect camera plateau (36x36 r10) with a 3-lens triangle, display pocket + front punch-hole, USB-C through the bottom flank, 2 flank antenna slits, mic/sensor pinholes, final top fillet. |
| `galaxy-like.json` | Gently CURVED back (bulge 0.6), tall 3-lens vertical camera island, back-face speaker grille (blind, pierces the tray floor), edge chamfer, flank USB-C, side button recess, pinhole. |

Conventions (see `generate_phone_from_spec`'s description for the full schema):

- All lengths mm; plan is XY-centred; the display side is z = thickness (top),
  the camera back is z = 0; a curved back bulges on +Z above the top.
- Camera `lenses[]` offsets are relative to the camera centre and must fit inside
  the plateau footprint.
- `front_punch` must sit inside the display pocket when one is enabled.
- Every spec here passes `PhoneParameters.Validate()` with ZERO warnings — gate
  g19 regenerates both presets end-to-end and asserts the per-stage log.

These presets supersede the originally planned C# "pattern library"
(`Generation/Patterns/`): with the full spec surface exposed over MCP, reusable
composition lives in JSON documents the LLM can read, patch (`set_parameters`),
and combine — no compiled pattern units needed.
