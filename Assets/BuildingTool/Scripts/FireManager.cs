using UnityEngine;

namespace BuildingSystem
{
    /// <summary>
    /// Global manager that finds all FireSpawnpoints and links them to the FireSpread simulation.
    /// This ensures we don't have heavy Update/Coroutine logic running on inactive spawns in Editor.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        private void Start()
        {
            // Find all fire spawn locations placed by the tool
            FireSpawnpoint[] spawnpoints = FindObjectsByType<FireSpawnpoint>(FindObjectsSortMode.None);
            
            if (spawnpoints.Length == 0)
            {
                Debug.Log("FireManager: No FireSpawnpoints found in the scene.");
                return;
            }

            foreach (var spawnpoint in spawnpoints)
            {
                // Disable any Editor-only renderers/meshes on the spawnpoint if we added them later
                MeshRenderer mr = spawnpoint.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                // Attach the active simulation component
                FireSpreadNode spreadNode = spawnpoint.gameObject.AddComponent<FireSpreadNode>();
                spreadNode.Initialize(spawnpoint);
            }
        }
    }
}
