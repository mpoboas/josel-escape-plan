using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fills or updates <see cref="SignageCatalog"/> with one row per PNG texture in the signage art folder.
/// </summary>
public static class SignageCatalogMenu
{
    private const string SignageArtFolder = "Assets/Assets/Signage";
    private const string CatalogAssetPath = "Assets/Assets/Signage/SignageCatalog.asset";

    [MenuItem("Assets/Create/Signage/Regenerate catalog from Assets/Assets/Signage")]
    public static void RegenerateCatalogFromSignageFolder()
    {
        if (!AssetDatabase.IsValidFolder(SignageArtFolder))
        {
            Debug.LogError("[SignageCatalog] Folder not found: " + SignageArtFolder);
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<SignageCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SignageCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.entries ??= new List<SignageCatalog.Entry>();
        catalog.entries.Clear();

        EnsurePngsImportedAsSprites(SignageArtFolder);

        var rows = new List<(string path, string name, Sprite sprite)>();
        CollectSpritesInto(rows, SignageArtFolder, CatalogAssetPath, "t:Sprite");
        if (rows.Count == 0)
            CollectSpritesInto(rows, SignageArtFolder, CatalogAssetPath, "t:Texture2D");

        rows.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        foreach (var (_, displayName, sprite) in rows)
            catalog.entries.Add(new SignageCatalog.Entry { displayName = displayName, sprite = sprite });

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SignageCatalog] Wrote {catalog.entries.Count} entries to {CatalogAssetPath}");
    }

    /// <summary>Forces PNGs in the folder to Sprite (Single) so <c>t:Sprite</c> / sub-asset resolution works.</summary>
    private static void EnsurePngsImportedAsSprites(string folder)
    {
        var guids = AssetDatabase.FindAssets("", new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static void CollectSpritesInto(
        List<(string path, string name, Sprite sprite)> list,
        string folder,
        string catalogPath,
        string filter)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets(filter, new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(path, catalogPath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seenPaths.Add(path))
                continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var o in assets)
                {
                    if (o is Sprite s)
                    {
                        sprite = s;
                        break;
                    }
                }
            }

            if (sprite == null)
                continue;

            var displayName = Path.GetFileNameWithoutExtension(path);
            list.Add((path, displayName, sprite));
        }
    }
}
