using UnityEngine;
using UnityEditor;

namespace BuildingSystem.Editor
{
    public static class PrimitiveFactory
    {
        private const string GENERATED_FOLDER = "Assets/BuildingTool/Generated";

        public static void GenerateDefaultPalette(BuildingTool tool)
        {
            if (!AssetDatabase.IsValidFolder("Assets/BuildingTool"))
                AssetDatabase.CreateFolder("Assets", "BuildingTool");
            if (!AssetDatabase.IsValidFolder(GENERATED_FOLDER))
                AssetDatabase.CreateFolder("Assets/BuildingTool", "Generated");

            if (tool.buildingPalette == null)
            {
                tool.buildingPalette = ScriptableObject.CreateInstance<BuildingPalette>();
                AssetDatabase.CreateAsset(tool.buildingPalette, "Assets/BuildingTool/DefaultPalette.asset");
            }

            tool.buildingPalette.availableObjects.Clear();

            // Floor – thin flat plane, centred in cell, sits at Y=0
            CreateAndAdd(tool.buildingPalette, "Floor",
                PrimitiveType.Cube, Vector3Int.one, PlacementAlignment.Center,
                new Vector3(1f, 0.05f, 1f), Vector3.zero);

            // Wall – tall thin slab, edge-aligned (snaps to cell edge)
            CreateAndAdd(tool.buildingPalette, "Wall",
                PrimitiveType.Cube, Vector3Int.one, PlacementAlignment.Edge,
                new Vector3(1f, 1f, 0.05f), new Vector3(0, 0.5f, 0));

            // Corner – narrow vertical post, corner-aligned
            CreateAndAdd(tool.buildingPalette, "Corner",
                PrimitiveType.Cube, Vector3Int.one, PlacementAlignment.Corner,
                new Vector3(0.1f, 1f, 0.1f), new Vector3(0, 0.5f, 0));

            // Stairs – ramp, centred (can be further refined)
            CreateAndAdd(tool.buildingPalette, "Stairs",
                PrimitiveType.Cube, Vector3Int.one, PlacementAlignment.Center,
                new Vector3(1f, 1f, 1f), new Vector3(0, 0.5f, 0), rotateX: 45f);

            EditorUtility.SetDirty(tool.buildingPalette);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildingTool] Default primitives generated in " + GENERATED_FOLDER);
        }

        private static void CreateAndAdd(BuildingPalette palette, string name,
            PrimitiveType type, Vector3Int size, PlacementAlignment alignment,
            Vector3 scale, Vector3 pivotOffset, float rotateX = 0f)
        {
            // Container – pivot at grid anchor
            GameObject root = new GameObject(name);

            // Visible mesh child – offset so pivot is at root
            GameObject mesh = GameObject.CreatePrimitive(type);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale    = scale;
            mesh.transform.localPosition = pivotOffset;
            if (rotateX != 0f)
                mesh.transform.localRotation = Quaternion.Euler(rotateX, 0, 0);

            // PlaceableObject on root
            PlaceableObject po = root.AddComponent<PlaceableObject>();
            po.objectType = name;
            po.size       = size;
            po.alignment  = alignment;

            // Save as prefab
            string path    = $"{GENERATED_FOLDER}/{name}.prefab";
            GameObject pfb = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            PlaceableObject pfbPO = pfb.GetComponent<PlaceableObject>();
            if (pfbPO != null && !palette.availableObjects.Contains(pfbPO))
                palette.availableObjects.Add(pfbPO);
        }
    }
}
