using UnityEngine;

namespace BuildingSystem
{
    /// <summary>
    /// Represents a pre-defined fire origin point that is invisible to players but visible in the developer tool.
    /// During playback, the FireSpreadManager will find these and initiate the simulated fire.
    /// </summary>
    public class FireSpawnpoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            // Draw a red wireframe box to indicate a fire spawn location in the Editor
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one);
            
            // Draw an arrow pointing up to indicate the spread direction
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1f, transform.position + Vector3.up * 2f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.1f);
        }
    }
}
