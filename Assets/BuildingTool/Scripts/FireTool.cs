using UnityEngine;

namespace BuildingSystem
{
    public enum FireToolMode
    {
        Place,
        Remove
    }

    /// <summary>
    /// Component placed on a GameObject to act as the locator and configuration 
    /// for the Editor-only fire placement tool.
    /// </summary>
    public class FireTool : MonoBehaviour
    {
        [Header("Tool Settings")]
        public FireToolMode currentMode = FireToolMode.Place;
        public float gridSize = 1f;
        public int currentFloor = 0;

        [Header("Live Fire Settings (Update in Play Mode!)")]
        [Tooltip("Speed the column of fire travels upwards")]
        public float upwardSpeed = 4f;
        
        [Tooltip("Thickness of the base fire column")]
        public float pillarRadius = 0.1f;
        
        [Tooltip("Multiplier for how many particles spawn in the pillar")]
        public float pillarEmissionMultiplier = 20f;
        
        [Tooltip("Visual size of the pillar particles")]
        public float pillarParticleSize = 0.4f;

        [Space(10)]
        [Tooltip("Speed the fire travels along the ceiling")]
        public float ceilingSpreadSpeed = 4f;
        
        [Tooltip("Maximum radius the ceiling fire can reach")]
        public float ceilingMaxRadius = 5f;
        
        [Tooltip("Emission rate for the ceiling fire")]
        public float ceilingEmissionRate = 80f;
        
        [Tooltip("Visual size of the ceiling fire particles")]
        public float ceilingParticleSize = 0.6f;
        
        [Tooltip("Buoyancy push against ceiling (Negative means it floats up)")]
        public float ceilingGravity = 0f;

        // Static reference so FireSpreadNodes can easily read from it live
        public static FireTool Instance;

        private void OnEnable()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets or creates a container for the fire spawnpoints on the current floor level
        /// </summary>
        public Transform GetFloorParent(int floorLevel)
        {
            string folderName = $"Fires_Floor_{floorLevel}";
            Transform existing = transform.Find(folderName);
            if (existing != null) return existing;

            GameObject go = new GameObject(folderName);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
