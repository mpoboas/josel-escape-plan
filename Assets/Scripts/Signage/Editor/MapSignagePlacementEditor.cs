using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapSignagePlacement))]
public class MapSignagePlacementEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var placement = (MapSignagePlacement)target;
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MapSignagePlacement.catalog)));

        var catalog = placement.catalog;
        var indexProp = serializedObject.FindProperty(nameof(MapSignagePlacement.symbolIndex));

        if (catalog != null && catalog.entries != null && catalog.entries.Count > 0)
        {
            var names = new string[catalog.entries.Count];
            for (int i = 0; i < names.Length; i++)
            {
                var e = catalog.entries[i];
                names[i] = string.IsNullOrEmpty(e.displayName)
                    ? (e.sprite != null ? e.sprite.name : $"Entry {i}")
                    : e.displayName;
            }

            indexProp.intValue = EditorGUILayout.Popup("Symbol", indexProp.intValue, names);
        }
        else
        {
            EditorGUILayout.PropertyField(indexProp);
            if (catalog == null)
                EditorGUILayout.HelpBox("Assign a SignageCatalog (Assets → Create → Signage → Regenerate catalog…).", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Catalog has no entries. Run the regenerate menu.", MessageType.Warning);
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MapSignagePlacement.worldSize)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MapSignagePlacement.yOffset)));

        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Apply / refresh visual"))
            placement.ApplyVisual();
    }
}
