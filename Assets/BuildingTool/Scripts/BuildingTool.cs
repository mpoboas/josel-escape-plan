using UnityEngine;

namespace BuildingSystem
{
    public class BuildingTool : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float gridSize = 1f;

        [Header("Palette")]
        public BuildingPalette buildingPalette;

        [Header("State")]
        public int currentFloor = 0;
        public int selectedPaletteIndex = 0;

        // GridData is stateless now (queries go to the live hierarchy).
        // We keep one instance just to hold the gridSize + root reference.
        private GridData gridData;

        public GridData Grid
        {
            get
            {
                if (gridData == null)
                    gridData = new GridData(gridSize, transform);
                return gridData;
            }
        }

        private void OnEnable()
        {
            gridData = new GridData(gridSize, transform);
        }

        /// <summary>
        /// Recreates the GridData instance (harmless since it's stateless,
        /// but kept so external callers such as undo callbacks still compile).
        /// </summary>
        public void RebuildGrid()
        {
            gridData = new GridData(gridSize, transform);
        }

        /// <summary>
        /// Gets or creates the organisational parent for a given floor level.
        /// </summary>
        public Transform GetFloorParent(int floorLevel)
        {
            string    floorName = $"Floor {floorLevel}";
            Transform existing  = transform.Find(floorName);
            if (existing != null) return existing;

            GameObject go = new GameObject(floorName);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;   // Y offset is baked into each object's world pos
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
