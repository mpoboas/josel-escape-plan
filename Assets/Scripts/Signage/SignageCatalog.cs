using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered list of sprites for floor-plan signage. Populate via the Editor menu
/// <b>Assets → Create → Signage → Regenerate catalog from Assets/Assets/Signage</b>.
/// </summary>
[CreateAssetMenu(fileName = "SignageCatalog", menuName = "Signage/Catalog", order = 0)]
public class SignageCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string displayName;
        public Sprite sprite;
    }

    public List<Entry> entries = new List<Entry>();

    public int EntryCount => entries != null ? entries.Count : 0;

    public Entry GetEntrySafe(int index)
    {
        if (entries == null || entries.Count == 0)
            return null;
        index = Mathf.Clamp(index, 0, entries.Count - 1);
        return entries[index];
    }
}
