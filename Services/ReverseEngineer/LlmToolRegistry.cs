using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Stage 5: LLM tool registry — exposes ModificationService primitives as
    /// Anthropic Claude API "tool definitions" (name + description + JSON Schema
    /// input).
    ///
    /// Each ToolDef.InputSchemaJson is a self-contained JSON Schema (object form
    /// with "type", "properties", "required") matching the Anthropic Messages API
    /// "input_schema" field. We hand-roll the JSON to avoid taking a dependency on
    /// Newtonsoft.Json (FeatureGraphJsonWriter convention).
    ///
    /// Consumers:
    ///   - LlmToolDispatcher  : dispatches "tool_use" JSON requests to primitives.
    ///   - (future)            : a thin Anthropic API client that hands the schema
    ///                           list to Claude as the "tools" parameter.
    /// </summary>
    // @lat: [[reverse-engineer#LlmToolRegistry]]
    public static class LlmToolRegistry
    {
        /// <summary>
        /// One tool definition for the LLM (name + description + JSON schema body).
        /// Kept as a struct so we don't allocate heap entries per registry init.
        /// </summary>
        public struct ToolDef
        {
            public string Name;
            public string Description;
            public string InputSchemaJson;

            public ToolDef(string name, string description, string inputSchemaJson)
            {
                Name = name;
                Description = description;
                InputSchemaJson = inputSchemaJson;
            }
        }

        /// <summary>
        /// Full list of tools the LLM can invoke. Order matches the dispatcher's
        /// switch table for readability — keep them in sync when adding new ops.
        /// </summary>
        public static List<ToolDef> GetAllTools()
        {
            var tools = new List<ToolDef>
            {
                // ---- 5 "change" primitives ----------------------------------
                new ToolDef(
                    "change_wall_thickness",
                    "Change the thickness (mm) of a wall identified by its ID (e.g. W1). The wall is a pair of parallel planar faces in the FeatureGraph; only face_a is offset to avoid topology breakage.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"wall_id\": {\"type\": \"string\", \"description\": \"Wall id from FeatureGraph (e.g. W1)\"}, " +
                      "\"new_thickness_mm\": {\"type\": \"number\", \"description\": \"Target wall thickness in mm (> 0)\"}" +
                    "}, \"required\": [\"wall_id\", \"new_thickness_mm\"]}"),

                new ToolDef(
                    "change_hole_diameter",
                    "Change the diameter (mm) of an existing hole identified by its ID (e.g. H1).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"hole_id\": {\"type\": \"string\", \"description\": \"Hole feature id from FeatureGraph\"}, " +
                      "\"new_diameter_mm\": {\"type\": \"number\", \"description\": \"Target hole diameter in mm (> 0)\"}" +
                    "}, \"required\": [\"hole_id\", \"new_diameter_mm\"]}"),

                new ToolDef(
                    "change_fillet_radius",
                    "Change the radius (mm) of a fillet chain (e.g. FC1). All fillet faces in the chain are offset; sign is auto-handled per face concavity.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"fillet_chain_id\": {\"type\": \"string\", \"description\": \"FilletChain id (e.g. FC1)\"}, " +
                      "\"new_radius_mm\": {\"type\": \"number\", \"description\": \"Target fillet radius in mm (>= 0)\"}" +
                    "}, \"required\": [\"fillet_chain_id\", \"new_radius_mm\"]}"),

                new ToolDef(
                    "change_boss_diameter",
                    "Change the outer diameter (mm) of a boss identified by its ID (e.g. B1).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"boss_id\": {\"type\": \"string\", \"description\": \"Boss feature id from FeatureGraph\"}, " +
                      "\"new_diameter_mm\": {\"type\": \"number\", \"description\": \"Target boss diameter in mm (> 0)\"}" +
                    "}, \"required\": [\"boss_id\", \"new_diameter_mm\"]}"),

                new ToolDef(
                    "change_boss_height",
                    "Change the height (mm) of a boss along its axis by offsetting the boss top planar face.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"boss_id\": {\"type\": \"string\", \"description\": \"Boss feature id from FeatureGraph\"}, " +
                      "\"new_height_mm\": {\"type\": \"number\", \"description\": \"Target boss height in mm (> 0)\"}" +
                    "}, \"required\": [\"boss_id\", \"new_height_mm\"]}"),

                // ---- "add" primitives ---------------------------------------
                new ToolDef(
                    "add_boss",
                    "Add a cylindrical boss on top of the body at the given position. Boss extrudes upward (+Z) by height.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Boss base center (X, Y, Z) in mm\"}, " +
                      "\"diameter_mm\": {\"type\": \"number\", \"description\": \"Boss diameter in mm (> 0)\"}, " +
                      "\"height_mm\": {\"type\": \"number\", \"description\": \"Boss height in mm (> 0)\"}" +
                    "}, \"required\": [\"position_mm\", \"diameter_mm\", \"height_mm\"]}"),

                new ToolDef(
                    "add_hole",
                    "Drill a hole (cylindrical Subtract) into the body. If through=true, cutter spans the body's Z extent; else depth_mm must be > 0 (blind hole).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Hole entrance center (X, Y, Z) in mm\"}, " +
                      "\"diameter_mm\": {\"type\": \"number\", \"description\": \"Hole diameter in mm (> 0)\"}, " +
                      "\"through\": {\"type\": \"boolean\", \"description\": \"true = through-hole, false = blind\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Blind hole depth in mm (required when through=false)\"}" +
                    "}, \"required\": [\"position_mm\", \"diameter_mm\", \"through\"]}"),

                new ToolDef(
                    "add_slit",
                    "Add a rectangular slit (slot) on the body's top face. Length axis defaults to +Y; width is perpendicular in-plane. Cutter excavates downward by depth.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Slit center on top face (X, Y, Z) in mm\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Slit short-side width in mm (> 0)\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Slit long-side length in mm (> 0)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Slit depth in mm (> 0)\"}, " +
                      "\"orientation_axis\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Direction of the slit long axis (optional, default +Y)\"}" +
                    "}, \"required\": [\"position_mm\", \"width_mm\", \"length_mm\", \"depth_mm\"]}"),

                new ToolDef(
                    "add_pocket",
                    "Add an axis-aligned rectangular pocket (Subtract) on the body's top face. width=X span, length=Y span, depth=Z depth.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Pocket center on top face (X, Y, Z) in mm\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Pocket X-width in mm (> 0)\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Pocket Y-length in mm (> 0)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Pocket depth in mm (> 0)\"}" +
                    "}, \"required\": [\"position_mm\", \"width_mm\", \"length_mm\", \"depth_mm\"]}"),

                new ToolDef(
                    "add_rib",
                    "Add a rectangular rib (Unite) on the top face running from start to end along the XY-projected centerline. Width = perpendicular thickness, height = +Z extrusion.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"start_position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Rib centerline start (X, Y, Z) in mm\"}, " +
                      "\"end_position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Rib centerline end (X, Y, Z) in mm\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Rib thickness in mm (> 0)\"}, " +
                      "\"height_mm\": {\"type\": \"number\", \"description\": \"Rib height in mm above top face (> 0)\"}" +
                    "}, \"required\": [\"start_position_mm\", \"end_position_mm\", \"width_mm\", \"height_mm\"]}"),

                new ToolDef(
                    "add_chamfer",
                    "Apply a small fillet (chamfer approximation) to body edges matching the given filter ('all' | 'top' | 'bottom' | 'vertical').",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Chamfer (round) radius in mm (> 0)\"}, " +
                      "\"edge_filter\": {\"type\": \"string\", \"enum\": [\"all\", \"top\", \"bottom\", \"vertical\"], \"description\": \"Edge filter selector\"}" +
                    "}, \"required\": [\"width_mm\", \"edge_filter\"]}"),

                new ToolDef(
                    "add_hole_pattern",
                    "Add multiple holes at once. pattern_type: 'Linear' | 'Circular' | 'Grid'. For Linear/Circular set count and spacing; for Grid set rows, cols, row_spacing, col_spacing.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"center_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Pattern center (X, Y, Z) in mm\"}, " +
                      "\"axis_direction\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Linear: in-plane direction; Circular: circle plane normal\"}, " +
                      "\"pattern_type\": {\"type\": \"string\", \"enum\": [\"Linear\", \"Circular\", \"Grid\"]}, " +
                      "\"count\": {\"type\": \"integer\", \"description\": \"Hole count for Linear/Circular (>= 2 for Circular)\"}, " +
                      "\"spacing\": {\"type\": \"number\", \"description\": \"Linear: spacing in mm; Circular: ring radius in mm\"}, " +
                      "\"diameter_mm\": {\"type\": \"number\", \"description\": \"Per-hole diameter in mm (> 0)\"}, " +
                      "\"through\": {\"type\": \"boolean\", \"description\": \"true = through; false = blind\"}, " +
                      "\"rows\": {\"type\": \"integer\", \"description\": \"Grid rows (Grid only)\"}, " +
                      "\"cols\": {\"type\": \"integer\", \"description\": \"Grid columns (Grid only)\"}, " +
                      "\"row_spacing\": {\"type\": \"number\", \"description\": \"Grid row spacing in mm (Grid only)\"}, " +
                      "\"col_spacing\": {\"type\": \"number\", \"description\": \"Grid column spacing in mm (Grid only)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Blind hole depth in mm (when through=false)\"}" +
                    "}, \"required\": [\"center_mm\", \"pattern_type\", \"diameter_mm\", \"through\"]}"),

                // ---- P4 oriented-face creation (curved back/flank) -----------
                // Unlike the planar add_* (top-face, +Z), these enter along the LOCAL outward
                // normal of whatever face is nearest the 3D seed, so they target a CURVED back or
                // flank: a lens hole following the crown, USB-C through a curved flank, a recess or
                // boss on a bump. seed_mm = a point ON or just outside the target face.
                new ToolDef(
                    "add_hole_on_face",
                    "Drill a hole that ENTERS ALONG THE LOCAL FACE NORMAL at a 3D seed point - use this " +
                    "(not add_hole) for a hole on a CURVED face (e.g. a camera-lens hole on a curved phone " +
                    "back, or a hole on a bump) that the planar straight-down add_hole cannot target. The " +
                    "tool picks the body face nearest seed_mm and drills inward along its outward normal.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point ON or just outside the target face (X, Y, Z) in mm\"}, " +
                      "\"diameter_mm\": {\"type\": \"number\", \"description\": \"Hole diameter in mm (> 0)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Depth along the inward normal in mm (> 0)\"}" +
                    "}, \"required\": [\"seed_mm\", \"diameter_mm\", \"depth_mm\"]}"),

                new ToolDef(
                    "add_slit_on_face",
                    "Cut a rectangular slit/slot that ENTERS ALONG THE LOCAL FACE NORMAL at a 3D seed - use " +
                    "this (not add_slit) for a slot through a CURVED flank (e.g. a USB-C or speaker slot on " +
                    "the curved side of a phone). orientation_seed_mm (optional) sets the slit's in-plane long " +
                    "axis: the slit length runs from the seed toward that point, projected onto the face.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point ON or just outside the target face (X, Y, Z) in mm\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Slit short-side width in mm (> 0)\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Slit long-side length in mm (> 0)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Depth along the inward normal in mm (> 0)\"}, " +
                      "\"orientation_seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Optional point the slit length points toward (in-plane long axis)\"}" +
                    "}, \"required\": [\"seed_mm\", \"width_mm\", \"length_mm\", \"depth_mm\"]}"),

                new ToolDef(
                    "add_pocket_on_face",
                    "Recess a rectangular pocket that ENTERS ALONG THE LOCAL FACE NORMAL at a 3D seed - use " +
                    "this (not add_pocket) for a recess on a CURVED face (e.g. a logo or antenna window on a " +
                    "curved back). The tool picks the body face nearest seed_mm and recesses inward.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point ON or just outside the target face (X, Y, Z) in mm\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Pocket width in mm (> 0)\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Pocket length in mm (> 0)\"}, " +
                      "\"depth_mm\": {\"type\": \"number\", \"description\": \"Recess depth along the inward normal in mm (> 0)\"}" +
                    "}, \"required\": [\"seed_mm\", \"width_mm\", \"length_mm\", \"depth_mm\"]}"),

                new ToolDef(
                    "add_boss_on_face",
                    "Raise a cylindrical boss that STANDS PROUD ALONG THE LOCAL FACE NORMAL at a 3D seed - use " +
                    "this (not add_boss) for a boss on a CURVED face (e.g. a camera-ring boss on a curved " +
                    "back). The tool picks the body face nearest seed_mm and raises the boss outward.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point ON or just outside the target face (X, Y, Z) in mm\"}, " +
                      "\"diameter_mm\": {\"type\": \"number\", \"description\": \"Boss diameter in mm (> 0)\"}, " +
                      "\"height_mm\": {\"type\": \"number\", \"description\": \"Boss height along the outward normal in mm (> 0)\"}" +
                    "}, \"required\": [\"seed_mm\", \"diameter_mm\", \"height_mm\"]}"),

                // ---- "remove" primitive --------------------------------------
                new ToolDef(
                    "remove_hole",
                    "Fill an existing hole (Unite a matching cylinder). Works for through or blind holes.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"hole_id\": {\"type\": \"string\", \"description\": \"Hole id from FeatureGraph\"}" +
                    "}, \"required\": [\"hole_id\"]}"),

                // ---- move / rotate / mirror ---------------------------------
                new ToolDef(
                    "move_hole",
                    "Move an existing hole by filling the old location and re-drilling at new_position_mm with the same D, depth, and through-ness.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"hole_id\": {\"type\": \"string\", \"description\": \"Hole id\"}, " +
                      "\"new_position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"New entrance center (X, Y, Z) in mm\"}" +
                    "}, \"required\": [\"hole_id\", \"new_position_mm\"]}"),

                new ToolDef(
                    "move_boss",
                    "Move an existing boss: subtract the old boss volume, then add a same-D/H boss at new_position_mm.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"boss_id\": {\"type\": \"string\", \"description\": \"Boss id\"}, " +
                      "\"new_position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"New base center (X, Y, Z) in mm\"}" +
                    "}, \"required\": [\"boss_id\", \"new_position_mm\"]}"),

                new ToolDef(
                    "rotate_boss",
                    "Rotate an existing boss about (axis_point_mm, axis_direction) by angle_deg. The boss is removed and re-added at the rotated base position.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"boss_id\": {\"type\": \"string\", \"description\": \"Boss id\"}, " +
                      "\"axis_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"axis_direction\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"angle_deg\": {\"type\": \"number\", \"description\": \"Rotation angle in degrees\"}" +
                    "}, \"required\": [\"boss_id\", \"axis_point_mm\", \"axis_direction\", \"angle_deg\"]}"),

                new ToolDef(
                    "rotate_hole",
                    "Rotate an existing hole about (axis_point_mm, axis_direction) by angle_deg. The old hole is filled and a same-D hole is drilled at the rotated position.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"hole_id\": {\"type\": \"string\", \"description\": \"Hole id\"}, " +
                      "\"axis_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"axis_direction\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"angle_deg\": {\"type\": \"number\", \"description\": \"Rotation angle in degrees\"}" +
                    "}, \"required\": [\"hole_id\", \"axis_point_mm\", \"axis_direction\", \"angle_deg\"]}"),

                new ToolDef(
                    "mirror_feature",
                    "Mirror a hole ('H*') or boss ('B*') across a plane (normal, origin). The original is kept; a same-dimension copy is added at the reflected position.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"feature_id\": {\"type\": \"string\", \"description\": \"Hole id (H*) or Boss id (B*)\"}, " +
                      "\"mirror_plane_normal\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Mirror plane normal (non-zero vector)\"}, " +
                      "\"mirror_plane_origin\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point on the mirror plane (mm)\"}" +
                    "}, \"required\": [\"feature_id\", \"mirror_plane_normal\", \"mirror_plane_origin\"]}"),

                // ---- batch op-chain -----------------------------------------
                new ToolDef(
                    "apply_operations",
                    "Run SEVERAL edits in ONE call (one round-trip) instead of issuing each tool " +
                    "separately. Pass `operations`: an array of {\\\"tool\\\": <name>, \\\"args\\\": {...}} where " +
                    "tool is any OTHER modification/creation tool (add_hole, add_boss, add_hole_on_face, " +
                    "change_hole_diameter, move_hole, mirror_feature, ...) and args is exactly that tool's " +
                    "argument object. Steps run IN ORDER and the chain STOPS at the first failure, " +
                    "reporting which step and why. Use this when the user asks for multiple edits at once " +
                    "(e.g. 'drill 3 holes and add a boss'). Cannot be nested. Example: {\\\"operations\\\":[" +
                    "{\\\"tool\\\":\\\"add_hole\\\",\\\"args\\\":{\\\"position_mm\\\":[10,0,7.4],\\\"diameter_mm\\\":3,\\\"through\\\":true}}," +
                    "{\\\"tool\\\":\\\"add_boss\\\",\\\"args\\\":{\\\"position_mm\\\":[-10,0,7.4],\\\"diameter_mm\\\":5,\\\"height_mm\\\":2}}]}.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"operations\": {\"type\": \"array\", \"description\": \"Ordered list of {tool, args} steps\", " +
                        "\"items\": {\"type\": \"object\", \"properties\": {" +
                          "\"tool\": {\"type\": \"string\", \"description\": \"Name of another modification/creation tool\"}, " +
                          "\"args\": {\"type\": \"object\", \"description\": \"That tool's argument object\"}" +
                        "}, \"required\": [\"tool\", \"args\"]}}" +
                    "}, \"required\": [\"operations\"]}"),

                // ---- read-only / introspection tools ------------------------
                new ToolDef(
                    "get_feature_graph",
                    "Return the FeatureGraph of the current body as JSON. Use this to discover available IDs (H*, B*, W*, FC*) before issuing modification tools.",
                    "{\"type\": \"object\", \"properties\": {}, \"required\": []}"),

                new ToolDef(
                    "find_features_by_type",
                    "Return the IDs of features of a given type ('hole'|'boss'|'wall'|'fillet_chain'|'slit'). Optional min_value / max_value filter on the primary dimension (D for hole/boss, R for fillet_chain, thickness for wall).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"feature_type\": {\"type\": \"string\", \"enum\": [\"hole\", \"boss\", \"wall\", \"fillet_chain\", \"slit\"], \"description\": \"Type to filter on\"}, " +
                      "\"min_value\": {\"type\": \"number\", \"description\": \"Inclusive lower bound on primary dimension (mm)\"}, " +
                      "\"max_value\": {\"type\": \"number\", \"description\": \"Inclusive upper bound on primary dimension (mm)\"}" +
                    "}, \"required\": [\"feature_type\"]}"),

                // ---- from-scratch generation (P7) ----
                new ToolDef(
                    "generate_phone",
                    "Generate a phone front-metal part FROM SCRATCH (no imported CAD needed) into a fresh document. Builds a uniform-wall hollow shell from the given envelope. All dimensions in mm; unspecified fields use sensible defaults.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Overall length (default 146.7)\"}, " +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Overall width (default 71.5)\"}, " +
                      "\"thickness_mm\": {\"type\": \"number\", \"description\": \"Overall thickness (default 7.4)\"}, " +
                      "\"corner_radius_mm\": {\"type\": \"number\", \"description\": \"Corner round radius (default 3.0)\"}, " +
                      "\"wall_mm\": {\"type\": \"number\", \"description\": \"Shell wall thickness; 0 = solid (default 0.6)\"}, " +
                      "\"camera_bump_mm\": {\"type\": \"number\", \"description\": \"Camera plateau height above the back; omit for none\"}, " +
                      "\"stop_at_stage\": {\"type\": \"string\", \"description\": \"Optional: HALT after a stage instead of a full build. 'S00' = bare rectangular base slab (a general primitive to start from), 'S00b' = hollow shell only. Omit for the full phone.\"}" +
                    "}, \"required\": []}"),

                // The RICH from-scratch tool: the full v2 design surface (curved back, oriented
                // features, holes/ports/grille/buttons arrays) that flat generate_phone cannot reach.
                // The LLM does the natural-language -> JSON; SpecParser binds + validates it.
                new ToolDef(
                    "generate_phone_from_spec",
                    "Generate a phone front-metal part FROM SCRATCH from a FULL structured spec - use this " +
                    "(not generate_phone) whenever the request involves a CURVED back, a lens/USB-C/speaker " +
                    "feature, or multiple holes/ports. Pass `spec_json`: a JSON object string with these " +
                    "snake_case keys (all mm; omit any field to keep its default; the server validates " +
                    "design intent and rejects an unbuildable spec before any geometry): " +
                    "length_mm, width_mm, thickness_mm, corner_r, min_wall, hollow_wall_mm (0=solid); " +
                    "back_bulge_mm (>0 = gently CURVED convex back; must leave >= min_wall after hollowing; " +
                    "curve must be GENTLE i.e. effective R >= width), lens_on_curved_back (bool; needs a " +
                    "curved back), ports_on_flank (bool; route flank ports through true side entry); " +
                    "pocket:false to disable the display pocket, or pocket:{enabled,width_mm,length_mm,depth_mm}; " +
                    "camera:{x_mm,y_mm,diameter_mm,height_mm,width_mm,length_mm,corner_r," +
                    "lenses:[{x_mm,y_mm,diameter_mm,depth_mm}]} (width+length>0 = rounded-rect plateau; " +
                    "lenses = openings recessed into the plateau, offsets from the camera centre, " +
                    "depth_mm 0 = down to back-surface level); " +
                    "front_punch:{x_mm,y_mm,diameter_mm} (front-camera punch hole through the display side, " +
                    "must sit inside the pocket when one is enabled); " +
                    "holes:[{x_mm,y_mm,diameter_mm,through(bool),depth_mm,on_curved_back(bool)}] (a lens hole " +
                    "on a curved back sets on_curved_back:true so it enters along the local crown normal); " +
                    "ports:[{type:'usbc'|'lightning',x_mm,y_mm,z_mm,width_mm,height_mm,on_face:'flank'}]; " +
                    "grille:{origin_x_mm,origin_y_mm,pitch_mm,rows,cols,hole_diameter_mm,on_back} " +
                    "(on_back:true drills the grille on the BACK face as blind holes piercing the tray floor); " +
                    "buttons:[{x_mm,y_mm,z_mm,width_mm,height_mm,depth_mm}]. " +
                    "Example: {\\\"length_mm\\\":150,\\\"width_mm\\\":72,\\\"thickness_mm\\\":8,\\\"back_bulge_mm\\\":0.7," +
                    "\\\"lens_on_curved_back\\\":true,\\\"ports_on_flank\\\":true," +
                    "\\\"holes\\\":[{\\\"x_mm\\\":20,\\\"y_mm\\\":0,\\\"diameter_mm\\\":4,\\\"through\\\":false,\\\"depth_mm\\\":1,\\\"on_curved_back\\\":true}]," +
                    "\\\"ports\\\":[{\\\"type\\\":\\\"usbc\\\",\\\"x_mm\\\":0,\\\"y_mm\\\":-36,\\\"z_mm\\\":4,\\\"width_mm\\\":9,\\\"height_mm\\\":3,\\\"on_face\\\":\\\"flank\\\"}]}.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"spec_json\": {\"type\": \"string\", \"description\": \"A JSON object string describing the full phone spec (snake_case keys as documented). The server parses + validates it.\"}, " +
                      "\"stop_at_stage\": {\"type\": \"string\", \"description\": \"Optional: HALT after a stage (e.g. 'S00' bare slab, 'S00a' curved back, 'S00b' hollow shell) instead of the full build.\"}" +
                    "}, \"required\": [\"spec_json\"]}"),

                new ToolDef(
                    "set_camera_height",
                    "Change the camera bump height of the generated phone and regenerate. Edits the parametric source of truth so the body stays consistent with the design parameters. (Superseded by the generic set_parameters, kept for back-compat.)",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"height_mm\": {\"type\": \"number\", \"description\": \"New camera bump height (mm)\"}" +
                    "}, \"required\": [\"height_mm\"]}"),

                // ---- kernel-truth inspection (P6/P8) -------------------------
                new ToolDef(
                    "validate_body",
                    "Kernel-truth manufacturability check of the CURRENT body: closed-solid volume plus a " +
                    "minimum-wall march when the body was generated in this session (the march samples the " +
                    "generated-phone frame, so min_wall_mm is null for imported/CAE bodies - volume-only then). " +
                    "Run this after a chain of modification tools to verify the result is still buildable.",
                    "{\"type\": \"object\", \"properties\": {}, \"required\": []}"),

                new ToolDef(
                    "measure_body",
                    "Measure the CURRENT body: volume_mm3, bbox_size_mm (sorted ascending - the same " +
                    "orientation-invariant convention fea_freeze fingerprints use), and closed_solid. Use it " +
                    "before/after an edit to reason about volume deltas, or to check parity with a freeze.",
                    "{\"type\": \"object\", \"properties\": {}, \"required\": []}"),

                new ToolDef(
                    "fea_freeze",
                    "Freeze the CURRENT body to an FEA-handoff file and return its kernel-truth fingerprint " +
                    "(volume/bbox/closed) captured BEFORE the write. format 'scdocx' (default) is license-free " +
                    "native SpaceClaim; 'step' auto-downgrades to scdocx under the ANSYS Student license " +
                    "(downgraded_from_step:true in the result). SIDE EFFECT: the scdocx path saves the ACTIVE " +
                    "document, retargeting its save path to out_path.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"out_path\": {\"type\": \"string\", \"description\": \"Absolute output path; extension is normalized to the format actually written\"}, " +
                      "\"format\": {\"type\": \"string\", \"enum\": [\"scdocx\", \"step\"], \"description\": \"Default scdocx. step falls back to scdocx when the license refuses STEP export.\"}" +
                    "}, \"required\": [\"out_path\"]}"),

                // ---- spec / params session tools (P7 read+write halves) ------
                new ToolDef(
                    "parse_spec",
                    "DRY-RUN lint of a phone spec WITHOUT creating any geometry or document: binds the same " +
                    "spec_json generate_phone_from_spec takes and returns valid/errors/warnings (warnings " +
                    "flag typo'd/unknown keys even when the spec passes). Iterate here until valid, then call " +
                    "generate_phone_from_spec.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"spec_json\": {\"type\": \"string\", \"description\": \"A JSON object string - the same snake_case schema generate_phone_from_spec documents\"}" +
                    "}, \"required\": [\"spec_json\"]}"),

                new ToolDef(
                    "get_parameters",
                    "Return the CURRENT session PhoneParameters as a full JSON spec (snake_case keys, " +
                    "round-trip compatible with parse_spec / generate_phone_from_spec / set_parameters). " +
                    "This is the READ half of the parametric loop - inspect before editing. Errors when no " +
                    "phone has been generated in this session.",
                    "{\"type\": \"object\", \"properties\": {}, \"required\": []}"),

                new ToolDef(
                    "set_parameters",
                    "PATCH any phone parameters and REGENERATE - the generic write half of the parametric " +
                    "loop (supersedes set_camera_height). spec_patch is a JSON object with the same keys as " +
                    "generate_phone_from_spec; ONLY supplied keys change. Rules: list blocks (holes/ports/" +
                    "buttons/antenna_slits/pin_holes) and camera/grille objects are REPLACED wholesale when " +
                    "supplied. Regeneration replays ALL stages from parameters - direct geometry edits made " +
                    "with add_*/change_* tools are NOT preserved.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"spec_patch\": {\"type\": \"string\", \"description\": \"JSON object string; only the supplied keys are changed (e.g. {\\\"length_mm\\\": 160})\"}, " +
                      "\"stop_at_stage\": {\"type\": \"string\", \"description\": \"Optional: HALT the regeneration after a stage (same semantics as generate_phone)\"}" +
                    "}, \"required\": [\"spec_patch\"]}"),

                // ---- session binding -----------------------------------------
                new ToolDef(
                    "rebind_active_body",
                    "Re-point the session at the ACTIVE document's first body and re-extract its FeatureGraph. " +
                    "Use after opening/switching documents: the automatic bind only fires when the session has " +
                    "no body yet, so without this the tools keep editing the previously bound (stale) body.",
                    "{\"type\": \"object\", \"properties\": {}, \"required\": []}"),

                new ToolDef(
                    "import_step",
                    "Import a .stp/.step file via SpaceClaim's native translator into a NEW document, extract " +
                    "its FeatureGraph, and bind it as the session body (subsequent tools then edit the import). " +
                    "WARNING: interactive SpaceClaim sessions only - the STEP translator hangs behind a dialog " +
                    "in /Headless mode. STEP IMPORT is not license-blocked on Student (only export is).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"path\": {\"type\": \"string\", \"description\": \"Absolute path to a .stp/.step file\"}" +
                    "}, \"required\": [\"path\"]}"),

                // ---- CAE from-scratch generators -----------------------------
                new ToolDef(
                    "generate_tensile_specimen",
                    "Generate a standard tensile/shear TEST SPECIMEN from scratch (plus grip jaws and named " +
                    "faces/selections for the Mechanical handoff) in the active document. 26 standards: " +
                    "ASTM_E8_Standard, ASTM_E8_SubSize, ISO_6892_1, ASTM_D638_TypeI..TypeV, ISO_527_2_Type1A/B, " +
                    "ASTM_E602_VNotch/UNotch, ASTM_E338, ASTM_E292, ASTM_D5766_OHT, ASTM_D6484_OHC, " +
                    "ASTM_D6742_FHT, ASTM_D5961_Bearing, ASTM_D3039, IPC_TM650_Tensile, IPC_TM650_PTHPull, " +
                    "DMA_Tensile_Rectangle, DMA_Tensile_DogBone, ASTM_D5379_Iosipescu, ASTM_D7078_VNotchRailShear, " +
                    "Custom. The session binds to the SPECIMEN body afterwards.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"standard\": {\"type\": \"string\", \"description\": \"One of the standard names above (e.g. ASTM_D638_TypeI)\"}, " +
                      "\"overrides\": {\"type\": \"object\", \"description\": \"Optional dimension overrides in mm/deg: gauge_length_mm, gauge_width_mm, thickness_mm, grip_width_mm, total_length_mm, fillet_radius_mm, grip_length_mm, notch_depth_mm, notch_radius_mm, notch_angle_deg, is_double_notch, hole_diameter_mm, is_elliptical_hole, hole_major_axis_mm, hole_minor_axis_mm, is_rectangular, tab_length_mm, tab_thickness_mm\"}" +
                    "}, \"required\": [\"standard\"]}"),

                new ToolDef(
                    "generate_laminate",
                    "Generate an N-layer rectangular LAMINATE stack from scratch (individual conformal bodies " +
                    "with coincident interfaces + automatic interface Named Selections) in the active document - " +
                    "e.g. an OLED/adhesive/glass display stack for CAE. The session binds to the first layer.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"width_mm\": {\"type\": \"number\", \"description\": \"Plate width in mm (> 0)\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Plate length in mm (> 0)\"}, " +
                      "\"stacking_direction\": {\"type\": \"string\", \"enum\": [\"X\", \"Y\", \"Z\"], \"description\": \"Stack axis (default Z)\"}, " +
                      "\"layers\": {\"type\": \"array\", \"description\": \"Bottom-up layer list\", \"items\": {\"type\": \"object\", \"properties\": {" +
                        "\"name\": {\"type\": \"string\", \"description\": \"Layer/body name (default Layer_i)\"}, " +
                        "\"thickness_mm\": {\"type\": \"number\", \"description\": \"Layer thickness in mm (> 0)\"}" +
                      "}, \"required\": [\"thickness_mm\"]}}" +
                    "}, \"required\": [\"width_mm\", \"length_mm\", \"layers\"]}"),

                new ToolDef(
                    "parse_package_file",
                    "Parse a package description file (*Layer blocks with Location/Length/Thickness, " +
                    "'Cylinder,x,y,r' solder-ball-map entries, 'Box,x,y,w,h' embedded dies; Mesh* keys " +
                    "are filtered out) and return the layer-stack summary WITHOUT building any geometry - " +
                    "the dry-run lint for generate_package_from_file. See Examples/packages/*.txt.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"path\": {\"type\": \"string\", \"description\": \"Absolute path to a package description .txt file\"}" +
                    "}, \"required\": [\"path\"]}"),

                new ToolDef(
                    "generate_package_from_file",
                    "Build the full IC-PACKAGE CAD stack from a package description file: bottom-up " +
                    "layers (PCB/substrate/EMC...), solder-joint layers instanced from the embedded " +
                    "ball map (ONE body per ball), embedded dies (Box entries), and per inclusion layer " +
                    "an optional 'resin' MATRIX body = layer slab MINUS all inclusions via Boolean " +
                    "(underfill / molding compound). ball_shape 'cylinder' = straight joint as exported; " +
                    "'barrel' = convex reflowed joint whose radius follows a circular arc bulging at " +
                    "mid-height. Mesh* settings in the file are ignored for CAD. Layers stack in file " +
                    "order; the session binds to the cheapest created body (a plain slab). " +
                    "NOTE: thousands of balls build in one call - expect tens of seconds.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"path\": {\"type\": \"string\", \"description\": \"Absolute path to a package description .txt file\"}, " +
                      "\"ball_shape\": {\"type\": \"string\", \"enum\": [\"cylinder\", \"barrel\"], \"description\": \"Solder-joint shape (default cylinder)\"}, " +
                      "\"barrel_bulge_ratio\": {\"type\": \"number\", \"description\": \"Barrel mid-height radius / pad radius (> 1, default 1.25)\"}, " +
                      "\"barrel_slices\": {\"type\": \"integer\", \"description\": \"Stacked-disc slices approximating the barrel arc (default 8)\"}, " +
                      "\"fill_matrix\": {\"type\": \"boolean\", \"description\": \"Create the resin matrix body per inclusion layer (default true)\"}, " +
                      "\"layer_filter\": {\"type\": \"array\", \"items\": {\"type\": \"string\"}, \"description\": \"Only build these layer names (z stacking stays true); omit for all\"}" +
                    "}, \"required\": [\"path\"]}"),

                // ---- general CAD primitives -----------------------------------
                new ToolDef(
                    "revolve_profile",
                    "REVOLVE a closed (r, z) polyline profile about an arbitrary axis to create a " +
                    "solid of revolution - shafts, pulleys, flanges, bottles, O-ring grooves. r = " +
                    "radial distance from the axis (>= 0), z = position along the axis. angle_deg " +
                    "in (0, 360] for partial revolves. The session binds to the created body.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"profile_rz_mm\": {\"type\": \"array\", \"items\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 2, \"maxItems\": 2}, \"minItems\": 3, \"description\": \"Closed profile as [r, z] pairs in mm (auto-closed)\"}, " +
                      "\"axis_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Point on the axis (default origin)\"}, " +
                      "\"axis_dir\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Axis direction (default +Z)\"}, " +
                      "\"angle_deg\": {\"type\": \"number\", \"description\": \"Revolve angle (default 360)\"}, " +
                      "\"name\": {\"type\": \"string\", \"description\": \"Body name (default 'Revolve')\"}" +
                    "}, \"required\": [\"profile_rz_mm\"]}"),

                new ToolDef(
                    "sweep_profile",
                    "SWEEP a circular section along a 3D polyline path with tangent arc-blended " +
                    "corners - pipes, tubes, wires, frames, cooling channels. wall_mm > 0 makes a " +
                    "hollow pipe (outer minus bore). corner_r_mm rounds every interior corner " +
                    "(recommended: sharp corners are kernel-hostile). Session binds to the body.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"path_mm\": {\"type\": \"array\", \"items\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, \"minItems\": 2, \"description\": \"Polyline waypoints [x,y,z] in mm\"}, " +
                      "\"dia_mm\": {\"type\": \"number\", \"description\": \"Section outer diameter\"}, " +
                      "\"corner_r_mm\": {\"type\": \"number\", \"description\": \"Corner blend radius (default 0 = sharp)\"}, " +
                      "\"wall_mm\": {\"type\": \"number\", \"description\": \"Pipe wall thickness (default 0 = solid)\"}, " +
                      "\"name\": {\"type\": \"string\", \"description\": \"Body name (default 'Sweep')\"}" +
                    "}, \"required\": [\"path_mm\", \"dia_mm\"]}"),

                new ToolDef(
                    "loft_profiles",
                    "LOFT a solid through 2+ stacked sections (circle | rect | polygon), each with " +
                    "its own size/center/rotation - transitions, funnels, ducts, tapered columns. " +
                    "Section planes are perpendicular to axis_dir. ruled=true gives straight " +
                    "(frustum-exact) transitions; false gives smooth. Session binds to the body.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"sections\": {\"type\": \"array\", \"minItems\": 2, \"items\": {\"type\": \"object\", \"properties\": {" +
                        "\"shape\": {\"type\": \"string\", \"enum\": [\"circle\", \"rect\", \"polygon\"]}, " +
                        "\"dia_mm\": {\"type\": \"number\", \"description\": \"circle dia / polygon circumscribed dia\"}, " +
                        "\"w_mm\": {\"type\": \"number\"}, \"h_mm\": {\"type\": \"number\"}, " +
                        "\"sides\": {\"type\": \"integer\", \"description\": \"polygon sides (default 6)\"}, " +
                        "\"center_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                        "\"rot_deg\": {\"type\": \"number\", \"description\": \"in-plane rotation\"}" +
                      "}, \"required\": [\"shape\", \"center_mm\"]}}, " +
                      "\"axis_dir\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Section-plane normal (default +Z)\"}, " +
                      "\"ruled\": {\"type\": \"boolean\", \"description\": \"Straight transitions (default true)\"}, " +
                      "\"name\": {\"type\": \"string\", \"description\": \"Body name (default 'Loft')\"}" +
                    "}, \"required\": [\"sections\"]}"),

                new ToolDef(
                    "split_body",
                    "SPLIT an existing body by a plane into below/above pieces (named separately, " +
                    "along the plane normal). Reports the piece volumes plus the original so " +
                    "conservation is checkable. DESTRUCTIVE when delete_original=true (default).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body (default: session body)\"}, " +
                      "\"plane_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"plane_normal\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3}, " +
                      "\"delete_original\": {\"type\": \"boolean\", \"description\": \"Default true\"}, " +
                      "\"name_below\": {\"type\": \"string\"}, \"name_above\": {\"type\": \"string\"}" +
                    "}, \"required\": [\"plane_point_mm\", \"plane_normal\"]}"),

                new ToolDef(
                    "draft_body",
                    "Apply a molding DRAFT: taper every planar SIDE face (normal perpendicular to " +
                    "pull_dir) by angle_deg about the neutral plane through neutral_point_mm. " +
                    "Positive/negative angle flips the taper sense. Reports faces tapered and the " +
                    "volume before/after (in-place mutation).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body (default: session body)\"}, " +
                      "\"neutral_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Point on the neutral (parting) plane\"}, " +
                      "\"pull_dir\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Mold pull direction (default +Z)\"}, " +
                      "\"angle_deg\": {\"type\": \"number\", \"description\": \"Draft angle, |a| in (0, 30]\"}" +
                    "}, \"required\": [\"neutral_point_mm\", \"angle_deg\"]}"),

                new ToolDef(
                    "transform_body",
                    "MOVE / ROTATE / SCALE a whole named body, optionally as COPIES and PATTERNS: " +
                    "op move + count N = linear array (step translation_mm); op rotate + count N = " +
                    "circular array (step angle_deg about the axis); copy=true keeps the original. " +
                    "op scale resizes about the body center (count 1 only).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body (default: session body)\"}, " +
                      "\"op\": {\"type\": \"string\", \"enum\": [\"move\", \"rotate\", \"scale\"]}, " +
                      "\"translation_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"move: step vector\"}, " +
                      "\"axis_point_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"rotate: axis point (default origin)\"}, " +
                      "\"axis_dir\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"rotate: axis dir (default +Z)\"}, " +
                      "\"angle_deg\": {\"type\": \"number\", \"description\": \"rotate: step angle\"}, " +
                      "\"factor\": {\"type\": \"number\", \"description\": \"scale: factor > 0\"}, " +
                      "\"copy\": {\"type\": \"boolean\", \"description\": \"Keep the original (default false)\"}, " +
                      "\"count\": {\"type\": \"integer\", \"description\": \"Pattern instances 1-200 (default 1)\"}" +
                    "}, \"required\": [\"op\"]}"),

                // ---- fastening design (concentric hole pairs) ----------------
                new ToolDef(
                    "suggest_fastener",
                    "Detect fastening SITES - coaxial cylindrical HOLE faces spanning the body stack " +
                    "(the 'two concentric circles' pick, any axis) - and return per-site geometry " +
                    "(hole d, grip = clamped stack thickness, axis, spanned bodies) PLUS parametric " +
                    "design recommendations: bolt (auto ISO 262 nominal/pitch + auto length + head " +
                    "options with trade-off rationale) and rivet (hole-filling shank + tail dims). " +
                    "Use this to GUIDE the fastening-design conversation before add_fastener. Read-only.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Optional 3D point; adds seed_distance_mm per site for disambiguation\"}" +
                    "}, \"required\": []}"),

                new ToolDef(
                    "add_fastener",
                    "Generate a PARAMETRIC fastener at the concentric-hole site nearest seed_mm. " +
                    "type 'bolt': head hex|socket_cap|pan|countersunk, ISO 262/724 metric thread " +
                    "rendered as 'rings' (cosmetic, one ring per pitch) | 'simplified' (minor-diameter " +
                    "core, FEA-friendly) | 'none', plus optional hex nut and washers - separate bodies " +
                    "for CAE contact. type 'rivet': factory head dome|flat|countersunk + hole-filling " +
                    "shank + bucked tail, one body. EVERY dimension auto-derives from the detected " +
                    "hole/grip via ISO-typical proportional ratios; nominal_d_mm/length_mm/pitch_mm " +
                    "override the automation. Returns the full dims_mm record actually used.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"3D point near the target hole (on/near its axis)\"}, " +
                      "\"type\": {\"type\": \"string\", \"enum\": [\"bolt\", \"rivet\"], \"description\": \"Default bolt\"}, " +
                      "\"head\": {\"type\": \"string\", \"enum\": [\"hex\", \"socket_cap\", \"pan\", \"countersunk\", \"dome\", \"flat\"], \"description\": \"Default: hex (bolt) / dome (rivet)\"}, " +
                      "\"thread\": {\"type\": \"string\", \"enum\": [\"rings\", \"simplified\", \"none\"], \"description\": \"Thread representation (default rings)\"}, " +
                      "\"nominal_d_mm\": {\"type\": \"number\", \"description\": \"Override the auto ISO nominal / rivet shank dia\"}, " +
                      "\"length_mm\": {\"type\": \"number\", \"description\": \"Override the auto bolt length (below head)\"}, " +
                      "\"pitch_mm\": {\"type\": \"number\", \"description\": \"Override the ISO 262 coarse pitch\"}, " +
                      "\"with_nut\": {\"type\": \"boolean\", \"description\": \"Bolt only (default true)\"}, " +
                      "\"with_washer\": {\"type\": \"boolean\", \"description\": \"Washer under head and nut (default false)\"}, " +
                      "\"name_prefix\": {\"type\": \"string\", \"description\": \"Body name prefix (default 'Fastener')\"}" +
                    "}, \"required\": [\"seed_mm\"]}"),

                // ---- CAE mutators on existing bodies -------------------------
                new ToolDef(
                    "laminate_body",
                    "Slice an EXISTING solid into laminate plies along its auto-detected thickness direction " +
                    "(largest planar face pair), preserving the true outline (arcs, fillets, holes) - e.g. " +
                    "'split this cover glass into 3 plies'. Layer thicknesses must sum to the detected body " +
                    "thickness. DESTRUCTIVE when delete_original=true (default): the source body is removed.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body name (default: the session body)\"}, " +
                      "\"layers\": {\"type\": \"array\", \"description\": \"Bottom-up ply list; thicknesses must sum to the body thickness\", \"items\": {\"type\": \"object\", \"properties\": {" +
                        "\"name\": {\"type\": \"string\"}, " +
                        "\"thickness_mm\": {\"type\": \"number\"}" +
                      "}, \"required\": [\"thickness_mm\"]}}, " +
                      "\"delete_original\": {\"type\": \"boolean\", \"description\": \"Remove the source solid after slicing (default true)\"}" +
                    "}, \"required\": [\"layers\"]}"),

                new ToolDef(
                    "cut_void",
                    "Create a parametric cutter primitive and Boolean it against target bodies - the numeric " +
                    "swiss-army Boolean. shape 'Cuboid': dim1=width dim2=height dim3=depth; 'Cylinder': " +
                    "dim1=RADIUS dim2=height (axis +Z); 'Sphere': dim1=RADIUS. position_mm is the cutter " +
                    "center. mode Subtract removes material, Unite adds, Intersect keeps the overlap. " +
                    "Reports volume before/after so the delta is checkable.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"shape\": {\"type\": \"string\", \"enum\": [\"Cuboid\", \"Cylinder\", \"Sphere\"]}, " +
                      "\"dim1_mm\": {\"type\": \"number\", \"description\": \"Cuboid width | Cylinder RADIUS | Sphere RADIUS (mm)\"}, " +
                      "\"dim2_mm\": {\"type\": \"number\", \"description\": \"Cuboid height | Cylinder height (mm)\"}, " +
                      "\"dim3_mm\": {\"type\": \"number\", \"description\": \"Cuboid depth (mm)\"}, " +
                      "\"position_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"Cutter center (X, Y, Z) in mm\"}, " +
                      "\"mode\": {\"type\": \"string\", \"enum\": [\"Subtract\", \"Unite\", \"Intersect\"]}, " +
                      "\"offset_mm\": {\"type\": \"number\", \"description\": \"Grow the cutter by this clearance (default 0)\"}, " +
                      "\"target_body_names\": {\"type\": \"array\", \"items\": {\"type\": \"string\"}, \"description\": \"Target bodies by name (default: the session body)\"}, " +
                      "\"create_named_selection\": {\"type\": \"boolean\", \"description\": \"Create an NS on the cut faces (default false)\"}" +
                    "}, \"required\": [\"shape\", \"dim1_mm\", \"position_mm\", \"mode\"]}"),

                new ToolDef(
                    "simplify_bodies",
                    "Keyword-driven CAE simplification across the whole part: mode 'BoundingBox' replaces each " +
                    "matched body with its bounding box (renamed *_simplified), 'SolidToShell' replaces thin " +
                    "solids with a mid-surface sheet (*_shell). Matching is a case-insensitive substring of " +
                    "body names, recursive through components (e.g. keyword 'screw' = 'replace all screws " +
                    "with boxes'). DESTRUCTIVE: the original bodies are deleted.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"keyword\": {\"type\": \"string\", \"description\": \"Case-insensitive substring of body names to match\"}, " +
                      "\"mode\": {\"type\": \"string\", \"enum\": [\"BoundingBox\", \"SolidToShell\"]}" +
                    "}, \"required\": [\"keyword\", \"mode\"]}"),

                new ToolDef(
                    "create_bending_fixture",
                    "Create a 3-point bending rig around a body: two lower supports + a loading nose (each a " +
                    "semi-cylinder with a reinforcement block, 6 bodies total). Span/width/loading axes are " +
                    "auto-detected from the bounding box (longest=span, shortest=loading); the real top/bottom " +
                    "surfaces are found by Boolean probing so curved bodies work. Defaults: span_ratio 0.8, " +
                    "diameters 3.8mm (ASTM D790-ish). Fails cleanly if a fixture would collide with the body.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body name (default: the session body)\"}, " +
                      "\"span_ratio\": {\"type\": \"number\", \"description\": \"Support span as a fraction of body length (default 0.8)\"}, " +
                      "\"span_mm\": {\"type\": \"number\", \"description\": \"Explicit support span in mm (overrides span_ratio)\"}, " +
                      "\"support_diameter_mm\": {\"type\": \"number\", \"description\": \"Lower support roller diameter (default 3.8)\"}, " +
                      "\"nose_diameter_mm\": {\"type\": \"number\", \"description\": \"Loading nose diameter (default 3.8)\"}" +
                    "}, \"required\": []}"),

                // ---- read-only assembly inspection ---------------------------
                new ToolDef(
                    "detect_contacts",
                    "Find contact interfaces between ALL body pairs in the part (read-only): anti-parallel " +
                    "planar faces within tolerance and coaxial equal-radius cylinders. Returns body pairs with " +
                    "type and contact area - the assembly-inspection step before conformal meshing or bonded-" +
                    "contact setup.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"tolerance_mm\": {\"type\": \"number\", \"description\": \"Max face-to-face gap in mm (default 0.1)\"}, " +
                      "\"keyword\": {\"type\": \"string\", \"description\": \"Optional: only consider bodies whose name contains this substring\"}, " +
                      "\"detect_planar\": {\"type\": \"boolean\", \"description\": \"Detect planar contacts (default true)\"}, " +
                      "\"detect_cylindrical\": {\"type\": \"boolean\", \"description\": \"Detect cylindrical contacts (default true)\"}" +
                    "}, \"required\": []}"),

                // ---- licensed-seat CAE tools ---------------------------------
                new ToolDef(
                    "mesh_with_gmsh",
                    "Volume-mesh the whole active part with the bundled external Gmsh: STEP export -> .geo " +
                    "script -> gmsh.exe -> parsed node/element counts, optionally writing an LS-DYNA .k file. " +
                    "NOTE: step 1 is a STEP export, which the ANSYS STUDENT license refuses - on a Student " +
                    "seat this tool fails gracefully with the diagnostic log; run it on a licensed seat.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"element_size_mm\": {\"type\": \"number\", \"description\": \"Global element size in mm (default 5.0)\"}, " +
                      "\"growth_rate\": {\"type\": \"number\", \"description\": \"Size growth rate 1.0-3.0 (default 1.3)\"}, " +
                      "\"shape\": {\"type\": \"string\", \"enum\": [\"Tet\", \"Hex\"], \"description\": \"Element shape (default Tet)\"}, " +
                      "\"algorithm\": {\"type\": \"string\", \"enum\": [\"Auto\", \"Delaunay\", \"Frontal\", \"HXT\"], \"description\": \"Mesh algorithm (default Auto)\"}, " +
                      "\"midside_nodes\": {\"type\": \"boolean\", \"description\": \"Quadratic elements (default false)\"}, " +
                      "\"body_overrides\": {\"type\": \"array\", \"description\": \"Per-body element sizes\", \"items\": {\"type\": \"object\", \"properties\": {" +
                        "\"body_name\": {\"type\": \"string\"}, " +
                        "\"element_size_mm\": {\"type\": \"number\"}" +
                      "}, \"required\": [\"body_name\", \"element_size_mm\"]}}, " +
                      "\"kfile_path\": {\"type\": \"string\", \"description\": \"Optional: also write an LS-DYNA .k file to this absolute path\"}" +
                    "}, \"required\": []}"),

                new ToolDef(
                    "conformal_mesh",
                    "Run the conformal-mesh workflow on the CURRENT part's bodies (2+): detect contact " +
                    "interfaces -> interface Named Selections -> Share Topology -> optional cylinder edge " +
                    "sizing -> SpaceClaim tet/hex mesh -> optional LS-DYNA/ANSYS export. Works on bodies " +
                    "already in the document (never imports files). NOTE: the SC meshing commands may be " +
                    "license-gated on a Student seat - the tool then reports mesh_ok:false with the full log.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"tolerance_mm\": {\"type\": \"number\", \"description\": \"Interface gap tolerance in mm (default 1.0)\"}, " +
                      "\"keyword\": {\"type\": \"string\", \"description\": \"Optional body-name filter (substring)\"}, " +
                      "\"detect_planar\": {\"type\": \"boolean\", \"description\": \"Default true\"}, " +
                      "\"detect_cylindrical\": {\"type\": \"boolean\", \"description\": \"Default true\"}, " +
                      "\"share_topology\": {\"type\": \"boolean\", \"description\": \"Enable Share Topology (default true)\"}, " +
                      "\"create_named_selections\": {\"type\": \"boolean\", \"description\": \"Interface NS groups (default true)\"}, " +
                      "\"element_size_mm\": {\"type\": \"number\", \"description\": \"Mesh element size in mm (default 2.0)\"}, " +
                      "\"strategy\": {\"type\": \"string\", \"enum\": [\"AutoTet\", \"AutoHex\", \"Mixed\"], \"description\": \"Default AutoTet\"}, " +
                      "\"growth_rate\": {\"type\": \"number\", \"description\": \"1.0-5.0 (default 1.8)\"}, " +
                      "\"midside_nodes\": {\"type\": \"boolean\", \"description\": \"Default false\"}, " +
                      "\"split_cylinder_edges\": {\"type\": \"boolean\", \"description\": \"Default false\"}, " +
                      "\"cylinder_divisions\": {\"type\": \"integer\", \"description\": \"Circle divisions when splitting (default 8)\"}, " +
                      "\"export_path\": {\"type\": \"string\", \"description\": \"Optional: export the mesh to this absolute path\"}, " +
                      "\"export_format\": {\"type\": \"string\", \"enum\": [\"LS-DYNA\", \"ANSYS\"], \"description\": \"Default LS-DYNA\"}" +
                    "}, \"required\": []}"),

                new ToolDef(
                    "surface_laminate",
                    "Grow laminate layers FROM a planar face of an existing body: the face's boundary " +
                    "(lines/arcs/ellipses) is extruded layer by layer along the face normal (or reversed), " +
                    "with interface Named Selections between plies. Address the face with seed_mm - a 3D " +
                    "point ON the planar face (the add_*_on_face idiom). Complements laminate_body (which " +
                    "SLICES a solid) and generate_laminate (which builds a free-standing stack).",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"seed_mm\": {\"type\": \"array\", \"items\": {\"type\": \"number\"}, \"minItems\": 3, \"maxItems\": 3, \"description\": \"A point ON the planar face to laminate from (X, Y, Z) in mm\"}, " +
                      "\"body_name\": {\"type\": \"string\", \"description\": \"Target body name (default: the session body)\"}, " +
                      "\"layers\": {\"type\": \"array\", \"description\": \"Layer list, first layer sits on the face\", \"items\": {\"type\": \"object\", \"properties\": {" +
                        "\"name\": {\"type\": \"string\"}, " +
                        "\"thickness_mm\": {\"type\": \"number\"}" +
                      "}, \"required\": [\"thickness_mm\"]}}, " +
                      "\"direction\": {\"type\": \"string\", \"enum\": [\"normal\", \"reverse\"], \"description\": \"Stack along the face normal (default) or reversed into/away from it\"}" +
                    "}, \"required\": [\"seed_mm\", \"layers\"]}"),
            };
            return tools;
        }

        /// <summary>
        /// Convenience: emit the full list as a single JSON array (suitable for
        /// the Anthropic Messages API "tools" field). Hand-rolled to match the
        /// FeatureGraphJsonWriter style.
        /// </summary>
        public static string ToToolsArrayJson()
        {
            var tools = GetAllTools();
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                sb.Append("{");
                sb.Append("\"name\": \"").Append(Escape(t.Name)).Append("\", ");
                sb.Append("\"description\": \"").Append(Escape(t.Description)).Append("\", ");
                sb.Append("\"input_schema\": ").Append(t.InputSchemaJson);
                sb.Append("}");
                if (i < tools.Count - 1) sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
