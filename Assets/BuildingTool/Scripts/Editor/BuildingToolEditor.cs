using UnityEngine;
using UnityEditor;

namespace BuildingSystem.Editor
{
    [CustomEditor(typeof(BuildingTool))]
    public class BuildingToolEditor : UnityEditor.Editor
    {
        // ----------------------------------------------------------------
        // State
        // ----------------------------------------------------------------
        private BuildingTool    tool;
        private Quaternion      currentRotation = Quaternion.identity;
        private GameObject      ghostObject;
        private PlaceableObject currentPrefab;

        // Debug toggles (persisted via EditorPrefs)
        private bool showGridGizmos  = true;
        private bool showAlignGizmos = false;

        private const string PREF_GRID  = "BuildingTool_ShowGrid";
        private const string PREF_ALIGN = "BuildingTool_ShowAlign";

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------

        private void OnEnable()
        {
            tool = (BuildingTool)target;
            SceneView.duringSceneGui += OnSceneGUI_Custom;
            showGridGizmos  = EditorPrefs.GetBool(PREF_GRID,  true);
            showAlignGizmos = EditorPrefs.GetBool(PREF_ALIGN, false);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI_Custom;
            DestroyGhost();
        }

        // ----------------------------------------------------------------
        // Inspector
        // ----------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Palette Actions", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate Default Primitives"))
                PrimitiveFactory.GenerateDefaultPalette(tool);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Debug Gizmos", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            showGridGizmos  = EditorGUILayout.Toggle("Show Grid",             showGridGizmos);
            showAlignGizmos = EditorGUILayout.Toggle("Show Alignment Offset", showAlignGizmos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREF_GRID,  showGridGizmos);
                EditorPrefs.SetBool(PREF_ALIGN, showAlignGizmos);
            }

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Scene Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Left Click  – Place\n" +
                "Right Click – Remove (any object at cell)\n" +
                "R           – Rotate 90°\n" +
                "C           – Cycle object type\n" +
                "N / M       – Floor down / up",
                MessageType.None);
        }

        // ----------------------------------------------------------------
        // Scene GUI
        // ----------------------------------------------------------------

        private void OnSceneGUI_Custom(SceneView sceneView)
        {
            // ---- This tool is editor-only. Never run during Play Mode. ----
            if (Application.isPlaying)
            {
                DestroyGhost(); // ensure ghost is cleaned up when entering play
                return;
            }

            if (tool.buildingPalette == null || tool.buildingPalette.availableObjects.Count == 0)
                return;

            Event e = Event.current;

            HandleKeyboard(e);

            int index = Mathf.Clamp(tool.selectedPaletteIndex, 0,
                                    tool.buildingPalette.availableObjects.Count - 1);
            tool.selectedPaletteIndex = index;
            currentPrefab = tool.buildingPalette.availableObjects[index];

            // Raycast to the current floor's horizontal plane
            float   floorY      = tool.currentFloor * tool.gridSize;
            Vector3 planeOrigin = tool.transform.TransformPoint(new Vector3(0, floorY, 0));
            var     gridPlane   = new Plane(Vector3.up, planeOrigin);
            Ray     ray         = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (gridPlane.Raycast(ray, out float enter))
            {
                Vector3    hitWorld = ray.GetPoint(enter);
                Vector3    localHit = tool.transform.InverseTransformPoint(hitWorld);
                Vector3Int gridPos  = GridData.PositionToGridCoord(localHit, tool.gridSize);
                gridPos.y = tool.currentFloor; // pin to the selected floor

                if (showGridGizmos) DrawGrid(gridPos);
                UpdateGhost(gridPos);

                // Prevent Unity from de-selecting our tool's GameObject
                int id = GUIUtility.GetControlID(FocusType.Passive);
                if (e.type == EventType.Layout)
                    HandleUtility.AddDefaultControl(id);

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (tool.Grid.CanPlaceObject(gridPos, currentPrefab, currentRotation))
                    {
                        PlaceObject(gridPos);
                        e.Use();
                    }
                }
                else if (e.type == EventType.MouseDown && e.button == 1)
                {
                    RemoveObjectAt(gridPos);
                    e.Use();
                }

                sceneView.Repaint();
            }
            else
            {
                DestroyGhost();
            }
        }

        // ----------------------------------------------------------------
        // Keyboard
        // ----------------------------------------------------------------

        private void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.R)
            {
                currentRotation *= Quaternion.Euler(0, 90, 0);
                DestroyGhost();
                e.Use();
            }
            else if (e.keyCode == KeyCode.C)
            {
                tool.selectedPaletteIndex =
                    (tool.selectedPaletteIndex + 1) % tool.buildingPalette.availableObjects.Count;
                currentRotation = Quaternion.identity; // reset rotation on type change
                DestroyGhost();
                e.Use();
            }
            else if (e.keyCode == KeyCode.N)
            {
                tool.currentFloor--;
                e.Use();
            }
            else if (e.keyCode == KeyCode.M)
            {
                tool.currentFloor++;
                e.Use();
            }
        }

        // ----------------------------------------------------------------
        // Ghost (preview)
        // ----------------------------------------------------------------

        private void UpdateGhost(Vector3Int gridPos)
        {
            if (currentPrefab == null) return;

            // Rebuild ghost if stale
            if (ghostObject == null ||
                ghostObject.GetComponent<PlaceableObject>()?.objectType != currentPrefab.objectType)
            {
                DestroyGhost();
                ghostObject           = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
                ghostObject.name      = "__Ghost__";
                ghostObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (var c in ghostObject.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c);
            }

            // Position = cell centre + alignment offset
            Vector3 cellCentre  = tool.transform.TransformPoint(GridData.GridCoordToPosition(gridPos, tool.gridSize));
            Vector3 alignOffset = currentPrefab.GetAlignmentOffset(currentRotation, tool.gridSize);
            ghostObject.transform.position = cellCentre + alignOffset;
            ghostObject.transform.rotation = tool.transform.rotation * currentRotation;

            // Colour feedback — query the live hierarchy
            bool  canPlace   = tool.Grid.CanPlaceObject(gridPos, currentPrefab, currentRotation);
            Color ghostColor = canPlace ? new Color(0f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
            foreach (var r in ghostObject.GetComponentsInChildren<Renderer>())
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color",     ghostColor);
                block.SetColor("_BaseColor", ghostColor);
                r.SetPropertyBlock(block);
            }

            // Alignment gizmos
            if (showAlignGizmos)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(cellCentre, Vector3.up, 0.08f);
                Handles.color = Color.cyan;
                Handles.DrawLine(cellCentre, cellCentre + alignOffset);
                Handles.DrawWireDisc(cellCentre + alignOffset, Vector3.up, 0.06f);
            }
        }

        private void DestroyGhost()
        {
            if (ghostObject != null)
                DestroyImmediate(ghostObject);
        }

        // ----------------------------------------------------------------
        // Place / Remove
        // ----------------------------------------------------------------

        private void PlaceObject(Vector3Int gridPos)
        {
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
            Undo.RegisterCreatedObjectUndo(newObj, "Place Building Object");

            Transform floorParent = tool.GetFloorParent(tool.currentFloor);
            newObj.transform.SetParent(floorParent);

            Vector3 cellCentre  = tool.transform.TransformPoint(GridData.GridCoordToPosition(gridPos, tool.gridSize));
            Vector3 alignOffset = currentPrefab.GetAlignmentOffset(currentRotation, tool.gridSize);
            newObj.transform.position = cellCentre + alignOffset;
            newObj.transform.rotation = tool.transform.rotation * currentRotation;

            EditorUtility.SetDirty(tool.gameObject);
            tool.Grid.InvalidateCache(); // hierarchy changed, refresh next query

            // Continuous wall snapping (Shift + click)
            if (currentPrefab.alignment == PlacementAlignment.Edge && Event.current.shift)
                TryExtendWall(gridPos, floorParent);
        }

        /// <summary>
        /// Right-click removal: finds ANY placeable object at the hovered cell,
        /// regardless of which object type is currently selected in the palette.
        /// Tries same-alignment match first, then falls back to anything at the cell.
        /// </summary>
        private void RemoveObjectAt(Vector3Int gridPos)
        {
            if (tool == null) return;

            // 1. Try to find by the currently selected alignment+rotation (most likely intent)
            PlaceableObject found = tool.Grid.FindAnyObjectAt(gridPos, currentPrefab.alignment, currentRotation);

            // 2. Fallback: scan all PlaceableObjects physically near this cell
            if (found == null)
            {
                float       s         = tool.gridSize;
                Vector3     cellWorld = tool.transform.TransformPoint(GridData.GridCoordToPosition(gridPos, s));
                float       threshold = s * 0.55f; // slightly larger than half-cell

                foreach (var po in tool.transform.GetComponentsInChildren<PlaceableObject>())
                {
                    if (Vector3.Distance(po.transform.position, cellWorld) < threshold)
                    {
                        found = po;
                        break;
                    }
                }
            }

            if (found == null) return;

            Undo.DestroyObjectImmediate(found.gameObject);
            tool.Grid.InvalidateCache(); // hierarchy changed, refresh next query
            EditorUtility.SetDirty(tool.gameObject);
        }

        private void TryExtendWall(Vector3Int fromCell, Transform floorParent)
        {
            Vector3    forward      = currentRotation * Vector3.forward;
            Vector3Int step         = new Vector3Int(Mathf.RoundToInt(forward.x), 0, Mathf.RoundToInt(forward.z));
            Vector3Int adjacentCell = fromCell + step;
            Quaternion oppositeRot  = currentRotation * Quaternion.Euler(0, 180, 0);

            if (!tool.Grid.CanPlaceObject(adjacentCell, currentPrefab, oppositeRot)) return;

            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
            Undo.RegisterCreatedObjectUndo(newObj, "Place Building Object (Auto-extend)");
            newObj.transform.SetParent(floorParent);

            Vector3 cellCentre  = tool.transform.TransformPoint(GridData.GridCoordToPosition(adjacentCell, tool.gridSize));
            Vector3 alignOffset = currentPrefab.GetAlignmentOffset(oppositeRot, tool.gridSize);
            newObj.transform.position = cellCentre + alignOffset;
            newObj.transform.rotation = tool.transform.rotation * oppositeRot;

            EditorUtility.SetDirty(tool.gameObject);
        }

        // ----------------------------------------------------------------
        // Grid gizmos
        // ----------------------------------------------------------------

        private void DrawGrid(Vector3Int center)
        {
            float s      = tool.gridSize;
            int   radius = 5;

            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                Vector3Int coord   = new Vector3Int(center.x + x, center.y, center.z + z);
                Vector3    cellPos = tool.transform.TransformPoint(GridData.GridCoordToPosition(coord, s));
                Vector3    p1 = cellPos + new Vector3(-s/2, 0, -s/2);
                Vector3    p2 = cellPos + new Vector3( s/2, 0, -s/2);
                Vector3    p3 = cellPos + new Vector3( s/2, 0,  s/2);
                Vector3    p4 = cellPos + new Vector3(-s/2, 0,  s/2);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p3);
                Handles.DrawLine(p3, p4);
                Handles.DrawLine(p4, p1);
            }

            // Highlight hovered cell
            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            Vector3 cp = tool.transform.TransformPoint(GridData.GridCoordToPosition(center, s));
            Handles.DrawWireDisc(cp, Vector3.up, s * 0.1f);

            if (showAlignGizmos)
            {
                // Edge midpoints (cyan)
                Handles.color = new Color(0f, 0.8f, 1f, 0.5f);
                foreach (var em in new[] {
                    cp + new Vector3( s/2, 0,    0), cp + new Vector3(-s/2, 0,    0),
                    cp + new Vector3(   0, 0,  s/2), cp + new Vector3(   0, 0, -s/2) })
                    Handles.DrawWireDisc(em, Vector3.up, 0.05f);

                // Corner points (orange)
                Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
                foreach (var co in new[] {
                    cp + new Vector3( s/2, 0,  s/2), cp + new Vector3(-s/2, 0,  s/2),
                    cp + new Vector3( s/2, 0, -s/2), cp + new Vector3(-s/2, 0, -s/2) })
                    Handles.DrawWireDisc(co, Vector3.up, 0.05f);
            }
        }
    }
}
