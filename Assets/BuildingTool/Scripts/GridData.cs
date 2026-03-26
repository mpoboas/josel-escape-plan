using UnityEngine;
using System.Collections.Generic;

namespace BuildingSystem
{
    /// <summary>
    /// Grid state management.
    ///
    /// Design principle: The scene hierarchy IS the source of truth.
    /// All placement queries (CanPlace, FindAt) scan the live hierarchy so
    /// they can never be stale after Undo, Redo, manual deletes, etc.
    /// The 'parent' Transform reference is set once and used every query.
    /// </summary>
    public class GridData
    {
        private readonly float     gridSize;
        private readonly Transform root;   // the BuildingTool GameObject

        // ----------------------------------------------------------------
        // Per-frame cache — GetComponentsInChildren is expensive; call it
        // at most once per editor frame no matter how many queries arrive.
        // ----------------------------------------------------------------
        private PlaceableObject[] cachedObjects = new PlaceableObject[0];
        private int               cacheFrame    = -1;

        public GridData(float gridSize, Transform root)
        {
            this.gridSize = gridSize;
            this.root     = root;
        }

        /// <summary>Returns the cached PlaceableObject list, refreshing once per frame.</summary>
        private PlaceableObject[] AllObjects()
        {
            // Time.frameCount works in both Edit and Play mode in Unity.
            int frame = UnityEngine.Time.frameCount;
            if (frame != cacheFrame)
            {
                cachedObjects = root != null
                    ? root.GetComponentsInChildren<PlaceableObject>()
                    : new PlaceableObject[0];
                cacheFrame = frame;
            }
            return cachedObjects;
        }

        /// <summary>Force the cache to refresh on the next query (call after placing/removing).</summary>
        public void InvalidateCache() => cacheFrame = -1;

        // ----------------------------------------------------------------
        // Placement queries — always live, never stale
        // ----------------------------------------------------------------

        /// <summary>
        /// Returns true if the given cell(s) are free for this prefab+rotation.
        /// Floors (Center) only conflict with other Center objects.
        /// Walls/Corners (Edge/Corner) only conflict with a same-alignment object
        /// at the same cell AND same rotation step.
        /// </summary>
        public bool CanPlaceObject(Vector3Int gridPos, PlaceableObject prefab, Quaternion rotation)
        {
            // By request, overlapping objects is strictly allowed in all circumstances.
            // Returning true immediately bypasses all grid cell collision checks.
            return true;
        }

        /// <summary>
        /// Finds whatever Center-aligned object occupies the given cell, or null.
        /// </summary>
        public PlaceableObject FindCenterObjectAt(Vector3Int gridPos)
        {
            foreach (var po in AllObjects())
            {
                if (po.alignment != PlacementAlignment.Center) continue;
                var coord = WorldPosToGridCoordWithOffset(po.transform.position,
                                                          po.transform.rotation,
                                                          PlacementAlignment.Center);
                if (coord == gridPos) return po;
            }
            return null;
        }

        /// <summary>
        /// Finds whatever Edge/Corner object occupies the given cell+rotationStep, or null.
        /// </summary>
        public PlaceableObject FindEdgeObjectAt(Vector3Int gridPos, int rotStep)
        {
            foreach (var po in AllObjects())
            {
                if (po.alignment == PlacementAlignment.Center) continue;
                var coord = WorldPosToGridCoordWithOffset(po.transform.position,
                                                          po.transform.rotation,
                                                          po.alignment);
                if (coord != gridPos) continue;
                if (RotationToStep(po.transform.rotation) != rotStep) continue;
                return po;
            }
            return null;
        }

        /// <summary>
        /// Finds the closest PlaceableObject at the given grid cell regardless of
        /// alignment or rotation — used for right-click removal.
        /// </summary>
        public PlaceableObject FindAnyObjectAt(Vector3Int gridPos, PlacementAlignment alignment, Quaternion rotation)
        {
            if (root == null) return null;

            if (alignment == PlacementAlignment.Center)
                return FindCenterObjectAt(gridPos);

            return FindEdgeObjectAt(gridPos, RotationToStep(rotation));
        }

        // ----------------------------------------------------------------
        // Coordinate conversion helpers
        // ----------------------------------------------------------------

        /// <summary>
        /// Converts a world position + rotation back to the grid cell the object
        /// "belongs" to (strips out the alignment offset first).
        /// </summary>
        public Vector3Int WorldPosToGridCoord(Vector3 worldPos, Quaternion worldRot)
        {
            // Find which PlaceableObject this transform belongs to so we can get alignment
            // — we don't have a reference here, so we approximate by computing the local pos
            // without any offset; the caller must ensure objects are placed correctly.
            Vector3 localPos = root.InverseTransformPoint(worldPos);
            return PositionToGridCoord(localPos, gridSize);
        }

        /// <summary>
        /// Converts a world position back to its grid cell, given the object's alignment
        /// offset has already been baked into that world position.
        /// </summary>
        public Vector3Int WorldPosToGridCoordWithOffset(Vector3 worldPos, Quaternion worldRot,
                                                         PlacementAlignment alignment)
        {
            // Reverse the alignment offset to get the anchor point
            float half = gridSize * 0.5f;
            Vector3 alignOffset = Vector3.zero;

            Quaternion horizontalRot = Quaternion.Euler(0, worldRot.eulerAngles.y, 0);

            if (alignment == PlacementAlignment.Edge)
            {
                Vector3 fwd = SnapAxis(horizontalRot * Vector3.forward);
                alignOffset = fwd * half;
            }
            else if (alignment == PlacementAlignment.Corner)
            {
                Vector3 fwd   = SnapAxis(horizontalRot * Vector3.forward);
                Vector3 right = SnapAxis(horizontalRot * Vector3.right);
                alignOffset = (fwd + right) * half;
            }

            Vector3 localRaw = root.InverseTransformPoint(worldPos - alignOffset);
            return PositionToGridCoord(localRaw, gridSize);
        }

        // ----------------------------------------------------------------
        // Static helpers (pure math, no state)
        // ----------------------------------------------------------------

        public static List<Vector3Int> GetOccupiedCells(Vector3Int anchorPos, Vector3Int size, Quaternion rotation)
        {
            var cells = new List<Vector3Int>();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                Vector3 rotated = rotation * new Vector3(x, y, z);
                cells.Add(anchorPos + new Vector3Int(
                    Mathf.RoundToInt(rotated.x),
                    Mathf.RoundToInt(rotated.y),
                    Mathf.RoundToInt(rotated.z)));
            }
            return cells;
        }

        /// <summary>Maps a Y-rotation to a 0-3 step (0=0°, 1=90°, 2=180°, 3=270°).</summary>
        public static int RotationToStep(Quaternion rotation)
        {
            float angle = rotation.eulerAngles.y;
            int   step  = Mathf.RoundToInt(angle / 90f) % 4;
            return step < 0 ? step + 4 : step;
        }

        public static Vector3Int PositionToGridCoord(Vector3 localPosition, float gridSize)
        {
            return new Vector3Int(
                Mathf.RoundToInt(localPosition.x / gridSize),
                Mathf.RoundToInt(localPosition.y / gridSize),
                Mathf.RoundToInt(localPosition.z / gridSize));
        }

        public static Vector3 GridCoordToPosition(Vector3Int coord, float gridSize)
        {
            return new Vector3(coord.x * gridSize, coord.y * gridSize, coord.z * gridSize);
        }

        public static Vector3 SnapAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (az >= ax && az >= ay) return new Vector3(0, 0, Mathf.Sign(v.z));
            return new Vector3(0, Mathf.Sign(v.y), 0);
        }
    }
}
