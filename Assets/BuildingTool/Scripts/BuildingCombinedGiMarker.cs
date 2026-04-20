using System.Collections.Generic;
using UnityEngine;

namespace BuildingSystem
{
    /// <summary>
    /// Editor-generated marker attached to a combined-for-GI child GameObject under a <c>Floor N</c> root.
    /// Records the source MeshRenderer GameObjects whose MeshRenderer was disabled and whose
    /// <see cref="UnityEditor.StaticEditorFlags.ContributeGI"/> flag was cleared when the combine
    /// operation ran, so the revert menu can restore them exactly.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingCombinedGiMarker : MonoBehaviour
    {
        [Tooltip("Source tile GameObjects whose MeshRenderer was disabled and whose ContributeGI was cleared.")]
        public List<GameObject> sourceGameObjects = new List<GameObject>();

        [Tooltip("Floor N level this combined mesh belongs to.")]
        public int floorLevel;

        [Tooltip("Name of the material this combined mesh renders.")]
        public string materialName;
    }
}
