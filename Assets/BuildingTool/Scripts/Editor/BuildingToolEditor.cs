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

        // Edit Mode selection
        private PlaceableObject selectedEditObject;

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
                "Mode Switch:\n" +
                "1 – Build Mode\n" +
                "2 – Edit Mode\n" +
                "3 – Remove Mode\n\n" +
                "General Tools:\n" +
                "N / M – Floor down / up\n" +
                "C     – Cycle object type (Build Mode)\n" +
                "R     – Rotate Y (Yaw)\n" +
                "T     – Rotate X (Pitch - useful for laying down corners)\n" +
                "Y     – Rotate Z (Roll)",
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
                DestroyGhost();
                return;
            }

            if (tool.buildingPalette == null || tool.buildingPalette.availableObjects.Count == 0)
                return;

            Event e = Event.current;

            HandleKeyboard(e);
            DrawHUD();

            int index = Mathf.Clamp(tool.selectedPaletteIndex, 0, tool.buildingPalette.availableObjects.Count - 1);
            tool.selectedPaletteIndex = index;
            currentPrefab = tool.buildingPalette.availableObjects[index];

            // In all modes, we consume passive clicks to prevent losing selection of the tool
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            if (tool.currentMode == ToolMode.Build)
            {
                RunBuildMode(e, sceneView);
            }
            else if (tool.currentMode == ToolMode.Edit)
            {
                RunEditMode(e, sceneView);
            }
            else if (tool.currentMode == ToolMode.Remove)
            {
                RunRemoveMode(e, sceneView);
            }
        }

        private void DrawHUD()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 250, 100));
            var style = new GUIStyle(GUI.skin.box);
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            
            Color modeColor = Color.white;
            if (tool.currentMode == ToolMode.Build) modeColor = Color.green;
            else if (tool.currentMode == ToolMode.Edit) modeColor = Color.yellow;
            else if (tool.currentMode == ToolMode.Remove) modeColor = new Color(1f, 0.3f, 0.3f);
            
            GUI.color = modeColor;
            GUILayout.Box($"MODE: {tool.currentMode.ToString().ToUpper()}", style);
            GUI.color = Color.white;
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ----------------------------------------------------------------
        // Modes
        // ----------------------------------------------------------------

        private void RunBuildMode(Event e, SceneView sceneView)
        {
            float floorY = tool.currentFloor * tool.gridSize;
            Vector3 planeOrigin = tool.transform.TransformPoint(new Vector3(0, floorY, 0));
            var gridPlane = new Plane(Vector3.up, planeOrigin);
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (gridPlane.Raycast(ray, out float enter))
            {
                Vector3 hitWorld = ray.GetPoint(enter);
                Vector3 localHit = tool.transform.InverseTransformPoint(hitWorld);
                Vector3Int gridPos = GridData.PositionToGridCoord(localHit, tool.gridSize);
                gridPos.y = tool.currentFloor;

                if (showGridGizmos) DrawGrid(gridPos);
                UpdateGhost(gridPos);

                if (e.type == EventType.MouseDown && e.button == 0) // Left click Place
                {
                    PlaceObject(gridPos);
                    e.Use();
                }

                sceneView.Repaint();
            }
            else
            {
                DestroyGhost();
            }
        }

        private void RunRemoveMode(Event e, SceneView sceneView)
        {
            DestroyGhost();

            PlaceableObject hoverObj = GetHoveredPlaceableObject(e.mousePosition);
            if (hoverObj != null)
            {
                // Highlight hovered object in red
                Handles.color = new Color(1f, 0f, 0f, 0.5f);
                Bounds b = GetObjectBounds(hoverObj);
                Handles.DrawWireCube(b.center, b.size * 1.05f);

                if (e.type == EventType.MouseDown && e.button == 0) // Left click Remove
                {
                    Undo.DestroyObjectImmediate(hoverObj.gameObject);
                    tool.Grid.InvalidateCache();
                    EditorUtility.SetDirty(tool.gameObject);
                    e.Use();
                }
            }
            sceneView.Repaint();
        }

        private void RunEditMode(Event e, SceneView sceneView)
        {
            DestroyGhost();

            // Deselect if clicking empty space
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceableObject clickedObj = GetHoveredPlaceableObject(e.mousePosition);
                selectedEditObject = clickedObj;
                e.Use();
            }

            // Draw hover highlight
            PlaceableObject hoverObj = GetHoveredPlaceableObject(e.mousePosition);
            if (hoverObj != null && hoverObj != selectedEditObject)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Bounds b = GetObjectBounds(hoverObj);
                Handles.DrawWireCube(b.center, b.size * 1.02f);
                sceneView.Repaint();
            }

            // Draw selected highlight
            if (selectedEditObject != null)
            {
                Handles.color = Color.yellow;
                Bounds b = GetObjectBounds(selectedEditObject);
                Handles.DrawWireCube(b.center, b.size * 1.05f);
                
                Handles.Label(b.center + Vector3.up * (b.extents.y + 0.2f), "SELECTED (Press R, T, Y to rotate)");
                sceneView.Repaint();
            }
        }

        private PlaceableObject cachedHover;

        private PlaceableObject GetHoveredPlaceableObject(Vector2 mousePosition)
        {
            Event e = Event.current;
            // Prevent picking during Layout or Repaint to avoid '!m_InsideContext' GUI exception
            if (e.type == EventType.Layout || e.type == EventType.Repaint)
            {
                if (cachedHover != null && cachedHover.gameObject == null) cachedHover = null;
                return cachedHover;
            }

            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            if (picked == null || picked.name == "__Ghost__")
            {
                cachedHover = null;
                return null;
            }

            PlaceableObject po = picked.GetComponentInParent<PlaceableObject>();
            if (po != null && po.transform.IsChildOf(tool.transform))
            {
                cachedHover = po;
                return po;
            }

            cachedHover = null;
            return null;
        }

        private Bounds GetObjectBounds(PlaceableObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // ----------------------------------------------------------------
        // Keyboard
        // ----------------------------------------------------------------

        private void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            // Mode switching
            if (e.keyCode == KeyCode.Alpha1) { tool.currentMode = ToolMode.Build; DestroyGhost(); e.Use(); return; }
            if (e.keyCode == KeyCode.Alpha2) { tool.currentMode = ToolMode.Edit; DestroyGhost(); e.Use(); return; }
            if (e.keyCode == KeyCode.Alpha3) { tool.currentMode = ToolMode.Remove; DestroyGhost(); e.Use(); return; }

            // Rotations (R = Y-axis/Yaw, T = X-axis/Pitch, Y = Z-axis/Roll)
            if (e.keyCode == KeyCode.R || e.keyCode == KeyCode.T || e.keyCode == KeyCode.Y)
            {
                Vector3 eulerChange = Vector3.zero;
                if (e.keyCode == KeyCode.R) eulerChange = new Vector3(0, 90, 0);       // Yaw
                else if (e.keyCode == KeyCode.T) eulerChange = new Vector3(90, 0, 0);  // Pitch (lay down)
                else if (e.keyCode == KeyCode.Y) eulerChange = new Vector3(0, 0, 90);  // Roll

                if (tool.currentMode == ToolMode.Build)
                {
                    currentRotation *= Quaternion.Euler(eulerChange);
                    DestroyGhost();
                    e.Use();
                }
                else if (tool.currentMode == ToolMode.Edit && selectedEditObject != null)
                {
                    Undo.RecordObject(selectedEditObject.transform, "Rotate Edit Object");
                    
                    // Rotate the object in place
                    selectedEditObject.transform.rotation *= Quaternion.Euler(eulerChange);
                    
                    EditorUtility.SetDirty(selectedEditObject);
                    e.Use();
                }
                return;
            }

            // Other keys
            if (e.keyCode == KeyCode.C && tool.currentMode == ToolMode.Build)
            {
                tool.selectedPaletteIndex = (tool.selectedPaletteIndex + 1) % tool.buildingPalette.availableObjects.Count;
                currentRotation = Quaternion.identity; // reset rotation
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

            if (ghostObject == null ||
                ghostObject.GetComponent<PlaceableObject>()?.objectType != currentPrefab.objectType)
            {
                DestroyGhost();
                ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
                ghostObject.name = "__Ghost__";
                ghostObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (var c in ghostObject.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c);
            }

            Vector3 cellCentre = tool.transform.TransformPoint(GridData.GridCoordToPosition(gridPos, tool.gridSize));
            Vector3 alignOffset = currentPrefab.GetAlignmentOffset(currentRotation, tool.gridSize);
            ghostObject.transform.position = cellCentre + alignOffset;
            ghostObject.transform.rotation = tool.transform.rotation * currentRotation;

            // Since overlapping is allowed, ghost is always green
            Color ghostColor = new Color(0f, 1f, 0f, 0.4f);
            foreach (var r in ghostObject.GetComponentsInChildren<Renderer>())
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", ghostColor);
                block.SetColor("_BaseColor", ghostColor);
                r.SetPropertyBlock(block);
            }

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
        // Place
        // ----------------------------------------------------------------

        private void PlaceObject(Vector3Int gridPos)
        {
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
            Undo.RegisterCreatedObjectUndo(newObj, "Place Building Object");

            Transform floorParent = tool.GetFloorParent(tool.currentFloor);
            newObj.transform.SetParent(floorParent);

            Vector3 cellCentre = tool.transform.TransformPoint(GridData.GridCoordToPosition(gridPos, tool.gridSize));
            Vector3 alignOffset = currentPrefab.GetAlignmentOffset(currentRotation, tool.gridSize);
            newObj.transform.position = cellCentre + alignOffset;
            newObj.transform.rotation = tool.transform.rotation * currentRotation;

            EditorUtility.SetDirty(tool.gameObject);
            tool.Grid.InvalidateCache(); 

            // Continuous wall snapping (Shift + click)
            if (currentPrefab.alignment == PlacementAlignment.Edge && Event.current.shift)
                TryExtendWall(gridPos, floorParent);
        }

        private void TryExtendWall(Vector3Int fromCell, Transform floorParent)
        {
            Vector3 forward = currentRotation * Vector3.forward;
            Vector3Int step = new Vector3Int(Mathf.RoundToInt(forward.x), 0, Mathf.RoundToInt(forward.z));
            Vector3Int adjacentCell = fromCell + step;
            Quaternion oppositeRot = currentRotation * Quaternion.Euler(0, 180, 0);

            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(currentPrefab.gameObject);
            Undo.RegisterCreatedObjectUndo(newObj, "Place Building Object (Auto-extend)");
            newObj.transform.SetParent(floorParent);

            Vector3 cellCentre = tool.transform.TransformPoint(GridData.GridCoordToPosition(adjacentCell, tool.gridSize));
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
            float s = tool.gridSize;
            int radius = 5;

            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                Vector3Int coord = new Vector3Int(center.x + x, center.y, center.z + z);
                Vector3 cellPos = tool.transform.TransformPoint(GridData.GridCoordToPosition(coord, s));
                Vector3 p1 = cellPos + new Vector3(-s/2, 0, -s/2);
                Vector3 p2 = cellPos + new Vector3(s/2, 0, -s/2);
                Vector3 p3 = cellPos + new Vector3(s/2, 0, s/2);
                Vector3 p4 = cellPos + new Vector3(-s/2, 0, s/2);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p3);
                Handles.DrawLine(p3, p4);
                Handles.DrawLine(p4, p1);
            }

            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            Vector3 cp = tool.transform.TransformPoint(GridData.GridCoordToPosition(center, s));
            Handles.DrawWireDisc(cp, Vector3.up, s * 0.1f);

            if (showAlignGizmos)
            {
                Handles.color = new Color(0f, 0.8f, 1f, 0.5f);
                foreach (var em in new[] {
                    cp + new Vector3(s/2, 0, 0), cp + new Vector3(-s/2, 0, 0),
                    cp + new Vector3(0, 0, s/2), cp + new Vector3(0, 0, -s/2) })
                    Handles.DrawWireDisc(em, Vector3.up, 0.05f);

                Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
                foreach (var co in new[] {
                    cp + new Vector3(s/2, 0, s/2), cp + new Vector3(-s/2, 0, s/2),
                    cp + new Vector3(s/2, 0, -s/2), cp + new Vector3(-s/2, 0, -s/2) })
                    Handles.DrawWireDisc(co, Vector3.up, 0.05f);
            }
        }
    }
}
