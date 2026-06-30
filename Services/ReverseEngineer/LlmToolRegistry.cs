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
                      "\"camera_bump_mm\": {\"type\": \"number\", \"description\": \"Camera plateau height above the back; omit for none\"}" +
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
                    "camera:{x_mm,y_mm,diameter_mm,height_mm}; " +
                    "holes:[{x_mm,y_mm,diameter_mm,through(bool),depth_mm,on_curved_back(bool)}] (a lens hole " +
                    "on a curved back sets on_curved_back:true so it enters along the local crown normal); " +
                    "ports:[{type:'usbc'|'lightning',x_mm,y_mm,z_mm,width_mm,height_mm,on_face:'flank'}]; " +
                    "grille:{origin_x_mm,origin_y_mm,pitch_mm,rows,cols,hole_diameter_mm}; " +
                    "buttons:[{x_mm,y_mm,z_mm,width_mm,height_mm,depth_mm}]. " +
                    "Example: {\\\"length_mm\\\":150,\\\"width_mm\\\":72,\\\"thickness_mm\\\":8,\\\"back_bulge_mm\\\":0.7," +
                    "\\\"lens_on_curved_back\\\":true,\\\"ports_on_flank\\\":true," +
                    "\\\"holes\\\":[{\\\"x_mm\\\":20,\\\"y_mm\\\":0,\\\"diameter_mm\\\":4,\\\"through\\\":false,\\\"depth_mm\\\":1,\\\"on_curved_back\\\":true}]," +
                    "\\\"ports\\\":[{\\\"type\\\":\\\"usbc\\\",\\\"x_mm\\\":0,\\\"y_mm\\\":-36,\\\"z_mm\\\":4,\\\"width_mm\\\":9,\\\"height_mm\\\":3,\\\"on_face\\\":\\\"flank\\\"}]}.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"spec_json\": {\"type\": \"string\", \"description\": \"A JSON object string describing the full phone spec (snake_case keys as documented). The server parses + validates it.\"}" +
                    "}, \"required\": [\"spec_json\"]}"),

                new ToolDef(
                    "set_camera_height",
                    "Change the camera bump height of the generated phone and regenerate. Edits the parametric source of truth so the body stays consistent with the design parameters.",
                    "{\"type\": \"object\", \"properties\": {" +
                      "\"height_mm\": {\"type\": \"number\", \"description\": \"New camera bump height (mm)\"}" +
                    "}, \"required\": [\"height_mm\"]}"),
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
