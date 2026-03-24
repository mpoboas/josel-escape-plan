using UnityEngine;

namespace BuildingSystem
{
    /// <summary>
    /// How this object aligns within a grid cell.
    ///
    ///   Center  - placed at the exact grid cell centre (e.g. floors, pillars).
    ///   Edge    - placed at one edge of the cell, shifted by half a grid unit
    ///             along its local Z-axis. After rotation, the axis changes
    ///             automatically so the offset always follows the piece.
    ///             e.g. Wall facing +Z → offset toward +Z edge.
    ///                  Wall facing +X → offset toward +X edge.
    ///   Corner  - placed at a grid corner (shifted ±half on both X and Z).
    ///             Rotation selects which of the four corners.
    /// </summary>
    public enum PlacementAlignment
    {
        Center,
        Edge,
        Corner
    }

    public class PlaceableObject : MonoBehaviour
    {
        [Tooltip("Human-readable type name (Floor, Wall, Corner, Stairs…)")]
        public string objectType = "Floor";

        [Tooltip("Footprint in grid cells. Use (1,1,1) for standard pieces.")]
        public Vector3Int size = Vector3Int.one;

        [Tooltip("How this piece snaps within its grid cell.")]
        public PlacementAlignment alignment = PlacementAlignment.Center;

        // ---------------------------------------------------------------
        // Helpers used by the editor placement logic
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns the world-space positional offset (relative to the grid cell
        /// centre) that this piece needs given <paramref name="rotation"/> and
        /// the owning tool's <paramref name="gridSize"/>.
        /// </summary>
        public Vector3 GetAlignmentOffset(Quaternion rotation, float gridSize)
        {
            float half = gridSize * 0.5f;

            switch (alignment)
            {
                case PlacementAlignment.Edge:
                    // The piece's "front" is its local +Z. Rotating the world-space
                    // +Z direction by the current rotation gives us the edge direction.
                    Vector3 forward = rotation * Vector3.forward;
                    // Snap to cardinal axis to avoid floating-point drift
                    forward = SnapToCardinal(forward);
                    return forward * half;

                case PlacementAlignment.Corner:
                    // Corners sit at the diagonal. Use both local X and Z.
                    Vector3 right   = SnapToCardinal(rotation * Vector3.right);
                    Vector3 fwd     = SnapToCardinal(rotation * Vector3.forward);
                    return (right + fwd) * half;

                default: // Center
                    return Vector3.zero;
            }
        }

        // ---------------------------------------------------------------
        // Private helpers
        // ---------------------------------------------------------------

        private static Vector3 SnapToCardinal(Vector3 v)
        {
            // Zero out the smallest two components so we keep only the dominant axis.
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az)
                return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (az >= ax && az >= ay)
                return new Vector3(0, 0, Mathf.Sign(v.z));
            return new Vector3(0, Mathf.Sign(v.y), 0);
        }
    }
}
