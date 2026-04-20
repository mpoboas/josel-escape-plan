using System.Collections.Generic;
using System.IO;
using BuildingSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Combines every <see cref="MeshRenderer"/> that lives under a <see cref="BuildingSystem.PlaceableObject"/>
/// (i.e. every grid-placed tile) under each <c>Floor N</c> root into one MeshRenderer per material,
/// generates a fresh UV2 for the combined mesh and saves it as a mesh asset. The lightmap baker
/// then produces a single continuous chart per floor per material instead of a separate chart per
/// tile, which removes the per-tile seams visible in the chart visualiser.
/// Non-tile scene props (desks, PCs, etc.) are not touched.
/// </summary>
public static class BuildingCombinedGiMenus
{
    private const string MenuRoot = "Tools/Building/";
    private const string CombinedChildPrefix = "_CombinedGI_";

    private const StaticEditorFlags kCombinedStaticFlags =
        StaticEditorFlags.ContributeGI |
        StaticEditorFlags.BatchingStatic |
        StaticEditorFlags.OccluderStatic |
        StaticEditorFlags.OccludeeStatic |
        StaticEditorFlags.ReflectionProbeStatic;

    [MenuItem(MenuRoot + "Combine floor meshes for baking")]
    public static void CombineFloors()
    {
        BuildingTool[] tools = Object.FindObjectsByType<BuildingTool>(FindObjectsSortMode.None);
        if (tools.Length == 0)
        {
            Debug.LogWarning("[BuildingCombinedGI] No BuildingTool in loaded scenes.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Combine floor meshes for baking");
        int undoGroup = Undo.GetCurrentGroup();

        int totalFloors = 0;
        int totalCombined = 0;
        int totalSources = 0;

        foreach (BuildingTool tool in tools)
        {
            Transform root = tool.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform ch = root.GetChild(i);
                if (!BuildingFloorNaming.TryParseFloorLevelFromName(ch.name, out int level))
                    continue;

                CombineFloor(ch, level, out int combined, out int sources);
                if (combined > 0)
                {
                    totalFloors++;
                    totalCombined += combined;
                    totalSources += sources;
                }
            }
        }

        AssetDatabase.SaveAssets();
        MarkOpenScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[BuildingCombinedGI] Combined {totalFloors} Floor N root(s) → {totalCombined} new MeshRenderer(s), " +
            $"replacing {totalSources} source MeshRenderer(s). Rebake the scene now.");
    }

    [MenuItem(MenuRoot + "Revert combined floor meshes")]
    public static void RevertCombinedFloors()
    {
        BuildingTool[] tools = Object.FindObjectsByType<BuildingTool>(FindObjectsSortMode.None);
        if (tools.Length == 0)
        {
            Debug.LogWarning("[BuildingCombinedGI] No BuildingTool in loaded scenes.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Revert combined floor meshes");
        int undoGroup = Undo.GetCurrentGroup();

        int reverted = 0;
        foreach (BuildingTool tool in tools)
        {
            Transform root = tool.transform;
            BuildingCombinedGiMarker[] markers =
                root.GetComponentsInChildren<BuildingCombinedGiMarker>(true);

            foreach (BuildingCombinedGiMarker marker in markers)
            {
                RestoreSources(marker);
                Undo.DestroyObjectImmediate(marker.gameObject);
                reverted++;
            }
        }

        MarkOpenScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[BuildingCombinedGI] Removed {reverted} combined child(ren) and restored sources.");
    }

    private static void CombineFloor(Transform floorRoot, int floorLevel, out int combinedCount, out int sourceCount)
    {
        combinedCount = 0;
        sourceCount = 0;

        // 1) Clean up any previous combined children under this floor (and re-enable their sources)
        for (int i = floorRoot.childCount - 1; i >= 0; i--)
        {
            Transform ch = floorRoot.GetChild(i);
            BuildingCombinedGiMarker existing = ch.GetComponent<BuildingCombinedGiMarker>();
            if (existing == null)
                continue;

            RestoreSources(existing);
            Undo.DestroyObjectImmediate(ch.gameObject);
        }

        // 2) Collect candidates: descendants that live under a PlaceableObject (i.e. they
        //    are building tiles placed by the BuildingTool), with MeshFilter + readable mesh.
        //    We intentionally don't filter by ContributeGI because tile prefabs often don't
        //    set that flag on the mesh-bearing child; they get baked via inheritance. The
        //    PlaceableObject ancestor check is what reliably identifies "a grid-placed tile".
        var byMaterial = new Dictionary<Material, List<CombineInstance>>();
        var sourceRenderers = new List<MeshRenderer>();

        MeshRenderer[] renderers = floorRoot.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in renderers)
        {
            GameObject go = mr.gameObject;

            if (go.GetComponent<BuildingCombinedGiMarker>() != null) continue;
            if (!HasPlaceableAncestor(go.transform, floorRoot)) continue;
            if (ShouldSkip(go)) continue;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
                Debug.LogWarning(
                    $"[BuildingCombinedGI] Mesh '{mesh.name}' on '{GetHierarchyPath(go.transform)}' is not readable — " +
                    "enable Read/Write in its FBX importer. Skipping this tile.", go);
                continue;
            }

            Matrix4x4 toFloorLocal = floorRoot.worldToLocalMatrix * mr.transform.localToWorldMatrix;
            Material[] mats = mr.sharedMaterials;
            int subCount = mesh.subMeshCount;

            bool added = false;
            for (int sm = 0; sm < subCount; sm++)
            {
                Material mat = (sm < mats.Length) ? mats[sm] : null;
                if (mat == null)
                    continue;

                if (!byMaterial.TryGetValue(mat, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    byMaterial[mat] = list;
                }
                list.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = sm,
                    transform = toFloorLocal
                });
                added = true;
            }

            if (added)
                sourceRenderers.Add(mr);
        }

        if (byMaterial.Count == 0)
            return;

        // 3) Build one combined mesh per material, unwrap UV2, save as asset, spawn MeshRenderer
        Scene activeScene = floorRoot.gameObject.scene.IsValid()
            ? floorRoot.gameObject.scene
            : SceneManager.GetActiveScene();

        string bakedFolder = EnsureBakedFolder(activeScene);
        string sceneSafeName = string.IsNullOrEmpty(activeScene.name) ? "Untitled" : SafeName(activeScene.name);

        GameObject firstCombinedGo = null;
        int matIndex = 0;
        foreach (var kv in byMaterial)
        {
            Material mat = kv.Key;
            CombineInstance[] instances = kv.Value.ToArray();

            string matSafe = SafeName(mat.name);
            string meshName = $"{sceneSafeName}_Floor{floorLevel}_{matSafe}_{matIndex}";

            Mesh combined = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };
            combined.CombineMeshes(instances, mergeSubMeshes: true, useMatrices: true);
            combined.RecalculateBounds();
            combined.RecalculateTangents();
            combined.Optimize();

            // CombineMeshes concatenates geometry without welding. Adjacent tiles share
            // positions at their shared edges, but their vertices stay topologically
            // disjoint, so GenerateSecondaryUVSet would still place a chart boundary at
            // every tile edge. We build a welded topology-only copy, unwrap that, and
            // transfer UV2 back by per-vertex remap. Original UV1 (tile texture UVs) is
            // untouched, so tiled materials keep rendering correctly.
            try
            {
                if (!GenerateUV2ViaWeldedTopology(combined, out string unwrapError))
                    Debug.LogWarning($"[BuildingCombinedGI] UV2 unwrap failed for '{meshName}': {unwrapError}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildingCombinedGI] UV2 unwrap threw for '{meshName}': {e.Message}");
            }

            string meshPath = $"{bakedFolder}/{meshName}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(combined, meshPath);

            GameObject go = new GameObject(CombinedChildPrefix + matSafe);
            Undo.RegisterCreatedObjectUndo(go, "Create combined GI mesh");

            go.transform.SetParent(floorRoot, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshFilter newMf = go.AddComponent<MeshFilter>();
            newMf.sharedMesh = combined;

            MeshRenderer newMr = go.AddComponent<MeshRenderer>();
            newMr.sharedMaterial = mat;
            newMr.stitchLightmapSeams = true;
            newMr.scaleInLightmap = 1f;
            newMr.receiveGI = ReceiveGI.Lightmaps;

            GameObjectUtility.SetStaticEditorFlags(go, kCombinedStaticFlags);

            BuildingCombinedGiMarker marker = go.AddComponent<BuildingCombinedGiMarker>();
            marker.floorLevel = floorLevel;
            marker.materialName = mat.name;

            if (firstCombinedGo == null)
                firstCombinedGo = go;

            combinedCount++;
            matIndex++;
        }

        // 4) Disable source MeshRenderers and clear their ContributeGI flag so they do not
        //    double-render or double-bake. Store their GameObjects on the first marker so
        //    revert can undo exactly this set.
        BuildingCombinedGiMarker firstMarker =
            firstCombinedGo != null ? firstCombinedGo.GetComponent<BuildingCombinedGiMarker>() : null;

        foreach (MeshRenderer mr in sourceRenderers)
        {
            Undo.RecordObject(mr, "Disable source renderer for GI combine");
            mr.enabled = false;

            GameObject go = mr.gameObject;
            Undo.RecordObject(go, "Clear ContributeGI for GI combine");
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags &= ~StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(go, flags);

            if (firstMarker != null)
                firstMarker.sourceGameObjects.Add(go);

            sourceCount++;
        }

        if (firstMarker != null)
            EditorUtility.SetDirty(firstMarker);
    }

    private static void RestoreSources(BuildingCombinedGiMarker marker)
    {
        if (marker == null || marker.sourceGameObjects == null)
            return;

        foreach (GameObject go in marker.sourceGameObjects)
        {
            if (go == null)
                continue;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.RecordObject(mr, "Re-enable source renderer");
                mr.enabled = true;
            }

            Undo.RecordObject(go, "Restore ContributeGI");
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
        }
    }

    /// <summary>
    /// Generates UV2 for <paramref name="mesh"/> by running Unwrapping on a topology-only copy
    /// where coincident vertices (same position &amp; matching normal) have been welded. The
    /// resulting UV2 is then transferred back onto the original mesh by walking triangles in
    /// lockstep, so adjacent tile edges end up sharing a lightmap texel (no per-tile chart)
    /// while the mesh's original UV1 (tile texture UVs), normals and tangents are preserved.
    /// </summary>
    /// <remarks>
    /// <see cref="Unwrapping.GenerateSecondaryUVSet"/> is allowed to split vertices at chart
    /// boundaries, so the post-unwrap vertex count can be larger than the welded input. We
    /// therefore rebuild the mesh from scratch, duplicating original vertex attributes only
    /// when the unwrapper assigns different UV2 islands to them. Vertex count can grow or
    /// shrink relative to the input.
    /// </remarks>
    private static bool GenerateUV2ViaWeldedTopology(Mesh mesh, out string error)
    {
        error = null;
        const float positionEps = 0.0005f;       // 0.5mm
        const float normalDotThreshold = 0.98f;  // ~11°

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Vector2[] uv1 = mesh.uv;
        bool hasNormals = normals != null && normals.Length == verts.Length;
        bool hasTangents = tangents != null && tangents.Length == verts.Length;
        bool hasUv1 = uv1 != null && uv1.Length == verts.Length;

        int vertexCount = verts.Length;
        int[] remap = new int[vertexCount];
        List<Vector3> weldedVerts = new List<Vector3>(vertexCount);
        List<Vector3> weldedNormals = hasNormals ? new List<Vector3>(vertexCount) : null;

        float invEps = 1f / positionEps;
        Dictionary<long, List<int>> spatial = new Dictionary<long, List<int>>();

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 p = verts[i];
            Vector3 nrm = hasNormals ? normals[i] : Vector3.up;

            int cx = Mathf.FloorToInt(p.x * invEps);
            int cy = Mathf.FloorToInt(p.y * invEps);
            int cz = Mathf.FloorToInt(p.z * invEps);

            int found = -1;

            for (int dx = -1; dx <= 1 && found < 0; dx++)
            {
                for (int dy = -1; dy <= 1 && found < 0; dy++)
                {
                    for (int dz = -1; dz <= 1 && found < 0; dz++)
                    {
                        long key = HashCell(cx + dx, cy + dy, cz + dz);
                        if (!spatial.TryGetValue(key, out List<int> bucket)) continue;

                        for (int k = 0; k < bucket.Count; k++)
                        {
                            int j = bucket[k];
                            if ((weldedVerts[j] - p).sqrMagnitude > positionEps * positionEps) continue;
                            if (hasNormals && Vector3.Dot(weldedNormals[j], nrm) < normalDotThreshold) continue;
                            found = j;
                            break;
                        }
                    }
                }
            }

            if (found >= 0)
            {
                remap[i] = found;
            }
            else
            {
                int newIdx = weldedVerts.Count;
                remap[i] = newIdx;
                weldedVerts.Add(p);
                if (hasNormals) weldedNormals.Add(nrm);

                long homeKey = HashCell(cx, cy, cz);
                if (!spatial.TryGetValue(homeKey, out List<int> home))
                {
                    home = new List<int>(4);
                    spatial[homeKey] = home;
                }
                home.Add(newIdx);
            }
        }

        int[] origTris = mesh.triangles;
        List<int> weldedTris = new List<int>(origTris.Length);
        List<int> survivingTriStarts = new List<int>(origTris.Length / 3);
        for (int i = 0; i < origTris.Length; i += 3)
        {
            int a = remap[origTris[i]];
            int b = remap[origTris[i + 1]];
            int c = remap[origTris[i + 2]];
            if (a == b || b == c || a == c) continue; // drop degenerate triangles produced by welding
            weldedTris.Add(a);
            weldedTris.Add(b);
            weldedTris.Add(c);
            survivingTriStarts.Add(i);
        }

        if (weldedTris.Count == 0)
        {
            error = "all triangles collapsed after welding";
            return false;
        }

        Mesh topology = new Mesh
        {
            name = mesh.name + "_WeldedTopology",
            indexFormat = IndexFormat.UInt32
        };
        topology.SetVertices(weldedVerts);
        if (hasNormals) topology.SetNormals(weldedNormals);
        topology.SetTriangles(weldedTris, 0);
        topology.RecalculateBounds();

        bool ok;
        try
        {
            ok = Unwrapping.GenerateSecondaryUVSet(topology);
        }
        catch (System.Exception e)
        {
            Object.DestroyImmediate(topology);
            error = e.Message;
            return false;
        }

        if (!ok)
        {
            Object.DestroyImmediate(topology);
            error = "GenerateSecondaryUVSet returned false";
            return false;
        }

        int[] postTris = topology.triangles;
        Vector2[] postUv2 = topology.uv2;
        int expectedCorners = survivingTriStarts.Count * 3;

        if (postTris == null || postTris.Length != expectedCorners || postUv2 == null)
        {
            Object.DestroyImmediate(topology);
            error = $"unwrapped mesh returned unexpected layout " +
                    $"(tris={(postTris?.Length ?? -1)}, expected corners={expectedCorners}, " +
                    $"uv2 present={(postUv2 != null)})";
            return false;
        }

        // Rebuild the mesh. For each surviving triangle corner we need one final vertex whose
        // original attributes come from the original vertex and whose UV2 comes from the
        // post-unwrap vertex. We dedupe by (origVertIdx, postUnwrapVertIdx) so a single
        // original vertex is only duplicated when the unwrapper genuinely assigned it to
        // multiple UV2 islands.
        Dictionary<long, int> pairToFinal = new Dictionary<long, int>(vertexCount);
        List<Vector3> finalPos = new List<Vector3>(vertexCount);
        List<Vector3> finalNrm = hasNormals ? new List<Vector3>(vertexCount) : null;
        List<Vector4> finalTan = hasTangents ? new List<Vector4>(vertexCount) : null;
        List<Vector2> finalUv1 = hasUv1 ? new List<Vector2>(vertexCount) : null;
        List<Vector2> finalUv2 = new List<Vector2>(vertexCount);
        List<int> finalTris = new List<int>(expectedCorners);

        for (int t = 0; t < survivingTriStarts.Count; t++)
        {
            int origStart = survivingTriStarts[t];
            for (int c = 0; c < 3; c++)
            {
                int origIdx = origTris[origStart + c];
                int postIdx = postTris[t * 3 + c];
                long key = ((long)origIdx << 32) | (uint)postIdx;
                if (!pairToFinal.TryGetValue(key, out int finalIdx))
                {
                    finalIdx = finalPos.Count;
                    pairToFinal[key] = finalIdx;
                    finalPos.Add(verts[origIdx]);
                    if (hasNormals) finalNrm.Add(normals[origIdx]);
                    if (hasTangents) finalTan.Add(tangents[origIdx]);
                    if (hasUv1) finalUv1.Add(uv1[origIdx]);
                    finalUv2.Add(postUv2[postIdx]);
                }
                finalTris.Add(finalIdx);
            }
        }

        Object.DestroyImmediate(topology);

        mesh.Clear();
        mesh.indexFormat = finalPos.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(finalPos);
        if (hasNormals) mesh.SetNormals(finalNrm);
        if (hasTangents) mesh.SetTangents(finalTan);
        if (hasUv1) mesh.SetUVs(0, finalUv1);
        mesh.SetUVs(1, finalUv2);
        mesh.SetTriangles(finalTris, 0);
        mesh.RecalculateBounds();
        return true;
    }

    private static long HashCell(int x, int y, int z)
    {
        // Interleave three 21-bit cell coords into a single 63-bit signed long.
        const long mask = (1L << 21) - 1L;
        return ((x & mask) << 42) | ((y & mask) << 21) | (z & mask);
    }

    private static bool HasPlaceableAncestor(Transform t, Transform boundary)
    {
        for (Transform cur = t; cur != null && cur != boundary; cur = cur.parent)
        {
            if (cur.GetComponent<BuildingSystem.PlaceableObject>() != null)
                return true;
        }
        return false;
    }

    private static bool ShouldSkip(GameObject go)
    {
        if (go.CompareTag("Player"))
            return true;
        if (go.GetComponent<ParticleSystem>() != null)
            return true;
        if (go.GetComponent<HingeJoint>() != null)
            return true;
        if (go.GetComponent<Animator>() != null)
            return true;
        // Skip tiles used as doors — they move at runtime.
        if (go.GetComponentInParent<DoorController>() != null)
            return true;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            return true;

        return false;
    }

    private static string EnsureBakedFolder(Scene scene)
    {
        string scenePath = scene.path;
        string sceneDir = string.IsNullOrEmpty(scenePath)
            ? "Assets"
            : Path.GetDirectoryName(scenePath).Replace('\\', '/');

        string sceneFolderName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
        string sceneFolder = $"{sceneDir}/{sceneFolderName}";
        if (!AssetDatabase.IsValidFolder(sceneFolder))
            AssetDatabase.CreateFolder(sceneDir, sceneFolderName);

        string combinedFolder = $"{sceneFolder}/CombinedGI";
        if (!AssetDatabase.IsValidFolder(combinedFolder))
            AssetDatabase.CreateFolder(sceneFolder, "CombinedGI");

        return combinedFolder;
    }

    private static string SafeName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "Mat";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t.parent == null)
            return t.name;
        return GetHierarchyPath(t.parent) + "/" + t.name;
    }

    private static void MarkOpenScenesDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded)
                EditorSceneManager.MarkSceneDirty(s);
        }
    }
}
