using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Stage 5: Dispatch LLM "tool_use" requests to ModificationService primitives.
    ///
    /// Pure JSON in / JSON out — does NOT depend on the Anthropic SDK. The caller
    /// supplies (tool_name, input_json) and receives:
    ///   { "success": bool, "error": string|null, "result": {...} }
    ///
    /// Hand-rolled JSON parsing matches the FeatureGraphJsonWriter style (no
    /// NuGet dependencies, IronPython-friendly). Only the small subset we need
    /// for tool inputs is supported: object literals at the top level with
    /// string / number / bool / number[] / null leaves.
    /// </summary>
    // @lat: [[reverse-engineer#LlmToolDispatcher]]
    public static class LlmToolDispatcher
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// C1 guard: tools that do NOT need a pre-existing designBody — read-only graph tools,
        /// pure spec/param tools, session-binding tools, and the from-scratch generators (which
        /// build/bind their own body via SessionContext). Everything else is rejected on a null
        /// body BEFORE its case runs. Keep in sync with McpServer's selfBinds/readOnly sets.
        /// </summary>
        private static readonly HashSet<string> NoBodyTools = new HashSet<string>
        {
            "get_feature_graph", "find_features_by_type",              // graph-only reads
            "generate_phone", "generate_phone_from_spec", "set_camera_height", // phone generation
            "parse_spec", "get_parameters", "set_parameters",          // pure spec/param tools
            "rebind_active_body", "import_step",                       // session binding
            "generate_tensile_specimen", "generate_laminate",          // CAE from-scratch generators
            "parse_package_file", "generate_package_from_file",        // package-stack generators
            "revolve_profile", "sweep_profile", "loft_profiles",       // CAD primitive creators
        };

        /// <summary>
        /// Dispatch one tool_use call. designBody/graph are the targets;
        /// toolName matches LlmToolRegistry.GetAllTools()[i].Name; inputJson
        /// is the JSON object the LLM emitted for "input".
        ///
        /// Returns a JSON envelope string. Never throws — all errors are
        /// surfaced via {"success": false, "error": "..."}.
        /// </summary>
        public static string Dispatch(DesignBody designBody, FeatureGraph graph, string toolName, string inputJson)
        {
            if (string.IsNullOrEmpty(toolName))
                return Envelope(false, "toolName is null/empty", null);
            if (designBody == null && !NoBodyTools.Contains(toolName))
                return Envelope(false, "designBody is null (required for modification tools)", null);

            Dictionary<string, object> args;
            try
            {
                args = ParseObject(inputJson ?? "{}");
            }
            catch (Exception ex)
            {
                return Envelope(false, "input JSON parse failed: " + ex.Message, null);
            }

            try
            {
                switch (toolName)
                {
                    case "change_wall_thickness":
                    {
                        string wallId = GetString(args, "wall_id");
                        double newT = GetDouble(args, "new_thickness_mm");
                        var r = ModificationService.ChangeWallThickness(designBody, graph, wallId, newT);
                        return EnvelopeFromResult(r);
                    }
                    case "change_hole_diameter":
                    {
                        string holeId = GetString(args, "hole_id");
                        double newD = GetDouble(args, "new_diameter_mm");
                        var r = ModificationService.ChangeHoleDiameter(designBody, graph, holeId, newD);
                        return EnvelopeFromResult(r);
                    }
                    case "change_fillet_radius":
                    {
                        string chainId = GetString(args, "fillet_chain_id");
                        double newR = GetDouble(args, "new_radius_mm");
                        var r = ModificationService.ChangeFilletRadius(designBody, graph, chainId, newR);
                        return EnvelopeFromResult(r);
                    }
                    case "change_boss_diameter":
                    {
                        string bossId = GetString(args, "boss_id");
                        double newD = GetDouble(args, "new_diameter_mm");
                        var r = ModificationService.ChangeBossDiameter(designBody, graph, bossId, newD);
                        return EnvelopeFromResult(r);
                    }
                    case "change_boss_height":
                    {
                        string bossId = GetString(args, "boss_id");
                        double newH = GetDouble(args, "new_height_mm");
                        var r = ModificationService.ChangeBossHeight(designBody, graph, bossId, newH);
                        return EnvelopeFromResult(r);
                    }
                    case "add_boss":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        double h = GetDouble(args, "height_mm");
                        // requireVolumeChange: the MCP surface opts INTO the volume-gate so a
                        // silent Boolean no-op (boss inside solid / cutter inside a bore) is an
                        // honest failure instead of success:true with zero effect. Internal
                        // callers (Move/Mirror/Pattern/Generation) stay on legacy semantics.
                        var r = ModificationService.AddBoss(designBody, pos, dia, h,
                            requireVolumeChange: true);
                        return EnvelopeFromResult(r);
                    }
                    case "add_hole":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        bool through = GetBool(args, "through");
                        double depth = GetDoubleOrDefault(args, "depth_mm", 0.0);
                        var r = ModificationService.AddHole(designBody, pos, dia, through, depth,
                            requireVolumeChange: true);
                        return EnvelopeFromResult(r);
                    }
                    case "add_slit":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        double[] axis = args.ContainsKey("orientation_axis") ? GetDoubleArray(args, "orientation_axis", 3) : null;
                        var r = ModificationService.AddSlit(designBody, pos, w, L, d, axis);
                        return EnvelopeFromResult(r);
                    }
                    case "add_pocket":
                    {
                        double[] pos = GetDoubleArray(args, "position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        var r = ModificationService.AddPocket(designBody, pos, w, L, d);
                        return EnvelopeFromResult(r);
                    }
                    // ---- P4 oriented-face creation: enter along the LOCAL face normal at a
                    //      3D seed, so they work on a CURVED back/flank (lens hole, USB-C through
                    //      a curved flank, recess/boss on a bump) that the planar add_* cannot
                    //      target. seed_mm = a point ON or just outside the target face.
                    case "add_hole_on_face":
                    {
                        double[] seed = GetDoubleArray(args, "seed_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        double depth = GetDouble(args, "depth_mm");
                        var r = ModificationService.AddHoleOnFace(designBody, seed, dia, depth,
                            requireVolumeChange: true);
                        return EnvelopeFromResult(r);
                    }
                    case "add_slit_on_face":
                    {
                        double[] seed = GetDoubleArray(args, "seed_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        double[] orient = args.ContainsKey("orientation_seed_mm")
                            ? GetDoubleArray(args, "orientation_seed_mm", 3) : null;
                        var r = ModificationService.AddSlitOnFace(designBody, seed, w, L, d, orient);
                        return EnvelopeFromResult(r);
                    }
                    case "add_pocket_on_face":
                    {
                        double[] seed = GetDoubleArray(args, "seed_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double L = GetDouble(args, "length_mm");
                        double d = GetDouble(args, "depth_mm");
                        var r = ModificationService.AddPocketOnFace(designBody, seed, w, L, d);
                        return EnvelopeFromResult(r);
                    }
                    case "add_boss_on_face":
                    {
                        double[] seed = GetDoubleArray(args, "seed_mm", 3);
                        double dia = GetDouble(args, "diameter_mm");
                        double h = GetDouble(args, "height_mm");
                        var r = ModificationService.AddBossOnFace(designBody, seed, dia, h,
                            requireVolumeChange: true);
                        return EnvelopeFromResult(r);
                    }
                    case "add_rib":
                    {
                        double[] s = GetDoubleArray(args, "start_position_mm", 3);
                        double[] e = GetDoubleArray(args, "end_position_mm", 3);
                        double w = GetDouble(args, "width_mm");
                        double h = GetDouble(args, "height_mm");
                        var r = ModificationService.AddRib(designBody, s, e, w, h);
                        return EnvelopeFromResult(r);
                    }
                    case "add_chamfer":
                    {
                        double w = GetDouble(args, "width_mm");
                        string filter = GetString(args, "edge_filter");
                        var r = ModificationService.AddChamfer(designBody, w, filter);
                        return EnvelopeFromResult(r);
                    }
                    case "add_hole_pattern":
                    {
                        double[] center = GetDoubleArray(args, "center_mm", 3);
                        double[] axis = args.ContainsKey("axis_direction") ? GetDoubleArray(args, "axis_direction", 3) : null;
                        string pt = GetString(args, "pattern_type");
                        int count = (int)GetDoubleOrDefault(args, "count", 0);
                        double spacing = GetDoubleOrDefault(args, "spacing", 0.0);
                        double dia = GetDouble(args, "diameter_mm");
                        bool through = GetBool(args, "through");
                        int rows = (int)GetDoubleOrDefault(args, "rows", 0);
                        int cols = (int)GetDoubleOrDefault(args, "cols", 0);
                        double rowSp = GetDoubleOrDefault(args, "row_spacing", 0.0);
                        double colSp = GetDoubleOrDefault(args, "col_spacing", 0.0);
                        double depth = GetDoubleOrDefault(args, "depth_mm", 0.0);
                        var r = ModificationService.AddHolePattern(
                            designBody, center, axis, pt, count, spacing, dia, through,
                            rows, cols, rowSp, colSp, depth);
                        return EnvelopeFromResult(r);
                    }
                    case "remove_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        var r = ModificationService.RemoveHole(designBody, graph, holeId);
                        return EnvelopeFromResult(r);
                    }
                    case "move_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        double[] newPos = GetDoubleArray(args, "new_position_mm", 3);
                        var r = ModificationService.MoveHole(designBody, graph, holeId, newPos);
                        return EnvelopeFromResult(r);
                    }
                    case "move_boss":
                    {
                        string bossId = GetString(args, "boss_id");
                        double[] newPos = GetDoubleArray(args, "new_position_mm", 3);
                        var r = ModificationService.MoveBoss(designBody, graph, bossId, newPos);
                        return EnvelopeFromResult(r);
                    }
                    case "rotate_boss":
                    {
                        string bossId = GetString(args, "boss_id");
                        double[] axisPt = GetDoubleArray(args, "axis_point_mm", 3);
                        double[] axisDir = GetDoubleArray(args, "axis_direction", 3);
                        double angle = GetDouble(args, "angle_deg");
                        var r = ModificationService.RotateBoss(designBody, graph, bossId, axisPt, axisDir, angle);
                        return EnvelopeFromResult(r);
                    }
                    case "rotate_hole":
                    {
                        string holeId = GetString(args, "hole_id");
                        double[] axisPt = GetDoubleArray(args, "axis_point_mm", 3);
                        double[] axisDir = GetDoubleArray(args, "axis_direction", 3);
                        double angle = GetDouble(args, "angle_deg");
                        var r = ModificationService.RotateHole(designBody, graph, holeId, axisPt, axisDir, angle);
                        return EnvelopeFromResult(r);
                    }
                    case "mirror_feature":
                    {
                        string featId = GetString(args, "feature_id");
                        double[] mn = GetDoubleArray(args, "mirror_plane_normal", 3);
                        double[] mo = GetDoubleArray(args, "mirror_plane_origin", 3);
                        var r = ModificationService.MirrorFeature(designBody, graph, featId, mn, mo);
                        return EnvelopeFromResult(r);
                    }

                    // ---- batch op-chain ------------------------------------
                    // Run several edits in ONE call (one round-trip). operations = an array of
                    // {tool, args} where tool is any OTHER modification tool name and args is its
                    // argument object. Each step is RE-DISPATCHED through this same switch, so every
                    // tool is batchable for free; the chain STOPS at the first step that fails
                    // (matching ModificationService.ApplyOperations semantics) and reports which
                    // step + why. Nesting apply_operations inside itself is rejected.
                    case "apply_operations":
                    {
                        if (!args.ContainsKey("operations") || !(args["operations"] is List<object> opsList))
                            return Envelope(false, "apply_operations needs an 'operations' array of {tool, args}", null);
                        if (opsList.Count == 0)
                            return Envelope(false, "operations array is empty", null);

                        var stepNames = new List<string>();
                        int step = 0;
                        foreach (var item in opsList)
                        {
                            step++;
                            var opObj = item as Dictionary<string, object>;
                            if (opObj == null)
                                return Envelope(false, "step " + step + ": each operation must be an object {tool, args}", null);
                            if (!opObj.ContainsKey("tool") || !(opObj["tool"] is string opTool) || string.IsNullOrEmpty(opTool))
                                return Envelope(false, "step " + step + ": missing string 'tool'", null);
                            if (opTool == "apply_operations")
                                return Envelope(false, "step " + step + ": apply_operations cannot be nested", null);

                            object opArgsObj = opObj.ContainsKey("args") ? opObj["args"] : null;
                            var opArgs = opArgsObj as Dictionary<string, object>;
                            string opArgsJson = (opArgs != null) ? SerializeValue(opArgs) : "{}";

                            // Re-dispatch the step through this same switch (same designBody/graph).
                            string stepEnv = Dispatch(designBody, graph, opTool, opArgsJson);
                            stepNames.Add(opTool);
                            if (stepEnv == null || stepEnv.IndexOf("\"success\": true", StringComparison.Ordinal) < 0)
                                return Envelope(false, "step " + step + " (" + opTool + ") failed: " + (stepEnv ?? "(null)"), null);
                        }
                        return Envelope(true, null, "{\"applied\": " + step + ", \"chain\": \"" +
                            EscapeStr(string.Join("->", stepNames.ToArray())) + "\"}");
                    }

                    // ---- read-only -----------------------------------------
                    case "get_feature_graph":
                    {
                        if (graph == null)
                            return Envelope(false, "graph is null", null);
                        string graphJson = FeatureGraphJsonWriter.ToJson(graph);
                        // result is the FeatureGraph object literal verbatim.
                        return Envelope(true, null, graphJson);
                    }
                    case "find_features_by_type":
                    {
                        if (graph == null)
                            return Envelope(false, "graph is null", null);
                        string ftype = GetString(args, "feature_type");
                        double minV = GetDoubleOrDefault(args, "min_value", double.NegativeInfinity);
                        double maxV = GetDoubleOrDefault(args, "max_value", double.PositiveInfinity);
                        var ids = FindFeaturesByType(graph, ftype, minV, maxV);
                        return Envelope(true, null, BuildIdsResultJson(ftype, ids));
                    }

                    // ---- from-scratch generation (P7) ----------------------
                    case "generate_phone":
                    {
                        var p = new Models.ReverseEngineer.PhoneParameters();
                        p.LengthMm = GetDoubleOrDefault(args, "length_mm", p.LengthMm);
                        p.WidthMm = GetDoubleOrDefault(args, "width_mm", p.WidthMm);
                        p.ThicknessMm = GetDoubleOrDefault(args, "thickness_mm", p.ThicknessMm);
                        p.CornerRadiusMm = GetDoubleOrDefault(args, "corner_radius_mm", p.CornerRadiusMm);
                        p.HollowWallMm = GetDoubleOrDefault(args, "wall_mm", p.HollowWallMm);
                        double camH = GetDoubleOrDefault(args, "camera_bump_mm", -1);
                        if (camH > 0)
                        {
                            p.Camera = new Models.ReverseEngineer.PhoneParameters.CameraIsland();
                            p.Camera.HeightMm = camH;
                        }
                        System.Collections.Generic.List<string> verr;
                        if (!p.Validate(out verr))
                            return Envelope(false, "invalid spec: " + string.Join("; ", verr.ToArray()), null);
                        // optional stage halt: "S00" = bare base slab (a general primitive slab),
                        // "S00b" = hollow shell only, etc.; null/absent = full build.
                        string stage = args.ContainsKey("stop_at_stage") ? GetString(args, "stop_at_stage") : null;
                        string genErr = Mcp.SessionContext.Instance.GeneratePhone(p, stage);
                        if (genErr != null) return Envelope(false, genErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"stopped_at\": " +
                            (stage == null ? "null" : "\"" + EscapeStr(stage) + "\"") + ", \"params\": \"" +
                            p.ToString().Replace("\"", "'") + "\"}");
                    }
                    case "generate_phone_from_spec":
                    {
                        // The LLM emits a full structured JSON spec; SpecParser binds + validates it
                        // (the same Validate() generate_phone runs, plus the curved/oriented surface).
                        string specJson = GetString(args, "spec_json");
                        var pr = Generation.SpecParser.Parse(specJson);
                        if (!pr.Success)
                        {
                            string why = (pr.Errors != null && pr.Errors.Count > 0)
                                ? string.Join("; ", pr.Errors.ToArray()) : "spec rejected";
                            return Envelope(false, "invalid spec: " + why, null);
                        }
                        string stage2 = args.ContainsKey("stop_at_stage") ? GetString(args, "stop_at_stage") : null;
                        string genErr2 = Mcp.SessionContext.Instance.GeneratePhone(pr.Params, stage2);
                        if (genErr2 != null) return Envelope(false, genErr2, null);
                        string warnJson = "[]";
                        if (pr.Warnings != null && pr.Warnings.Count > 0)
                        {
                            var wb = new StringBuilder("[");
                            for (int i = 0; i < pr.Warnings.Count; i++)
                            {
                                if (i > 0) wb.Append(", ");
                                wb.Append("\"").Append(EscapeStr(pr.Warnings[i])).Append("\"");
                            }
                            wb.Append("]");
                            warnJson = wb.ToString();
                        }
                        return Envelope(true, null, "{\"generated\": true, \"params\": \"" +
                            pr.Params.ToString().Replace("\"", "'") + "\", \"warnings\": " + warnJson + "}");
                    }
                    case "set_camera_height":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        if (sc.Params == null)
                            return Envelope(false, "no generated phone in session (call generate_phone first)", null);
                        double newH = GetDouble(args, "height_mm");
                        // Edit the param (the source of truth) + regenerate — the reconciliation
                        // channel (P7): geometry never outruns params. Handle-based targeting is
                        // implicit because regenerate rebuilds the camera at its anchor.
                        if (sc.Params.Camera == null)
                            sc.Params.Camera = new Models.ReverseEngineer.PhoneParameters.CameraIsland();
                        sc.Params.Camera.HeightMm = newH;
                        string e2 = sc.GeneratePhone(sc.Params);
                        if (e2 != null) return Envelope(false, e2, null);
                        return Envelope(true, null, "{\"camera_height_mm\": " + newH + ", \"regenerated\": true}");
                    }

                    // ---- kernel-truth inspection (P6/P8) --------------------
                    case "validate_body":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        if (sc.Params != null && ReferenceEquals(sc.Body, designBody))
                        {
                            // full Tier-2 check: closed solid + min-wall march in the generated frame.
                            var vr = Generation.ValidationService.Validate(designBody, sc.Params);
                            var vb = new StringBuilder();
                            vb.Append("{\"pass\": ").Append(vr.Pass ? "true" : "false");
                            vb.Append(", \"issues\": ").Append(JsonStringArray(vr.Issues));
                            vb.Append(", \"volume_mm3\": ").Append(vr.VolumeMm3.ToString("0.###", Inv));
                            vb.Append(", \"min_wall_mm\": ").Append(
                                vr.MinWallMm >= 0 ? vr.MinWallMm.ToString("0.###", Inv) : "null");
                            vb.Append("}");
                            return Envelope(true, null, vb.ToString());
                        }
                        // no params for THIS body (external/CAE body): degrade to volume-only, never lie.
                        double vVol;
                        try { vVol = designBody.Shape.Volume * 1e9; } catch { vVol = 0; }
                        bool vPass = vVol > 0;
                        return Envelope(true, null, "{\"pass\": " + (vPass ? "true" : "false") +
                            ", \"issues\": " + (vPass ? "[]" : "[\"not a closed solid (Volume<=0)\"]") +
                            ", \"volume_mm3\": " + vVol.ToString("0.###", Inv) +
                            ", \"min_wall_mm\": null" +
                            ", \"note\": \"no session parameters for this body - volume-only check (min-wall march needs the generated-phone frame)\"}");
                    }
                    case "measure_body":
                    {
                        double mVol;
                        try { mVol = designBody.Shape.Volume * 1e9; } catch { mVol = 0; }
                        bool mClosed;
                        try { mClosed = designBody.Shape.IsClosed; } catch { mClosed = false; }
                        var mbb = designBody.Shape.GetBoundingBox(Geometry.Matrix.Identity);
                        double[] mDims =
                        {
                            (mbb.MaxCorner.X - mbb.MinCorner.X) * 1000.0,
                            (mbb.MaxCorner.Y - mbb.MinCorner.Y) * 1000.0,
                            (mbb.MaxCorner.Z - mbb.MinCorner.Z) * 1000.0,
                        };
                        Array.Sort(mDims); // sorted ascending — the fea_freeze fingerprint convention
                        return Envelope(true, null, "{\"volume_mm3\": " + mVol.ToString("0.###", Inv) +
                            ", \"bbox_size_mm\": [" + mDims[0].ToString("0.###", Inv) + ", " +
                            mDims[1].ToString("0.###", Inv) + ", " + mDims[2].ToString("0.###", Inv) + "]" +
                            ", \"closed_solid\": " + ((mClosed && mVol > 0) ? "true" : "false") + "}");
                    }
                    case "fea_freeze":
                    {
                        string outPath = GetString(args, "out_path");
                        string fmt = args.ContainsKey("format") ? GetString(args, "format") : "scdocx";
                        // reopenAndVerify pinned FALSE: Document.Open(.stp) hangs behind a translator
                        // dialog in /Headless — the fingerprint in the payload is the parity contract.
                        var fr = Generation.FeaFreezeService.Freeze(designBody, outPath, false, fmt);
                        if (fr == null || !fr.Success)
                            return Envelope(false, (fr != null ? fr.Error : null) ?? "freeze failed", null);
                        double[] fb = fr.BboxSizeMm ?? new double[3];
                        return Envelope(true, null, "{\"path\": \"" + EscapeStr(fr.StepPath ?? "") + "\"" +
                            ", \"format\": \"" + EscapeStr(fr.Format ?? "") + "\"" +
                            ", \"volume_mm3\": " + fr.VolumeMm3.ToString("0.###", Inv) +
                            ", \"bbox_sorted_mm\": [" + fb[0].ToString("0.###", Inv) + ", " +
                            fb[1].ToString("0.###", Inv) + ", " + fb[2].ToString("0.###", Inv) + "]" +
                            ", \"closed_solid\": " + (fr.ClosedSolid ? "true" : "false") +
                            ", \"downgraded_from_step\": " + (fr.DowngradedFromStep ? "true" : "false") +
                            ", \"file_bytes\": " + fr.FileBytes.ToString(Inv) + "}");
                    }

                    // ---- spec / params session tools (P7 read+write halves) --
                    case "parse_spec":
                    {
                        string specJson3 = GetString(args, "spec_json");
                        var pr3 = Generation.SpecParser.Parse(specJson3);
                        // the tool itself succeeded — validity is the PAYLOAD (dry-run lint).
                        return Envelope(true, null, "{\"valid\": " + (pr3.Success ? "true" : "false") +
                            ", \"errors\": " + JsonStringArray(pr3.Errors) +
                            ", \"warnings\": " + JsonStringArray(pr3.Warnings) +
                            ", \"params_summary\": \"" +
                            EscapeStr(pr3.Params != null ? pr3.Params.ToString().Replace("\"", "'") : "") + "\"}");
                    }
                    case "get_parameters":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        if (sc.Params == null)
                            return Envelope(false,
                                "no session parameters (params exist only after generate_phone/generate_phone_from_spec/set_parameters)", null);
                        return Envelope(true, null, Generation.PhoneParametersJsonWriter.ToJson(sc.Params));
                    }
                    case "set_parameters":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        if (sc.Params == null)
                            return Envelope(false,
                                "no session parameters to patch (call generate_phone/generate_phone_from_spec first)", null);
                        string patchJson = GetString(args, "spec_patch");
                        // Deep-copy the baseline via the round-trip writer so a REJECTED patch can
                        // never corrupt the live session params (Parse mutates its baseline).
                        var baseCopy = Generation.SpecParser.Parse(
                            Generation.PhoneParametersJsonWriter.ToJson(sc.Params));
                        if (!baseCopy.Success)
                            return Envelope(false, "internal: baseline round-trip failed: " +
                                string.Join("; ", baseCopy.Errors.ToArray()), null);
                        var pr4 = Generation.SpecParser.Parse(patchJson, baseCopy.Params);
                        if (!pr4.Success)
                            return Envelope(false, "invalid patch: " + string.Join("; ", pr4.Errors.ToArray()), null);
                        string stage3 = args.ContainsKey("stop_at_stage") ? GetString(args, "stop_at_stage") : null;
                        string genErr3 = sc.GeneratePhone(pr4.Params, stage3);
                        if (genErr3 != null) return Envelope(false, genErr3, null);
                        return Envelope(true, null, "{\"generated\": true, \"stopped_at\": " +
                            (stage3 == null ? "null" : "\"" + EscapeStr(stage3) + "\"") +
                            ", \"params\": \"" + pr4.Params.ToString().Replace("\"", "'") + "\"" +
                            ", \"warnings\": " + JsonStringArray(pr4.Warnings) + "}");
                    }

                    // ---- session binding -------------------------------------
                    case "rebind_active_body":
                    {
                        var sc = Mcp.SessionContext.Instance;
                        string bindErr2;
                        if (!sc.BindActiveBody(out bindErr2))
                            return Envelope(false, "rebind failed: " + (bindErr2 ?? "(no reason)"), null);
                        return Envelope(true, null, "{\"bound\": true, \"body_name\": \"" +
                            EscapeStr(sc.Body != null ? (sc.Body.Name ?? "") : "") + "\"" +
                            ", \"feature_counts\": " + FeatureCountsJson(sc.Graph) + "}");
                    }
                    case "import_step":
                    {
                        string stepPath = GetString(args, "path");
                        var ir = StepImportService.ImportAndExtract(stepPath);
                        if (ir == null || !ir.Success || ir.ImportedBody == null)
                            return Envelope(false, "import failed: " +
                                (ir != null ? (ir.ErrorMessage ?? "(no message)") : "(null result)"), null);
                        // MUST rebind explicitly: McpServer's auto-bind only fires when session.Body
                        // is null — without this, later mod tools keep mutating the STALE old body.
                        string ibErr = Mcp.SessionContext.Instance.BindBody(
                            ir.ImportedBody, ir.ExtractedGraph, stepPath);
                        if (ibErr != null) return Envelope(false, ibErr, null);
                        return Envelope(true, null, "{\"imported\": true, \"path\": \"" + EscapeStr(stepPath) + "\"" +
                            ", \"body_name\": \"" + EscapeStr(ir.ImportedBody.Name ?? "") + "\"" +
                            ", \"feature_counts\": " + FeatureCountsJson(ir.ExtractedGraph) +
                            ", \"note\": \"opened a NEW document; previously open documents remain\"}");
                    }

                    // ---- CAE from-scratch generators -------------------------
                    case "generate_tensile_specimen":
                    {
                        string standard = GetString(args, "standard");
                        Models.TensileTest.ASTMSpecimenType stdType;
                        try
                        {
                            stdType = (Models.TensileTest.ASTMSpecimenType)Enum.Parse(
                                typeof(Models.TensileTest.ASTMSpecimenType), standard.Trim(), true);
                        }
                        catch
                        {
                            return Envelope(false, "unknown standard '" + standard +
                                "' (e.g. ASTM_E8_Standard, ASTM_D638_TypeI, ISO_6892_1, ASTM_D3039, ASTM_D5766_OHT, Custom)", null);
                        }
                        var spParams = new TensileTest.ASTMSpecimenFactory().GetDefaultParameters(stdType);
                        if (args.ContainsKey("overrides") && args["overrides"] is Dictionary<string, object> ovr)
                            ApplySpecimenOverrides(spParams, ovr);
                        Part tPart = ResolveActivePart(true);
                        if (tPart == null)
                            return Envelope(false, "no active part and could not create a document", null);
                        var preBodies = new HashSet<DesignBody>(
                            ConformalMesh.ConformalMeshService.CollectBodies(tPart, null));
                        var specimen = new TensileTest.SpecimenModelingService()
                            .CreateTensileSpecimen(tPart, spParams);
                        if (specimen == null)
                            return Envelope(false, "specimen creation returned null", null);
                        // bind the SPECIMEN (not a grip jaw) so graph/mod/measure tools target it.
                        string tbErr = Mcp.SessionContext.Instance.BindBody(specimen, null, null);
                        if (tbErr != null) return Envelope(false, tbErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"standard\": \"" +
                            EscapeStr(stdType.ToString()) + "\"" +
                            ", \"specimen_body\": \"" + EscapeStr(specimen.Name ?? "") + "\"" +
                            ", \"bodies_created\": " + JsonStringArray(NewBodyNames(tPart, preBodies)) +
                            ", \"params\": \"" + EscapeStr(spParams.ToString().Replace("\"", "'")) + "\"}");
                    }
                    case "generate_laminate":
                    {
                        double lamW = GetDouble(args, "width_mm");
                        double lamL = GetDouble(args, "length_mm");
                        string dirStr = args.ContainsKey("stacking_direction")
                            ? GetString(args, "stacking_direction") : "Z";
                        Models.Laminate.StackingDirection lamDir;
                        try
                        {
                            lamDir = (Models.Laminate.StackingDirection)Enum.Parse(
                                typeof(Models.Laminate.StackingDirection), dirStr.Trim(), true);
                        }
                        catch { return Envelope(false, "stacking_direction must be X|Y|Z", null); }
                        var lamP = new Models.Laminate.RectangularLaminateParameters
                        { WidthMm = lamW, LengthMm = lamL, Direction = lamDir };
                        lamP.Layers = ParseLayers(args, "layers");
                        string lamErr;
                        // the service does NOT call Validate — the wrapper must.
                        if (!lamP.Validate(out lamErr)) return Envelope(false, lamErr, null);
                        Part lamPart = ResolveActivePart(true);
                        if (lamPart == null)
                            return Envelope(false, "no active part and could not create a document", null);
                        var lamBodies = new Laminate.RectangularLaminateService()
                            .CreateRectangularLaminate(lamPart, lamP);
                        if (lamBodies == null || lamBodies.Count == 0)
                            return Envelope(false, "laminate creation returned no bodies", null);
                        string lamBindErr = Mcp.SessionContext.Instance.BindBody(lamBodies[0], null, null);
                        if (lamBindErr != null) return Envelope(false, lamBindErr, null);
                        var lamNames = new List<string>();
                        foreach (var b in lamBodies) lamNames.Add(b.Name ?? "(unnamed)");
                        return Envelope(true, null, "{\"generated\": true, \"bodies_created\": " +
                            JsonStringArray(lamNames) +
                            ", \"total_thickness_mm\": " + lamP.GetTotalThicknessMm().ToString("0.###", Inv) +
                            ", \"interfaces\": " + (lamBodies.Count - 1).ToString(Inv) + "}");
                    }
                    case "parse_package_file":
                    {
                        string ppfPath = GetString(args, "path");
                        Models.Package.PackageSpec ppf;
                        try { ppf = Package.PackageFileParser.ParseFile(ppfPath); }
                        catch (Exception ex)
                        { return Envelope(false, "package parse failed: " + ex.Message, null); }
                        return Envelope(true, null, PackageSummaryJson(ppf, ppfPath));
                    }
                    case "generate_package_from_file":
                    {
                        string gpfPath = GetString(args, "path");
                        Models.Package.PackageSpec gpf;
                        try { gpf = Package.PackageFileParser.ParseFile(gpfPath); }
                        catch (Exception ex)
                        { return Envelope(false, "package parse failed: " + ex.Message, null); }

                        var gpo = new Models.Package.PackageGenOptions();
                        if (args.ContainsKey("ball_shape"))
                            gpo.BallShape = GetString(args, "ball_shape");
                        if (!string.Equals(gpo.BallShape, "cylinder", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(gpo.BallShape, "barrel", StringComparison.OrdinalIgnoreCase))
                            return Envelope(false, "ball_shape must be cylinder|barrel", null);
                        if (args.ContainsKey("barrel_bulge_ratio"))
                            gpo.BarrelBulgeRatio = GetDouble(args, "barrel_bulge_ratio");
                        if (string.Equals(gpo.BallShape, "barrel", StringComparison.OrdinalIgnoreCase)
                            && gpo.BarrelBulgeRatio <= 1.0)
                            return Envelope(false,
                                "barrel_bulge_ratio must be > 1.0 for barrel balls", null);
                        if (args.ContainsKey("barrel_slices"))
                        {
                            double gpSl = GetDouble(args, "barrel_slices");
                            if (gpSl != Math.Floor(gpSl) || gpSl < 3 || gpSl > 64)
                                return Envelope(false,
                                    "barrel_slices must be an integer in [3, 64]", null);
                            gpo.BarrelSlices = (int)gpSl;
                        }
                        if (args.ContainsKey("fill_matrix"))
                            gpo.FillMatrix = GetBool(args, "fill_matrix");
                        // a malformed filter must fail loudly: silently building ALL layers
                        // instead of the one the caller asked for is a multi-thousand-body,
                        // minutes-long mistake that cannot be undone over MCP.
                        if (args.ContainsKey("layer_filter") && args["layer_filter"] != null)
                        {
                            if (!(args["layer_filter"] is List<object> gpFilter))
                                return Envelope(false,
                                    "layer_filter must be an array of layer-name strings", null);
                            gpo.LayerFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var o in gpFilter)
                            {
                                if (!(o is string fs) || fs.Length == 0)
                                    return Envelope(false,
                                        "layer_filter items must be non-empty strings", null);
                                gpo.LayerFilter.Add(fs);
                            }
                        }

                        Part gpPart = ResolveActivePart(true);
                        if (gpPart == null)
                            return Envelope(false, "no active part and could not create a document", null);
                        DesignBody gpBound;
                        var gpRes = new Package.PackageGenerationService()
                            .BuildStack(gpPart, gpf, gpo, out gpBound);
                        if (!gpRes.Success)
                            return Envelope(false, gpRes.Error ?? "package build failed", null);
                        if (gpBound != null)
                        {
                            string gpErr = Mcp.SessionContext.Instance.BindBody(gpBound, null, null);
                            if (gpErr != null) return Envelope(false, gpErr, null);
                        }

                        var gpSb = new System.Text.StringBuilder();
                        gpSb.Append("{\"generated\": true, \"file\": \"").Append(EscapeStr(gpfPath))
                            .Append("\", \"total_bodies\": ").Append(gpRes.TotalBodies.ToString(Inv))
                            .Append(", \"total_thickness_mm\": ")
                            .Append(gpRes.TotalThicknessMm.ToString("0.###", Inv))
                            .Append(", \"ball_shape\": \"").Append(EscapeStr(gpo.BallShape.ToLowerInvariant()))
                            .Append("\", \"bound_body\": \"").Append(EscapeStr(gpRes.BoundBodyName ?? ""))
                            .Append("\", \"per_layer\": [");
                        for (int i = 0; i < gpRes.Layers.Count; i++)
                        {
                            var l = gpRes.Layers[i];
                            if (i > 0) gpSb.Append(", ");
                            gpSb.Append("{\"name\": \"").Append(EscapeStr(l.Name ?? ""))
                                .Append("\", \"z_base_mm\": ").Append(l.ZBaseMm.ToString("0.###", Inv))
                                .Append(", \"thickness_mm\": ").Append(l.ThicknessMm.ToString("0.###", Inv))
                                .Append(", \"balls\": ").Append(l.BallBodies.ToString(Inv))
                                .Append(", \"boxes\": ").Append(l.BoxBodies.ToString(Inv))
                                .Append(", \"matrix\": ").Append(l.MatrixCreated ? "true" : "false")
                                .Append(", \"skipped\": ").Append(l.Skipped ? "true" : "false")
                                .Append("}");
                        }
                        gpSb.Append("], \"log\": ").Append(JsonStringArray(gpRes.Log))
                            .Append(", \"warnings\": ").Append(JsonStringArray(gpf.Warnings)).Append("}");
                        return Envelope(true, null, gpSb.ToString());
                    }

                    // ---- fastening design (bolt/rivet on concentric hole pairs) ----
                    case "suggest_fastener":
                    {
                        var sfnSvc = new Fastener.FastenerGenerationService();
                        Part sfnPart = ScanRootPart(designBody);
                        if (sfnPart == null) return Envelope(false, "no part to scan", null);
                        var sfnSites = sfnSvc.DetectSites(sfnPart);
                        double[] sfnSeed = args.ContainsKey("seed_mm")
                            ? GetDoubleArray(args, "seed_mm", 3) : null;
                        var sfnSb = new System.Text.StringBuilder();
                        sfnSb.Append("{\"sites\": [");
                        for (int i = 0; i < sfnSites.Count; i++)
                        {
                            if (i > 0) sfnSb.Append(", ");
                            sfnSb.Append(FastenerSiteJson(sfnSites[i], sfnSeed));
                        }
                        sfnSb.Append("], \"site_count\": ").Append(sfnSites.Count.ToString(Inv))
                             .Append("}");
                        return Envelope(true, null, sfnSb.ToString());
                    }
                    case "add_fastener":
                    {
                        double[] afSeed = GetDoubleArray(args, "seed_mm", 3);
                        string afType = args.ContainsKey("type") ? GetString(args, "type") : "bolt";
                        var afSpec = new Models.Fastener.FastenerSpec();
                        if (string.Equals(afType, "rivet", StringComparison.OrdinalIgnoreCase))
                        {
                            afSpec.Type = Models.Fastener.FastenerType.Rivet;
                            afSpec.Head = Models.Fastener.HeadStyle.Dome;
                        }
                        else if (!string.Equals(afType, "bolt", StringComparison.OrdinalIgnoreCase))
                            return Envelope(false, "type must be bolt|rivet", null);
                        if (args.ContainsKey("head"))
                        {
                            var afHead = ParseHeadStyle(GetString(args, "head"));
                            if (afHead == null)
                                return Envelope(false,
                                    "head must be hex|socket_cap|pan|countersunk|dome|flat", null);
                            afSpec.Head = afHead.Value;
                        }
                        if (args.ContainsKey("thread"))
                        {
                            string afTh = GetString(args, "thread").ToLowerInvariant();
                            if (afTh == "rings") afSpec.Thread.Style = Models.Fastener.ThreadStyle.CosmeticRings;
                            else if (afTh == "simplified") afSpec.Thread.Style = Models.Fastener.ThreadStyle.Simplified;
                            else if (afTh == "none") afSpec.Thread.Style = Models.Fastener.ThreadStyle.None;
                            else return Envelope(false, "thread must be rings|simplified|none", null);
                        }
                        if (args.ContainsKey("nominal_d_mm")) afSpec.NominalDMm = GetDouble(args, "nominal_d_mm");
                        if (args.ContainsKey("length_mm")) afSpec.LengthMm = GetDouble(args, "length_mm");
                        if (args.ContainsKey("pitch_mm")) afSpec.Thread.PitchMm = GetDouble(args, "pitch_mm");
                        if (args.ContainsKey("with_nut")) afSpec.WithNut = GetBool(args, "with_nut");
                        if (args.ContainsKey("with_washer")) afSpec.WithWasher = GetBool(args, "with_washer");

                        var afSvc = new Fastener.FastenerGenerationService();
                        Part afPart = ScanRootPart(designBody);
                        if (afPart == null) return Envelope(false, "no part to scan", null);
                        List<Models.Fastener.FastenerSite> afAll;
                        var afSite = afSvc.FindNearestSite(afPart, afSeed, out afAll);
                        if (afSite == null)
                            return Envelope(false,
                                "no fastening site (coaxial cylindrical hole faces) found in the part", null);
                        double afDist = Fastener.FastenerGenerationService.SeedDistance(afSite, afSeed);
                        double afMaxDist = Math.Max(2 * afSite.HoleDiaMm, 5.0);
                        if (afDist > afMaxDist)
                            return Envelope(false, string.Format(Inv,
                                "seed is {0:0.##}mm from the nearest site axis (> {1:0.##}mm) — " +
                                "{2} site(s) exist; call suggest_fastener to list them",
                                afDist, afMaxDist, afAll.Count), null);

                        string afPrefix = args.ContainsKey("name_prefix")
                            ? GetString(args, "name_prefix") : "Fastener";
                        var afRes = afSvc.Generate(afPart, afSite, afSpec, afPrefix);
                        if (!afRes.Success)
                            return Envelope(false, afRes.Error ?? "fastener generation failed", null);

                        var afSb = new System.Text.StringBuilder();
                        afSb.Append("{\"generated\": true, \"type\": \"")
                            .Append(afSpec.Type == Models.Fastener.FastenerType.Rivet ? "rivet" : "bolt")
                            .Append("\", \"head\": \"").Append(afSpec.Head.ToString().ToLowerInvariant())
                            .Append("\", \"bodies_created\": ").Append(JsonStringArray(afRes.BodiesCreated))
                            .Append(", \"site\": ").Append(FastenerSiteJson(afSite, afSeed))
                            .Append(", \"dims_mm\": {");
                        bool afFirst = true;
                        foreach (var kv in afRes.DimsMm)
                        {
                            if (!afFirst) afSb.Append(", ");
                            afFirst = false;
                            afSb.Append("\"").Append(EscapeStr(kv.Key)).Append("\": ")
                                .Append(kv.Value.ToString("0.####", Inv));
                        }
                        afSb.Append("}}");
                        return Envelope(true, null, afSb.ToString());
                    }

                    // ---- general CAD primitives (revolve/sweep/loft/split/draft/transform) ----
                    case "revolve_profile":
                    {
                        var rvProf = ParsePointList(args, "profile_rz_mm", 3, 2);
                        double[] rvAxP = args.ContainsKey("axis_point_mm")
                            ? GetDoubleArray(args, "axis_point_mm", 3) : new double[] { 0, 0, 0 };
                        double[] rvAxD = args.ContainsKey("axis_dir")
                            ? GetDoubleArray(args, "axis_dir", 3) : new double[] { 0, 0, 1 };
                        double rvAng = GetDoubleOrDefault(args, "angle_deg", 360.0);
                        string rvName = args.ContainsKey("name") ? GetString(args, "name") : "Revolve";
                        Part rvPart = ResolveActivePart(true);
                        if (rvPart == null) return Envelope(false, "no active part", null);
                        Body rvBody;
                        try { rvBody = new CadOps.CadPrimitivesService().Revolve(
                            rvProf.ToArray(), rvAxP, rvAxD, rvAng); }
                        catch (Exception ex) { return Envelope(false, "revolve failed: " + ex.Message, null); }
                        var rvDb = Core.Geometry.BodyBuilder.CreateDesignBody(rvPart, rvName, rvBody);
                        string rvBindErr = Mcp.SessionContext.Instance.BindBody(rvDb, null, null);
                        if (rvBindErr != null) return Envelope(false, rvBindErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"body\": \"" + EscapeStr(rvDb.Name) +
                            "\", \"volume_mm3\": " + (rvDb.Shape.Volume * 1e9).ToString("0.####", Inv) + "}");
                    }
                    case "sweep_profile":
                    {
                        var swPath = ParsePointList(args, "path_mm", 2, 3);
                        double swDia = GetDouble(args, "dia_mm");
                        double swCr = GetDoubleOrDefault(args, "corner_r_mm", 0);
                        double swWall = GetDoubleOrDefault(args, "wall_mm", 0);
                        string swName = args.ContainsKey("name") ? GetString(args, "name") : "Sweep";
                        Part swPart = ResolveActivePart(true);
                        if (swPart == null) return Envelope(false, "no active part", null);
                        Body swBody;
                        try { swBody = new CadOps.CadPrimitivesService().SweepTube(
                            swPath.ToArray(), swDia, swCr, swWall); }
                        catch (Exception ex) { return Envelope(false, "sweep failed: " + ex.Message, null); }
                        var swDb = Core.Geometry.BodyBuilder.CreateDesignBody(swPart, swName, swBody);
                        string swBindErr = Mcp.SessionContext.Instance.BindBody(swDb, null, null);
                        if (swBindErr != null) return Envelope(false, swBindErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"body\": \"" + EscapeStr(swDb.Name) +
                            "\", \"volume_mm3\": " + (swDb.Shape.Volume * 1e9).ToString("0.####", Inv) + "}");
                    }
                    case "loft_profiles":
                    {
                        if (!args.ContainsKey("sections") || !(args["sections"] is List<object> lfSecs)
                            || lfSecs.Count < 2)
                            return Envelope(false, "sections must be an array of >= 2 section objects", null);
                        var lfList = new List<CadOps.CadPrimitivesService.LoftSection>();
                        foreach (var o in lfSecs)
                        {
                            if (!(o is Dictionary<string, object> sd))
                                return Envelope(false, "each section must be an object", null);
                            var sec = new CadOps.CadPrimitivesService.LoftSection();
                            if (sd.ContainsKey("shape")) sec.Shape = GetString(sd, "shape");
                            if (sd.ContainsKey("dia_mm")) sec.DiaMm = GetDouble(sd, "dia_mm");
                            if (sd.ContainsKey("w_mm")) sec.WMm = GetDouble(sd, "w_mm");
                            if (sd.ContainsKey("h_mm")) sec.HMm = GetDouble(sd, "h_mm");
                            if (sd.ContainsKey("sides")) sec.Sides = (int)GetDouble(sd, "sides");
                            if (sd.ContainsKey("rot_deg")) sec.RotDeg = GetDouble(sd, "rot_deg");
                            if (sd.ContainsKey("center_mm")) sec.CenterMm = GetDoubleArray(sd, "center_mm", 3);
                            lfList.Add(sec);
                        }
                        double[] lfAx = args.ContainsKey("axis_dir")
                            ? GetDoubleArray(args, "axis_dir", 3) : new double[] { 0, 0, 1 };
                        bool lfRuled = !args.ContainsKey("ruled") || GetBool(args, "ruled");
                        string lfName = args.ContainsKey("name") ? GetString(args, "name") : "Loft";
                        Part lfPart = ResolveActivePart(true);
                        if (lfPart == null) return Envelope(false, "no active part", null);
                        Body lfBody;
                        try { lfBody = new CadOps.CadPrimitivesService().Loft(lfList, lfAx, lfRuled); }
                        catch (Exception ex) { return Envelope(false, "loft failed: " + ex.Message, null); }
                        var lfDb = Core.Geometry.BodyBuilder.CreateDesignBody(lfPart, lfName, lfBody);
                        string lfBindErr = Mcp.SessionContext.Instance.BindBody(lfDb, null, null);
                        if (lfBindErr != null) return Envelope(false, lfBindErr, null);
                        return Envelope(true, null, "{\"generated\": true, \"body\": \"" + EscapeStr(lfDb.Name) +
                            "\", \"volume_mm3\": " + (lfDb.Shape.Volume * 1e9).ToString("0.####", Inv) + "}");
                    }
                    case "split_body":
                    {
                        DesignBody spTarget = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            spTarget = ResolveBodyByName(ScanRootPart(designBody), GetString(args, "body_name"));
                            if (spTarget == null)
                                return Envelope(false, "body not found: " + GetString(args, "body_name"), null);
                        }
                        double[] spP = GetDoubleArray(args, "plane_point_mm", 3);
                        double[] spN = GetDoubleArray(args, "plane_normal", 3);
                        bool spDel = !args.ContainsKey("delete_original") || GetBool(args, "delete_original");
                        string spBase = spTarget.Name ?? "Body";
                        string spNb = args.ContainsKey("name_below") ? GetString(args, "name_below") : spBase + "_Below";
                        string spNa = args.ContainsKey("name_above") ? GetString(args, "name_above") : spBase + "_Above";
                        Part spPart = spTarget.Parent as Part;
                        Body spBelow, spAbove;
                        double spVol0 = spTarget.Shape.Volume * 1e9;
                        try { new CadOps.CadPrimitivesService().SplitByPlane(
                            spTarget.Shape, spP, spN, out spBelow, out spAbove); }
                        catch (Exception ex) { return Envelope(false, "split failed: " + ex.Message, null); }
                        // a split that leaves either side empty did not actually cut the
                        // body — succeeding with one whole-volume piece would silently
                        // rename+rebind under the caller.
                        if (spBelow == null || spAbove == null)
                            return Envelope(false, "the plane does not cut the body (one side is empty)", null);
                        var spNames = new List<string>();
                        double spVb = 0, spVa = 0;
                        DesignBody spFirstPiece = null;
                        try
                        {
                            var dbB = Core.Geometry.BodyBuilder.CreateDesignBody(spPart, spNb, spBelow);
                            spNames.Add(dbB.Name); spVb = dbB.Shape.Volume * 1e9;
                            spFirstPiece = dbB;
                            var dbA = Core.Geometry.BodyBuilder.CreateDesignBody(spPart, spNa, spAbove);
                            spNames.Add(dbA.Name); spVa = dbA.Shape.Volume * 1e9;
                            if (spDel) spTarget.Delete();
                        }
                        finally
                        {
                            // deterministic rebind: point the session at a PIECE, not at
                            // whatever body happens to be first in the document.
                            if (designBody.IsDeleted)
                            {
                                if (spFirstPiece != null && !spFirstPiece.IsDeleted)
                                    Mcp.SessionContext.Instance.BindBody(spFirstPiece, null, null);
                                else
                                { string spIgn; Mcp.SessionContext.Instance.BindActiveBody(out spIgn); }
                            }
                        }
                        return Envelope(true, null, "{\"split\": true, \"bodies_created\": " +
                            JsonStringArray(spNames) +
                            ", \"volume_below_mm3\": " + spVb.ToString("0.####", Inv) +
                            ", \"volume_above_mm3\": " + spVa.ToString("0.####", Inv) +
                            ", \"volume_original_mm3\": " + spVol0.ToString("0.####", Inv) +
                            ", \"session_bound_to\": \"" + EscapeStr(designBody.IsDeleted && spFirstPiece != null
                                ? spFirstPiece.Name : (designBody.Name ?? "")) + "\"}");
                    }
                    case "draft_body":
                    {
                        DesignBody drTarget = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            drTarget = ResolveBodyByName(ScanRootPart(designBody), GetString(args, "body_name"));
                            if (drTarget == null)
                                return Envelope(false, "body not found: " + GetString(args, "body_name"), null);
                        }
                        double[] drP = GetDoubleArray(args, "neutral_point_mm", 3);
                        double[] drDir = args.ContainsKey("pull_dir")
                            ? GetDoubleArray(args, "pull_dir", 3) : new double[] { 0, 0, 1 };
                        double drAng = GetDouble(args, "angle_deg");
                        if (Math.Abs(drAng) <= 0 || Math.Abs(drAng) > 30)
                            return Envelope(false, "angle_deg must be in (0, 30] (or negative)", null);
                        double drV0 = drTarget.Shape.Volume * 1e9;
                        int drN, drSkipped;
                        try { drN = new CadOps.CadPrimitivesService().DraftSideFaces(
                            drTarget.Shape, drP, drDir, drAng, out drSkipped); }
                        catch (Exception ex) { return Envelope(false, "draft failed: " + ex.Message, null); }
                        if (drN == 0) return Envelope(false, "no planar side faces found to draft" +
                            (drSkipped > 0 ? " (" + drSkipped.ToString(Inv) + " non-planar face(s) skipped)" : ""), null);
                        return Envelope(true, null, "{\"drafted\": true, \"faces\": " + drN.ToString(Inv) +
                            ", \"skipped_nonplanar_faces\": " + drSkipped.ToString(Inv) +
                            ", \"volume_before_mm3\": " + drV0.ToString("0.####", Inv) +
                            ", \"volume_after_mm3\": " + (drTarget.Shape.Volume * 1e9).ToString("0.####", Inv) + "}");
                    }
                    case "transform_body":
                    {
                        DesignBody tbTarget = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            tbTarget = ResolveBodyByName(ScanRootPart(designBody), GetString(args, "body_name"));
                            if (tbTarget == null)
                                return Envelope(false, "body not found: " + GetString(args, "body_name"), null);
                        }
                        string tbOp = GetString(args, "op").ToLowerInvariant();
                        bool tbCopy = args.ContainsKey("copy") && GetBool(args, "copy");
                        int tbCount = args.ContainsKey("count") ? (int)GetDouble(args, "count") : 1;
                        if (tbCount < 1 || tbCount > 200)
                            return Envelope(false, "count must be in [1, 200]", null);
                        // a pattern always keeps the original — copy=false with count>1
                        // would be silently promoted to copy=true, so reject it loudly.
                        if (!tbCopy && tbCount > 1 && args.ContainsKey("copy"))
                            return Envelope(false,
                                "copy=false requires count=1 (in-place transform); " +
                                "patterns keep the original - use copy=true with count", null);
                        Part tbPart = tbTarget.Parent as Part;
                        var tbNames = new List<string>();
                        Func<int, Matrix> tbStep;
                        switch (tbOp)
                        {
                            case "move":
                            {
                                double[] t = GetDoubleArray(args, "translation_mm", 3);
                                tbStep = i => Matrix.CreateTranslation(Vector.Create(
                                    Core.Geometry.GeometryUtils.MmToMeters(t[0] * i),
                                    Core.Geometry.GeometryUtils.MmToMeters(t[1] * i),
                                    Core.Geometry.GeometryUtils.MmToMeters(t[2] * i)));
                                break;
                            }
                            case "rotate":
                            {
                                double[] ap = args.ContainsKey("axis_point_mm")
                                    ? GetDoubleArray(args, "axis_point_mm", 3) : new double[] { 0, 0, 0 };
                                double[] ad = args.ContainsKey("axis_dir")
                                    ? GetDoubleArray(args, "axis_dir", 3) : new double[] { 0, 0, 1 };
                                double ang = GetDouble(args, "angle_deg") * Math.PI / 180.0;
                                var line = Line.Create(Point.Create(
                                        Core.Geometry.GeometryUtils.MmToMeters(ap[0]),
                                        Core.Geometry.GeometryUtils.MmToMeters(ap[1]),
                                        Core.Geometry.GeometryUtils.MmToMeters(ap[2])),
                                    Direction.Create(ad[0], ad[1], ad[2]));
                                tbStep = i => Matrix.CreateRotation(line, ang * i);
                                break;
                            }
                            case "scale":
                            {
                                double f = GetDouble(args, "factor");
                                if (f <= 0) return Envelope(false, "factor must be > 0", null);
                                if (tbCount != 1) return Envelope(false, "scale does not support count > 1", null);
                                var bbT = tbTarget.Shape.GetBoundingBox(Matrix.Identity);
                                var ctr = Point.Create(
                                    (bbT.MinCorner.X + bbT.MaxCorner.X) / 2,
                                    (bbT.MinCorner.Y + bbT.MaxCorner.Y) / 2,
                                    (bbT.MinCorner.Z + bbT.MaxCorner.Z) / 2);
                                tbStep = i => Matrix.CreateScale(f, ctr);
                                break;
                            }
                            default:
                                return Envelope(false, "op must be move|rotate|scale", null);
                        }
                        if (!tbCopy && tbCount == 1)
                        {
                            tbTarget.Shape.Transform(tbStep(1));
                            tbNames.Add(tbTarget.Name ?? "(unnamed)");
                        }
                        else
                        {
                            for (int i = 1; i <= tbCount; i++)
                            {
                                Body cp = tbTarget.Shape.Copy();
                                cp.Transform(tbStep(i));
                                var db = Core.Geometry.BodyBuilder.CreateDesignBody(tbPart,
                                    (tbTarget.Name ?? "Body") + "_" + i.ToString(Inv), cp);
                                tbNames.Add(db.Name);
                            }
                        }
                        return Envelope(true, null, "{\"transformed\": true, \"op\": \"" + EscapeStr(tbOp) +
                            "\", \"bodies\": " + JsonStringArray(tbNames) + "}");
                    }

                    // ---- CAE mutators on existing bodies ---------------------
                    case "laminate_body":
                    {
                        DesignBody lbTarget = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            string lbName = GetString(args, "body_name");
                            lbTarget = ResolveBodyByName(ScanRootPart(designBody), lbName);
                            if (lbTarget == null) return Envelope(false, "body not found: " + lbName, null);
                        }
                        var slP = new Models.Laminate.SolidLaminateParameters();
                        slP.Layers = ParseLayers(args, "layers");
                        slP.DeleteOriginalBody = !args.ContainsKey("delete_original") || GetBool(args, "delete_original");
                        string slErr;
                        if (!slP.Validate(out slErr)) return Envelope(false, slErr, null);
                        var slSvc = new Laminate.SolidLaminateService();
                        var slA = slSvc.AnalyzeSolid(lbTarget);
                        if (slA == null || !slA.IsValid)
                            return Envelope(false, "solid analysis failed: " +
                                (slA != null ? (slA.ErrorMessage ?? "(no message)") : "(null)"), null);
                        double slSum = slP.GetTotalThicknessMm();
                        double slTol = Math.Max(0.02, slA.ThicknessMm * 0.01);
                        if (Math.Abs(slSum - slA.ThicknessMm) > slTol)
                            return Envelope(false, string.Format(Inv,
                                "layer thickness sum {0:0.###}mm != detected body thickness {1:0.###}mm",
                                slSum, slA.ThicknessMm), null);
                        // stale-session guard runs in finally: the service deletes the source
                        // BEFORE slicing, so a mid-slice throw must still rebind the session.
                        List<DesignBody> plies = null;
                        try
                        {
                            plies = slSvc.CreateSolidLaminate(lbTarget.Parent as Part, lbTarget, slA, slP);
                        }
                        finally
                        {
                            if (designBody.IsDeleted)
                            { string slIgn; Mcp.SessionContext.Instance.BindActiveBody(out slIgn); }
                        }
                        var plyNames = new List<string>();
                        if (plies != null) foreach (var b in plies) plyNames.Add(b.Name ?? "(unnamed)");
                        return Envelope(true, null, "{\"layers_created\": " + JsonStringArray(plyNames) +
                            ", \"detected_thickness_mm\": " + slA.ThicknessMm.ToString("0.###", Inv) +
                            ", \"stacking_direction\": [" + slA.StackingNormal.X.ToString("0.###", Inv) + ", " +
                            slA.StackingNormal.Y.ToString("0.###", Inv) + ", " +
                            slA.StackingNormal.Z.ToString("0.###", Inv) + "]}");
                    }
                    case "cut_void":
                    {
                        string shapeStr = GetString(args, "shape");
                        VoidCut.CutterShape cvShape;
                        try
                        {
                            cvShape = (VoidCut.CutterShape)Enum.Parse(
                                typeof(VoidCut.CutterShape), shapeStr.Trim(), true);
                        }
                        catch { return Envelope(false, "shape must be Cuboid|Cylinder|Sphere", null); }
                        string modeStr = GetString(args, "mode");
                        VoidCut.BooleanMode cvMode;
                        try
                        {
                            cvMode = (VoidCut.BooleanMode)Enum.Parse(
                                typeof(VoidCut.BooleanMode), modeStr.Trim(), true);
                        }
                        catch { return Envelope(false, "mode must be Subtract|Unite|Intersect", null); }
                        double cvD1 = GetDouble(args, "dim1_mm");
                        double cvD2 = GetDoubleOrDefault(args, "dim2_mm", 0.0);
                        double cvD3 = GetDoubleOrDefault(args, "dim3_mm", 0.0);
                        if (cvD1 <= 0) return Envelope(false, "dim1_mm must be > 0", null);
                        if (cvShape == VoidCut.CutterShape.Cuboid && (cvD2 <= 0 || cvD3 <= 0))
                            return Envelope(false,
                                "Cuboid needs dim1_mm(width) dim2_mm(height) dim3_mm(depth) all > 0", null);
                        if (cvShape == VoidCut.CutterShape.Cylinder && cvD2 <= 0)
                            return Envelope(false,
                                "Cylinder needs dim1_mm(RADIUS) and dim2_mm(height) > 0", null);
                        double[] cvPos = GetDoubleArray(args, "position_mm", 3);
                        double cvOff = GetDoubleOrDefault(args, "offset_mm", 0.0);
                        bool cvNS = args.ContainsKey("create_named_selection")
                            && GetBool(args, "create_named_selection");
                        Part cvPart = ScanRootPart(designBody);
                        var cvTargets = new List<DesignBody>();
                        if (args.ContainsKey("target_body_names")
                            && args["target_body_names"] is List<object> cvTn && cvTn.Count > 0)
                        {
                            foreach (var t in cvTn)
                            {
                                var tb = ResolveBodyByName(cvPart, t != null ? t.ToString() : null);
                                if (tb == null) return Envelope(false, "target body not found: " + t, null);
                                cvTargets.Add(tb);
                            }
                        }
                        else cvTargets.Add(designBody);
                        VoidCut.VoidCutResult cvRes = null;
                        try
                        {
                            cvRes = VoidCut.VoidCutService.ExecuteWithShape(cvPart, cvShape,
                                cvD1, cvD2, cvD3, cvPos[0], cvPos[1], cvPos[2],
                                cvTargets, cvMode, cvOff, true, cvNS, true);
                        }
                        finally
                        {
                            // stale-session guard: Subtract can fully consume the session body —
                            // rebind even on the exception path (finally) so the session heals.
                            if (designBody.IsDeleted)
                            { string cvIgn; Mcp.SessionContext.Instance.BindActiveBody(out cvIgn); }
                        }
                        if (cvRes == null) return Envelope(false, "cut_void returned null", null);
                        // The service counts a THROWN Boolean as SKIPPED (FailedCount is never
                        // incremented on this path) — gate success on SkippedCount.
                        bool cvOk = cvRes.SkippedCount == 0 && cvRes.SubtractedCount > 0;
                        string cvPayload = "{\"processed\": " + cvRes.SubtractedCount.ToString(Inv) +
                            ", \"skipped\": " + cvRes.SkippedCount.ToString(Inv) +
                            ", \"volume_before_mm3\": " + cvRes.TotalVolumeBeforeMm3.ToString("0.###", Inv) +
                            ", \"volume_after_mm3\": " + cvRes.TotalVolumeAfterMm3.ToString("0.###", Inv) +
                            ", \"named_selections\": " + cvRes.NamedSelectionsCreated.ToString(Inv) +
                            ", \"log\": " + JsonStringArray(cvRes.Log) + "}";
                        return Envelope(cvOk, cvOk ? null
                            : ("cut_void: " + cvRes.SkippedCount + " skipped(failed) / " +
                               cvRes.SubtractedCount + " processed - see log"), cvPayload);
                    }
                    case "simplify_bodies":
                    {
                        string simKw = GetString(args, "keyword");
                        string simModeStr = GetString(args, "mode");
                        Models.Simplify.SimplifyMode simMode;
                        try
                        {
                            simMode = (Models.Simplify.SimplifyMode)Enum.Parse(
                                typeof(Models.Simplify.SimplifyMode), simModeStr.Trim(), true);
                        }
                        catch { return Envelope(false, "mode must be BoundingBox|SolidToShell", null); }
                        // stale-session guard runs in finally: the keyword can match (and delete)
                        // the session body, including on a partial-failure exception path.
                        Models.Simplify.SimplifyResult simRes = null;
                        try
                        {
                            simRes = Simplify.SimplifyService.Execute(
                                designBody.Parent as Part, simKw, simMode);
                        }
                        finally
                        {
                            if (designBody.IsDeleted)
                            { string simIgn; Mcp.SessionContext.Instance.BindActiveBody(out simIgn); }
                        }
                        if (simRes == null) return Envelope(false, "simplify returned null", null);
                        bool simOk = simRes.FailedCount == 0;
                        string simPayload = "{\"matched\": " + simRes.MatchedCount.ToString(Inv) +
                            ", \"processed\": " + simRes.ProcessedCount.ToString(Inv) +
                            ", \"failed\": " + simRes.FailedCount.ToString(Inv) +
                            ", \"log\": " + JsonStringArray(simRes.Log) + "}";
                        return Envelope(simOk, simOk ? null
                            : ("simplify: " + simRes.FailedCount + " body(ies) failed - see log"), simPayload);
                    }
                    case "create_bending_fixture":
                    {
                        DesignBody bfTarget = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            string bfName = GetString(args, "body_name");
                            bfTarget = ResolveBodyByName(ScanRootPart(designBody), bfName);
                            if (bfTarget == null) return Envelope(false, "body not found: " + bfName, null);
                        }
                        var bfSvc = new BendingFixture.BendingFixtureService();
                        var bfP = new Models.BendingFixture.BendingFixtureParameters();
                        if (args.ContainsKey("span_mm"))
                        { bfP.UseSpanRatio = false; bfP.SpanMm = GetDouble(args, "span_mm"); }
                        else if (args.ContainsKey("span_ratio"))
                        { bfP.UseSpanRatio = true; bfP.SpanRatio = GetDouble(args, "span_ratio"); }
                        bfP.SupportDiameter = GetDoubleOrDefault(args, "support_diameter_mm", bfP.SupportDiameter);
                        bfP.LoadingNoseDiameter = GetDoubleOrDefault(args, "nose_diameter_mm", bfP.LoadingNoseDiameter);
                        // MANDATORY pre-step: DetectDirections populates ComputedSpanMm/Body*Mm —
                        // the DesignBody overload of CreateFixtures does NOT, and skipping it yields
                        // degenerate geometry with no error.
                        var bfBox = bfSvc.ComputeBoundingBox(bfTarget);
                        bfSvc.DetectDirections(bfBox, bfP);
                        string bfErr;
                        if (!bfP.Validate(out bfErr)) return Envelope(false, bfErr, null);
                        var fixtures = bfSvc.CreateFixtures(bfTarget.Parent as Part, bfTarget, bfP);
                        var bfNames = new List<string>();
                        if (fixtures != null) foreach (var b in fixtures) bfNames.Add(b.Name ?? "(unnamed)");
                        return Envelope(true, null, "{\"bodies_created\": " + JsonStringArray(bfNames) +
                            ", \"span_mm\": " + bfP.ComputedSpanMm.ToString("0.###", Inv) +
                            ", \"span_axis\": \"" + bfP.SpanDirection.ToString() + "\"" +
                            ", \"loading_axis\": \"" + bfP.LoadingDirection.ToString() + "\"}");
                    }

                    // ---- read-only assembly inspection -----------------------
                    case "detect_contacts":
                    {
                        double dcTol = GetDoubleOrDefault(args, "tolerance_mm", 0.1);
                        string dcKw = args.ContainsKey("keyword") ? GetString(args, "keyword") : null;
                        bool dcPl = !args.ContainsKey("detect_planar") || GetBool(args, "detect_planar");
                        bool dcCy = !args.ContainsKey("detect_cylindrical") || GetBool(args, "detect_cylindrical");
                        var dcBodies = ConformalMesh.ConformalMeshService.CollectBodies(
                            ScanRootPart(designBody), dcKw);
                        if (dcBodies == null || dcBodies.Count < 2)
                            return Envelope(true, null,
                                "{\"pairs\": [], \"count\": 0, \"note\": \"fewer than 2 bodies matched\"}");
                        var dcPairs = ConformalMesh.ConformalMeshService.DetectInterfaces(
                            dcBodies, dcTol, dcPl, dcCy);
                        var dcSb = new StringBuilder("[");
                        for (int i = 0; i < dcPairs.Count; i++)
                        {
                            var pr5 = dcPairs[i];
                            if (i > 0) dcSb.Append(", ");
                            dcSb.Append("{\"body_a\": \"")
                                .Append(EscapeStr(pr5.BodyA != null ? (pr5.BodyA.Name ?? "") : "")).Append("\"");
                            dcSb.Append(", \"body_b\": \"")
                                .Append(EscapeStr(pr5.BodyB != null ? (pr5.BodyB.Name ?? "") : "")).Append("\"");
                            dcSb.Append(", \"type\": \"")
                                .Append(pr5.Type.ToString().ToLowerInvariant()).Append("\"");
                            dcSb.Append(", \"area_mm2\": ").Append(pr5.TotalAreaMm2.ToString("0.###", Inv));
                            dcSb.Append("}");
                        }
                        dcSb.Append("]");
                        return Envelope(true, null, "{\"pairs\": " + dcSb.ToString() +
                            ", \"count\": " + dcPairs.Count.ToString(Inv) + "}");
                    }

                    // ---- licensed-seat CAE tools (STEP-export / SC-meshing gated on
                    //      ANSYS Student; they fail GRACEFULLY there with the full log) ----
                    case "mesh_with_gmsh":
                    {
                        var gmp = new Models.GmshMesher.GmshMeshParameters();
                        gmp.GlobalElementSizeMm = GetDoubleOrDefault(args, "element_size_mm", gmp.GlobalElementSizeMm);
                        gmp.GrowthRate = GetDoubleOrDefault(args, "growth_rate", gmp.GrowthRate);
                        if (args.ContainsKey("shape"))
                        {
                            try
                            {
                                gmp.Shape = (Models.GmshMesher.ElementShape)Enum.Parse(
                                    typeof(Models.GmshMesher.ElementShape), GetString(args, "shape").Trim(), true);
                            }
                            catch { return Envelope(false, "shape must be Tet|Hex", null); }
                        }
                        if (args.ContainsKey("algorithm"))
                        {
                            try
                            {
                                gmp.Algorithm = (Models.GmshMesher.MeshAlgorithm)Enum.Parse(
                                    typeof(Models.GmshMesher.MeshAlgorithm), GetString(args, "algorithm").Trim(), true);
                            }
                            catch { return Envelope(false, "algorithm must be Auto|Delaunay|Frontal|HXT", null); }
                        }
                        if (args.ContainsKey("midside_nodes")) gmp.MidsideNodes = GetBool(args, "midside_nodes");
                        Part gmPart = designBody.Parent as Part;
                        if (gmPart == null) return Envelope(false, "body has no parent Part", null);
                        if (args.ContainsKey("body_overrides")
                            && args["body_overrides"] is List<object> gmOv && gmOv.Count > 0)
                        {
                            // gmsh volume tag = 1-based STEP import order = part body order; the
                            // geo writer adds the +1 itself (BodyIndex is 0-based).
                            var order = new List<DesignBody>();
                            foreach (DesignBody b in gmPart.Bodies) order.Add(b);
                            foreach (var item in gmOv)
                            {
                                var oo = item as Dictionary<string, object>;
                                if (oo == null)
                                    return Envelope(false, "body_overrides items must be {body_name, element_size_mm}", null);
                                string ovName = GetString(oo, "body_name");
                                double ovSize = GetDouble(oo, "element_size_mm");
                                int ovIdx = order.FindIndex(
                                    b => string.Equals(b.Name, ovName, StringComparison.OrdinalIgnoreCase));
                                if (ovIdx < 0) return Envelope(false, "override body not found: " + ovName, null);
                                gmp.BodyOverrides.Add(new Models.GmshMesher.BodyMeshOverride(ovName, ovIdx, ovSize));
                            }
                        }
                        var mesh = GmshMesher.GmshMeshService.RunMeshPipeline(gmPart, null, gmp);
                        if (mesh == null)
                            return Envelope(false,
                                "gmsh pipeline failed - see log (STEP export is license-refused on ANSYS Student seats)",
                                "{\"log\": " + JsonStringArray(GmshMesher.GmshMeshService.DiagnosticLog) + "}");
                        string kOut = "null";
                        if (args.ContainsKey("kfile_path"))
                        {
                            string kPath = GetString(args, "kfile_path");
                            GmshMesher.GmshKFileWriter.WriteKFile(
                                kPath, mesh, gmPart, GmshMesher.GmshMeshService.DiagnosticLog);
                            kOut = "\"" + EscapeStr(kPath) + "\"";
                        }
                        return Envelope(true, null, "{\"nodes\": " + mesh.TotalNodeCount.ToString(Inv) +
                            ", \"volume_elements\": " + mesh.VolumeElements.Count.ToString(Inv) +
                            ", \"surface_elements\": " + mesh.SurfaceElements.Count.ToString(Inv) +
                            ", \"kfile\": " + kOut +
                            ", \"log\": " + JsonStringArray(GmshMesher.GmshMeshService.DiagnosticLog) + "}");
                    }
                    case "conformal_mesh":
                    {
                        var cmp = new Models.ConformalMesh.ConformalMeshParameters();
                        // PINNED: never import over MCP — Document.Open(.stp) is the headless-hang
                        // path; the current part's bodies are the workflow input.
                        cmp.ImportMode = Models.ConformalMesh.StepImportMode.UseCurrentPart;
                        cmp.ToleranceMm = GetDoubleOrDefault(args, "tolerance_mm", cmp.ToleranceMm);
                        cmp.BodyKeyword = args.ContainsKey("keyword") ? GetString(args, "keyword") : "";
                        cmp.DetectPlanar = !args.ContainsKey("detect_planar") || GetBool(args, "detect_planar");
                        cmp.DetectCylindrical = !args.ContainsKey("detect_cylindrical") || GetBool(args, "detect_cylindrical");
                        cmp.EnableShareTopology = !args.ContainsKey("share_topology") || GetBool(args, "share_topology");
                        cmp.CreateInterfaceNamedSelections = !args.ContainsKey("create_named_selections")
                            || GetBool(args, "create_named_selections");
                        cmp.ElementSizeMm = GetDoubleOrDefault(args, "element_size_mm", cmp.ElementSizeMm);
                        cmp.GrowthRate = GetDoubleOrDefault(args, "growth_rate", cmp.GrowthRate);
                        if (args.ContainsKey("strategy"))
                        {
                            try
                            {
                                cmp.Strategy = (Models.ConformalMesh.MeshStrategy)Enum.Parse(
                                    typeof(Models.ConformalMesh.MeshStrategy), GetString(args, "strategy").Trim(), true);
                            }
                            catch { return Envelope(false, "strategy must be AutoTet|AutoHex|Mixed", null); }
                        }
                        if (args.ContainsKey("midside_nodes")) cmp.MidsideNodes = GetBool(args, "midside_nodes");
                        if (args.ContainsKey("split_cylinder_edges"))
                            cmp.SplitCylinderEdges = GetBool(args, "split_cylinder_edges");
                        cmp.CylinderEdgeDivisions = (int)GetDoubleOrDefault(args, "cylinder_divisions", cmp.CylinderEdgeDivisions);
                        if (args.ContainsKey("export_path"))
                        {
                            cmp.AutoExport = true;
                            cmp.ExportPath = GetString(args, "export_path");
                            if (args.ContainsKey("export_format"))
                                cmp.ExportFormat = GetString(args, "export_format");
                        }
                        string cmErr;
                        if (!cmp.Validate(out cmErr)) return Envelope(false, cmErr, null);
                        // SC meshing/export commands (InitMeshSettings/SaveDYNA/...) are
                        // ACTIVE-document scoped; meshing one doc while another is active
                        // would silently mesh/export the wrong one. Fail fast instead.
                        if (Window.ActiveWindow == null
                            || Window.ActiveWindow.Document != designBody.Document)
                            return Envelope(false,
                                "the session body's document is not the ACTIVE window (SC meshing/export are active-document scoped) - activate it or call rebind_active_body", null);
                        bool meshOk, exportOk;
                        var cmPairs = ConformalMesh.ConformalMeshService.ExecuteWorkflow(
                            ScanRootPart(designBody), cmp, out meshOk, out exportOk);
                        var cmSb = new StringBuilder("[");
                        for (int i = 0; i < cmPairs.Count; i++)
                        {
                            var pr6 = cmPairs[i];
                            if (i > 0) cmSb.Append(", ");
                            cmSb.Append("{\"body_a\": \"")
                                .Append(EscapeStr(pr6.BodyA != null ? (pr6.BodyA.Name ?? "") : "")).Append("\"");
                            cmSb.Append(", \"body_b\": \"")
                                .Append(EscapeStr(pr6.BodyB != null ? (pr6.BodyB.Name ?? "") : "")).Append("\"");
                            cmSb.Append(", \"type\": \"")
                                .Append(pr6.Type.ToString().ToLowerInvariant()).Append("\"");
                            cmSb.Append(", \"area_mm2\": ").Append(pr6.TotalAreaMm2.ToString("0.###", Inv));
                            cmSb.Append("}");
                        }
                        cmSb.Append("]");
                        string cmPayload = "{\"mesh_ok\": " + (meshOk ? "true" : "false") +
                            ", \"export_ok\": " + (exportOk ? "true" : "false") +
                            ", \"interfaces\": " + cmPairs.Count.ToString(Inv) +
                            ", \"pairs\": " + cmSb.ToString() +
                            ", \"log\": " + JsonStringArray(ConformalMesh.ConformalMeshService.DiagnosticLog) + "}";
                        bool cmOk = meshOk && exportOk;
                        return Envelope(cmOk, cmOk ? null
                            : (!meshOk
                                ? "conformal mesh failed at the meshing step - see log"
                                : "mesh succeeded but the requested export was NOT written - see log"),
                            cmPayload);
                    }
                    case "surface_laminate":
                    {
                        DesignBody sfBody = designBody;
                        if (args.ContainsKey("body_name"))
                        {
                            string sfName = GetString(args, "body_name");
                            sfBody = ResolveBodyByName(ScanRootPart(designBody), sfName);
                            if (sfBody == null) return Envelope(false, "body not found: " + sfName, null);
                        }
                        double[] sfSeed = GetDoubleArray(args, "seed_mm", 3);
                        var sfFace = ResolveNearestPlanarFace(sfBody, sfSeed);
                        if (sfFace == null)
                            return Envelope(false,
                                "no planar face found near seed_mm (surface_laminate needs a PLANAR face; seed a point on it)", null);
                        var sfP = new Models.Laminate.SurfaceLaminateParameters();
                        sfP.Layers = ParseLayers(args, "layers");
                        if (args.ContainsKey("direction"))
                        {
                            string sfDir = GetString(args, "direction").Trim().ToLowerInvariant();
                            if (sfDir == "reverse") sfP.Direction = Models.Laminate.OffsetDirection.Reverse;
                            else if (sfDir == "normal") sfP.Direction = Models.Laminate.OffsetDirection.Normal;
                            else return Envelope(false, "direction must be normal|reverse", null);
                        }
                        // The service stacks along the PLANE's DirZ, which is the face's OUTWARD
                        // normal only when !IsReversed (the ModificationService sign idiom). Flip
                        // so the tool's "normal" ALWAYS means outward — otherwise plies extrude
                        // INTO the body on the ~half of faces that are reversed.
                        if (sfFace.Shape.IsReversed)
                            sfP.Direction = (sfP.Direction == Models.Laminate.OffsetDirection.Normal)
                                ? Models.Laminate.OffsetDirection.Reverse
                                : Models.Laminate.OffsetDirection.Normal;
                        string sfErr;
                        if (!sfP.Validate(out sfErr)) return Envelope(false, sfErr, null);
                        var sfMade = new Laminate.SurfaceLaminateService().CreateSurfaceLaminate(
                            sfBody.Parent as Part, sfFace, sfP);
                        var sfNames = new List<string>();
                        if (sfMade != null) foreach (var b in sfMade) sfNames.Add(b.Name ?? "(unnamed)");
                        return Envelope(true, null, "{\"layers_created\": " + JsonStringArray(sfNames) +
                            ", \"total_thickness_mm\": " + sfP.GetTotalThicknessMm().ToString("0.###", Inv) + "}");
                    }

                    default:
                        return Envelope(false, "Unknown toolName: " + toolName, null);
                }
            }
            catch (Exception ex)
            {
                return Envelope(false, "Dispatch threw: " + ex.Message, null);
            }
        }

        // ----------------------------------------------------------------
        // Result envelope builders
        // ----------------------------------------------------------------
        private static string Envelope(bool success, string error, string resultJsonOrNull)
        {
            var sb = new StringBuilder();
            sb.Append("{\"success\": ").Append(success ? "true" : "false");
            sb.Append(", \"error\": ");
            if (string.IsNullOrEmpty(error)) sb.Append("null");
            else sb.Append("\"").Append(EscapeStr(error)).Append("\"");
            sb.Append(", \"result\": ");
            if (string.IsNullOrEmpty(resultJsonOrNull)) sb.Append("null");
            else sb.Append(resultJsonOrNull);
            sb.Append("}");
            return sb.ToString();
        }

        private static string EnvelopeFromResult(ModificationResult r)
        {
            if (r == null) return Envelope(false, "ModificationResult is null", null);
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"operation\": \"").Append(EscapeStr(r.Operation ?? "")).Append("\", ");
            sb.Append("\"modified_face_indices\": [");
            if (r.ModifiedFaceIndices != null)
            {
                for (int i = 0; i < r.ModifiedFaceIndices.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(r.ModifiedFaceIndices[i].ToString(Inv));
                }
            }
            sb.Append("], ");
            int newEdges = (r.NewlyCreatedEdges != null) ? r.NewlyCreatedEdges.Count : 0;
            sb.Append("\"new_edge_count\": ").Append(newEdges.ToString(Inv));
            // kernel-truth telemetry (always-on when the op measured it)
            if (!double.IsNaN(r.VolumeDeltaMm3))
                sb.Append(", \"volume_delta_mm3\": ").Append(r.VolumeDeltaMm3.ToString("0.###", Inv));
            sb.Append("}");
            return Envelope(r.Success, r.Success ? null : (r.ErrorMessage ?? "(no message)"), sb.ToString());
        }

        // ----------------------------------------------------------------
        // find_features_by_type implementation
        // ----------------------------------------------------------------
        private static List<string> FindFeaturesByType(FeatureGraph graph, string ftype, double minV, double maxV)
        {
            var ids = new List<string>();
            if (graph == null || string.IsNullOrEmpty(ftype)) return ids;
            string t = ftype.Trim().ToLowerInvariant();
            if (t == "hole" && graph.Holes != null)
            {
                foreach (var h in graph.Holes)
                {
                    if (h.DiameterMm >= minV && h.DiameterMm <= maxV) ids.Add(h.Id);
                }
            }
            else if (t == "boss" && graph.Bosses != null)
            {
                foreach (var b in graph.Bosses)
                {
                    if (b.DiameterMm >= minV && b.DiameterMm <= maxV) ids.Add(b.Id);
                }
            }
            else if (t == "wall" && graph.Walls != null)
            {
                foreach (var w in graph.Walls)
                {
                    if (w.ThicknessMm >= minV && w.ThicknessMm <= maxV) ids.Add(w.Id);
                }
            }
            else if ((t == "fillet_chain" || t == "filletchain") && graph.FilletChains != null)
            {
                foreach (var fc in graph.FilletChains)
                {
                    if (fc.RadiusMm >= minV && fc.RadiusMm <= maxV) ids.Add(fc.Id);
                }
            }
            else if (t == "slit" && graph.Slits != null)
            {
                foreach (var s in graph.Slits)
                {
                    // Slits have no single primary dim; include all (min/max ignored).
                    ids.Add(s.Id);
                }
            }
            return ids;
        }

        private static string BuildIdsResultJson(string ftype, List<string> ids)
        {
            var sb = new StringBuilder();
            sb.Append("{\"feature_type\": \"").Append(EscapeStr(ftype ?? "")).Append("\", \"ids\": [");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(EscapeStr(ids[i])).Append("\"");
            }
            sb.Append("], \"count\": ").Append(ids.Count.ToString(Inv)).Append("}");
            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // CAE tool-surface helpers (part/body resolution + payload builders)
        // ----------------------------------------------------------------

        /// <summary>Document-scope Part for tools that SCAN the whole body tree
        /// (detect_contacts / conformal_mesh): assembly bodies live in component-content
        /// parts, so designBody.Parent alone sees only 1 body and the scan silently comes
        /// up empty. MainPart (CollectBodies recurses components DOWN from it) with the
        /// local Parent as fallback for bodies in internal parts not under MainPart.</summary>
        private static Part ScanRootPart(DesignBody body)
        {
            try
            {
                if (body != null && body.Document != null && body.Document.MainPart != null)
                    return body.Document.MainPart;
            }
            catch { }
            return body != null ? body.Parent as Part : null;
        }

        /// <summary>Active MainPart; optionally create a fresh document when none is open.</summary>
        private static Part ResolveActivePart(bool createIfNone)
        {
            var win = Window.ActiveWindow;
            if ((win == null || win.Document == null) && createIfNone)
            {
                Document.Create();
                win = Window.ActiveWindow;
            }
            return (win != null && win.Document != null) ? win.Document.MainPart : null;
        }

        /// <summary>Resolve a body by name (exact match first, then substring, both
        /// case-insensitive), searching recursively through components.</summary>
        private static DesignBody ResolveBodyByName(Part part, string name)
        {
            if (part == null || string.IsNullOrEmpty(name)) return null;
            var all = ConformalMesh.ConformalMeshService.CollectBodies(part, null);
            foreach (var b in all)
                if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) return b;
            foreach (var b in all)
                if (b.Name != null && b.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return b;
            return null;
        }

        /// <summary>Bodies present now but not in the pre-call snapshot (generator payloads).</summary>
        private static List<string> NewBodyNames(Part part, HashSet<DesignBody> before)
        {
            var names = new List<string>();
            foreach (var b in ConformalMesh.ConformalMeshService.CollectBodies(part, null))
                if (!before.Contains(b)) names.Add(b.Name ?? "(unnamed)");
            return names;
        }

        /// <summary>Parse a [{name?, thickness_mm}] array into laminate layer definitions.
        /// Missing names default to Layer_i. Throws ArgumentException (→ error envelope).</summary>
        private static List<Models.Laminate.LaminateLayerDefinition> ParseLayers(
            Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || !(args[key] is List<object> arr) || arr.Count == 0)
                throw new ArgumentException("missing/empty required array field: " + key);
            var layers = new List<Models.Laminate.LaminateLayerDefinition>();
            int idx = 0;
            foreach (var item in arr)
            {
                idx++;
                var o = item as Dictionary<string, object>;
                if (o == null)
                    throw new ArgumentException(key + "[" + idx + "] must be an object {name, thickness_mm}");
                string name = (o.ContainsKey("name") && o["name"] != null)
                    ? o["name"].ToString() : ("Layer_" + idx);
                double t = GetDouble(o, "thickness_mm");
                layers.Add(new Models.Laminate.LaminateLayerDefinition(name, t));
            }
            return layers;
        }

        /// <summary>Apply snake_case numeric/bool overrides onto factory-default specimen params.
        /// Unknown keys throw (clear LLM feedback) rather than being silently ignored.</summary>
        private static void ApplySpecimenOverrides(
            Models.TensileTest.TensileSpecimenParameters p, Dictionary<string, object> ov)
        {
            foreach (var kv in ov)
            {
                switch (kv.Key.Trim().ToLowerInvariant())
                {
                    case "gauge_length_mm": p.GaugeLength = GetDouble(ov, kv.Key); break;
                    case "gauge_width_mm": p.GaugeWidth = GetDouble(ov, kv.Key); break;
                    case "thickness_mm": p.Thickness = GetDouble(ov, kv.Key); break;
                    case "grip_width_mm": p.GripWidth = GetDouble(ov, kv.Key); break;
                    case "total_length_mm": p.TotalLength = GetDouble(ov, kv.Key); break;
                    case "fillet_radius_mm": p.FilletRadius = GetDouble(ov, kv.Key); break;
                    case "grip_length_mm": p.GripLength = GetDouble(ov, kv.Key); break;
                    case "notch_depth_mm": p.NotchDepth = GetDouble(ov, kv.Key); break;
                    case "notch_radius_mm": p.NotchRadius = GetDouble(ov, kv.Key); break;
                    case "notch_angle_deg": p.NotchAngle = GetDouble(ov, kv.Key); break;
                    case "is_double_notch": p.IsDoubleNotch = GetBool(ov, kv.Key); break;
                    case "hole_diameter_mm": p.HoleDiameter = GetDouble(ov, kv.Key); break;
                    case "is_elliptical_hole": p.IsEllipticalHole = GetBool(ov, kv.Key); break;
                    case "hole_major_axis_mm": p.HoleMajorAxis = GetDouble(ov, kv.Key); break;
                    case "hole_minor_axis_mm": p.HoleMinorAxis = GetDouble(ov, kv.Key); break;
                    case "is_rectangular": p.IsRectangular = GetBool(ov, kv.Key); break;
                    case "tab_length_mm": p.TabLength = GetDouble(ov, kv.Key); break;
                    case "tab_thickness_mm": p.TabThickness = GetDouble(ov, kv.Key); break;
                    default:
                        throw new ArgumentException("unknown override key: " + kv.Key +
                            " (expected e.g. gauge_length_mm, gauge_width_mm, thickness_mm, ...)");
                }
            }
        }

        /// <summary>Nearest PLANAR DesignFace to a 3D seed (mm) — the surface_laminate face-
        /// addressing scheme (the proven add_*_on_face seed idiom, restricted to planes).
        /// Projections landing OUTSIDE the face's bbox (+0.5mm margin) are heavily penalized
        /// so a distant coplanar plane can't shadow the face the seed actually sits on.</summary>
        private static DesignFace ResolveNearestPlanarFace(DesignBody body, double[] seedMm)
        {
            if (body == null || seedMm == null || seedMm.Length < 3) return null;
            var seed = Geometry.Point.Create(seedMm[0] / 1000.0, seedMm[1] / 1000.0, seedMm[2] / 1000.0);
            DesignFace best = null;
            double bestScore = double.MaxValue;
            double bestArea = double.MaxValue;
            foreach (var df in body.Faces)
            {
                try
                {
                    var plane = df.Shape.Geometry as Geometry.Plane;
                    if (plane == null) continue;
                    var ev = plane.ProjectPoint(seed);
                    if (ev == null) continue;
                    var pp = ev.Point;
                    double dx = pp.X - seed.X, dy = pp.Y - seed.Y, dz = pp.Z - seed.Z;
                    double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    var bb = df.Shape.GetBoundingBox(Geometry.Matrix.Identity);
                    const double m = 0.0005; // 0.5mm bbox margin
                    bool inside = pp.X >= bb.MinCorner.X - m && pp.X <= bb.MaxCorner.X + m
                        && pp.Y >= bb.MinCorner.Y - m && pp.Y <= bb.MaxCorner.Y + m
                        && pp.Z >= bb.MinCorner.Z - m && pp.Z <= bb.MaxCorner.Z + m;
                    double score = d + (inside ? 0.0 : 1.0); // 1m penalty off-face
                    double area;
                    try { area = df.Shape.Area; } catch { area = double.MaxValue; }
                    // near-equal scores = coplanar faces whose bboxes both cover the seed
                    // (pocket+island, L-regions): prefer the SMALLER face — the one the seed
                    // actually sits on — instead of first-enumerated-wins.
                    bool better = score < bestScore - 1e-9
                        || (Math.Abs(score - bestScore) <= 1e-9 && area < bestArea);
                    if (better) { bestScore = score; bestArea = area; best = df; }
                }
                catch { }
            }
            return best;
        }

        private static string JsonStringArray(IEnumerable<string> items)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            if (items != null)
                foreach (var s in items)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"").Append(EscapeStr(s ?? "")).Append("\"");
                }
            sb.Append("]");
            return sb.ToString();
        }

        private static string FeatureCountsJson(FeatureGraph g)
        {
            if (g == null) return "{}";
            int holes = g.Holes != null ? g.Holes.Count : 0;
            int bosses = g.Bosses != null ? g.Bosses.Count : 0;
            int walls = g.Walls != null ? g.Walls.Count : 0;
            int fillets = g.FilletChains != null ? g.FilletChains.Count : 0;
            int slits = g.Slits != null ? g.Slits.Count : 0;
            return "{\"holes\": " + holes.ToString(Inv) + ", \"bosses\": " + bosses.ToString(Inv) +
                ", \"walls\": " + walls.ToString(Inv) + ", \"fillet_chains\": " + fillets.ToString(Inv) +
                ", \"slits\": " + slits.ToString(Inv) + "}";
        }

        // ----------------------------------------------------------------
        // Argument extraction
        // ----------------------------------------------------------------
        /// <summary>Array-of-arrays numeric parse: [[a,b(,c)], ...] with loud failures.</summary>
        private static List<double[]> ParsePointList(Dictionary<string, object> args,
            string key, int minCount, int dims)
        {
            if (!args.ContainsKey(key) || !(args[key] is List<object> outer))
                throw new ArgumentException(key + " must be an array of " + dims + "-number arrays");
            if (outer.Count < minCount)
                throw new ArgumentException(key + " needs >= " + minCount + " points");
            var result = new List<double[]>();
            for (int i = 0; i < outer.Count; i++)
            {
                if (!(outer[i] is List<object> inner) || inner.Count < dims)
                    throw new ArgumentException(key + "[" + i + "] must be a " + dims + "-number array");
                var p = new double[dims];
                for (int k = 0; k < dims; k++)
                {
                    if (inner[k] is double dv) p[k] = dv;
                    else if (inner[k] is long lv) p[k] = lv;
                    else if (inner[k] is int iv) p[k] = iv;
                    else if (!(inner[k] is string sv) || !double.TryParse(
                        sv, NumberStyles.Float, Inv, out p[k]))
                        throw new ArgumentException(key + "[" + i + "][" + k + "] is not a number");
                }
                result.Add(p);
            }
            return result;
        }

        private static Models.Fastener.HeadStyle? ParseHeadStyle(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "hex": return Models.Fastener.HeadStyle.Hex;
                case "socket_cap": case "socket": return Models.Fastener.HeadStyle.SocketCap;
                case "pan": return Models.Fastener.HeadStyle.Pan;
                case "countersunk": case "csk": return Models.Fastener.HeadStyle.Countersunk;
                case "dome": case "button": return Models.Fastener.HeadStyle.Dome;
                case "flat": return Models.Fastener.HeadStyle.Flat;
                default: return null;
            }
        }

        /// <summary>One fastening site + parametric design recommendations — the data the
        /// LLM uses to GUIDE the fastening-design conversation (bolt vs rivet, head trade-
        /// offs, auto ISO dims). Rationale strings are the machine-readable design notes.</summary>
        private static string FastenerSiteJson(Models.Fastener.FastenerSite s, double[] seedMm)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"hole_d_mm\": ").Append(s.HoleDiaMm.ToString("0.###", Inv))
              .Append(", \"grip_mm\": ").Append(s.GripMm.ToString("0.###", Inv))
              .Append(", \"faces\": ").Append(s.FaceCount.ToString(Inv))
              .Append(", \"bodies\": ").Append(JsonStringArray(s.BodyNames))
              .Append(", \"axis_point_mm\": [").Append(s.AxisPointMm[0].ToString("0.###", Inv))
              .Append(", ").Append(s.AxisPointMm[1].ToString("0.###", Inv))
              .Append(", ").Append(s.AxisPointMm[2].ToString("0.###", Inv))
              .Append("], \"axis_dir\": [").Append(s.AxisDir[0].ToString("0.####", Inv))
              .Append(", ").Append(s.AxisDir[1].ToString("0.####", Inv))
              .Append(", ").Append(s.AxisDir[2].ToString("0.####", Inv)).Append("]");
            if (seedMm != null)
                sb.Append(", \"seed_distance_mm\": ").Append(
                    Fastener.FastenerGenerationService.SeedDistance(s, seedMm).ToString("0.###", Inv));

            sb.Append(", \"recommendations\": [");
            double n = Models.Fastener.IsoMetricThread.AutoNominalForHole(s.HoleDiaMm);
            bool first = true;
            if (n > 0)
            {
                double pitch = Models.Fastener.IsoMetricThread.CoarsePitchFor(n);
                double autoLen = s.GripMm + 0.8 * n + 2 * pitch;
                sb.Append("{\"type\": \"bolt\", \"designation\": \"M").Append(n.ToString("0.#", Inv))
                  .Append("\", \"nominal_d_mm\": ").Append(n.ToString("0.###", Inv))
                  .Append(", \"pitch_mm\": ").Append(pitch.ToString("0.###", Inv))
                  .Append(", \"auto_length_mm\": ").Append(autoLen.ToString("0.###", Inv))
                  .Append(", \"heads\": [\"hex\", \"socket_cap\", \"pan\", \"countersunk\"]")
                  .Append(", \"with_nut\": true")
                  .Append(", \"rationale\": \"serviceable/reusable joint; nut needs access to both sides; ")
                  .Append("hex = wrench access, socket_cap = compact radial space, countersunk = flush surface\"}");
                first = false;
            }
            double shank = s.HoleDiaMm * 0.98;
            if (!first) sb.Append(", ");
            sb.Append("{\"type\": \"rivet\", \"shank_d_mm\": ").Append(shank.ToString("0.###", Inv))
              .Append(", \"tail_dia_mm\": ").Append((1.6 * shank).ToString("0.###", Inv))
              .Append(", \"tail_h_mm\": ").Append((0.6 * shank).ToString("0.###", Inv))
              .Append(", \"heads\": [\"dome\", \"flat\", \"countersunk\"]")
              .Append(", \"rationale\": \"permanent joint, single-side install, fills the hole ")
              .Append("(better shear/fatigue); countersunk head when the surface must stay flush\"}");
            sb.Append("]");
            if (n <= 0)
                sb.Append(", \"note\": \"no standard metric nominal fits this hole with a ")
                  .Append("medium clearance fit - bolt requires explicit nominal_d_mm\"");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>Layer-stack summary for parse_package_file (no geometry).</summary>
        private static string PackageSummaryJson(Models.Package.PackageSpec spec, string path)
        {
            var zb = spec.ComputeZBasesMm();
            int totalBalls = 0, totalBoxes = 0;
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"file\": \"").Append(EscapeStr(path ?? ""))
              .Append("\", \"layers\": [");
            for (int i = 0; i < spec.Layers.Count; i++)
            {
                var l = spec.Layers[i];
                totalBalls += l.Balls.Count;
                totalBoxes += l.Boxes.Count;
                if (i > 0) sb.Append(", ");
                sb.Append("{\"name\": \"").Append(EscapeStr(l.Name ?? ""))
                  .Append("\", \"z_base_mm\": ").Append(zb[i].ToString("0.###", Inv))
                  .Append(", \"thickness_mm\": ").Append(l.ThicknessMm.ToString("0.###", Inv))
                  .Append(", \"size_mm\": [").Append(l.LenXMm.ToString("0.###", Inv))
                  .Append(", ").Append(l.LenYMm.ToString("0.###", Inv))
                  .Append("], \"location_mm\": [").Append(l.LocXMm.ToString("0.###", Inv))
                  .Append(", ").Append(l.LocYMm.ToString("0.###", Inv))
                  .Append("], \"balls\": ").Append(l.Balls.Count.ToString(Inv))
                  .Append(", \"boxes\": ").Append(l.Boxes.Count.ToString(Inv))
                  .Append(", \"mesh_keys\": ").Append(JsonStringArray(l.MeshOptions.Keys))
                  .Append("}");
            }
            sb.Append("], \"total_thickness_mm\": ")
              .Append(spec.GetTotalThicknessMm().ToString("0.###", Inv))
              .Append(", \"total_balls\": ").Append(totalBalls.ToString(Inv))
              .Append(", \"total_boxes\": ").Append(totalBoxes.ToString(Inv))
              .Append(", \"warnings\": ").Append(JsonStringArray(spec.Warnings)).Append("}");
            return sb.ToString();
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required string field: " + key);
            return args[key].ToString();
        }

        private static double GetDouble(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required number field: " + key);
            object v = args[key];
            if (v is double d) return d;
            if (v is long l) return (double)l;
            double parsed;
            if (double.TryParse(v.ToString(), NumberStyles.Float, Inv, out parsed)) return parsed;
            throw new ArgumentException("field " + key + " is not a number: " + v);
        }

        private static double GetDoubleOrDefault(Dictionary<string, object> args, string key, double dflt)
        {
            if (!args.ContainsKey(key) || args[key] == null) return dflt;
            return GetDouble(args, key);
        }

        private static bool GetBool(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required bool field: " + key);
            object v = args[key];
            if (v is bool b) return b;
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true") return true;
            if (s == "false") return false;
            throw new ArgumentException("field " + key + " is not a bool: " + v);
        }

        private static double[] GetDoubleArray(Dictionary<string, object> args, string key, int expectedLen)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                throw new ArgumentException("missing required array field: " + key);
            object v = args[key];
            if (!(v is List<object> list))
                throw new ArgumentException("field " + key + " is not an array");
            if (list.Count != expectedLen)
                throw new ArgumentException("field " + key + " expected length " + expectedLen + ", got " + list.Count);
            var arr = new double[expectedLen];
            for (int i = 0; i < expectedLen; i++)
            {
                object e = list[i];
                if (e == null) throw new ArgumentException("field " + key + "[" + i + "] is null");
                if (e is double d) { arr[i] = d; continue; }
                if (e is long l) { arr[i] = (double)l; continue; }
                double parsed;
                if (double.TryParse(e.ToString(), NumberStyles.Float, Inv, out parsed)) { arr[i] = parsed; continue; }
                throw new ArgumentException("field " + key + "[" + i + "] is not a number: " + e);
            }
            return arr;
        }

        // ----------------------------------------------------------------
        // Tiny JSON parser (object / array / string / number / bool / null).
        // Sufficient for LLM tool inputs; not a full RFC 8259 implementation.
        // ----------------------------------------------------------------
        private static Dictionary<string, object> ParseObject(string json)
        {
            int idx = 0;
            SkipWs(json, ref idx);
            if (idx >= json.Length || json[idx] != '{')
                throw new FormatException("expected '{' at position " + idx);
            object o = ParseValue(json, ref idx);
            if (!(o is Dictionary<string, object> dict))
                throw new FormatException("top-level value is not an object");
            return dict;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected EOF");
            char c = s[i];
            if (c == '{') return ParseObjectInner(s, ref i);
            if (c == '[') return ParseArrayInner(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') { ParseNull(s, ref i); return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
            throw new FormatException("unexpected char '" + c + "' at " + i);
        }

        private static Dictionary<string, object> ParseObjectInner(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            if (s[i] != '{') throw new FormatException("expected '{'");
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + i);
                i++;
                object val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected EOF in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                throw new FormatException("expected ',' or '}' at " + i);
            }
        }

        private static List<object> ParseArrayInner(string s, ref int i)
        {
            var list = new List<object>();
            if (s[i] != '[') throw new FormatException("expected '['");
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                object v = ParseValue(s, ref i);
                list.Add(v);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected EOF in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("expected ',' or ']' at " + i);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected '\"' at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return sb.ToString(); }
                if (c == '\\')
                {
                    if (i + 1 >= s.Length) throw new FormatException("bad escape at EOF");
                    char esc = s[i + 1];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 5 >= s.Length) throw new FormatException("bad \\u escape");
                            string hex = s.Substring(i + 2, 4);
                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, Inv, out code))
                                throw new FormatException("bad \\u escape: " + hex);
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new FormatException("unknown escape \\" + esc);
                    }
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            throw new FormatException("unterminated string");
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-'))
                i++;
            string token = s.Substring(start, i - start);
            // Prefer long for integer-looking values; fall back to double.
            if (token.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
            {
                long l;
                if (long.TryParse(token, NumberStyles.Integer, Inv, out l)) return l;
            }
            double d;
            if (double.TryParse(token, NumberStyles.Float, Inv, out d)) return d;
            throw new FormatException("bad number: " + token);
        }

        private static bool ParseBool(string s, ref int i)
        {
            if (i + 4 <= s.Length && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (i + 5 <= s.Length && s.Substring(i, 5) == "false") { i += 5; return false; }
            throw new FormatException("bad bool at " + i);
        }

        private static void ParseNull(string s, ref int i)
        {
            if (i + 4 <= s.Length && s.Substring(i, 4) == "null") { i += 4; return; }
            throw new FormatException("bad null at " + i);
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static string EscapeStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        /// <summary>Serialize a value produced by ParseValue (Dictionary/List/string/double/bool/null)
        /// back to a JSON string — used by apply_operations to re-emit each step's args for the
        /// recursive Dispatch call.</summary>
        private static string SerializeValue(object v)
        {
            if (v == null) return "null";
            if (v is bool b) return b ? "true" : "false";
            if (v is string s) return "\"" + EscapeStr(s) + "\"";
            if (v is double d) return d.ToString("R", Inv);
            if (v is long l) return l.ToString(Inv);
            if (v is int i) return i.ToString(Inv);
            if (v is List<object> list)
            {
                var sb = new StringBuilder("[");
                for (int k = 0; k < list.Count; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append(SerializeValue(list[k]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (v is Dictionary<string, object> obj)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in obj)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"").Append(EscapeStr(kv.Key)).Append("\": ").Append(SerializeValue(kv.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }
            // fallback: stringify
            return "\"" + EscapeStr(v.ToString()) + "\"";
        }
    }
}
