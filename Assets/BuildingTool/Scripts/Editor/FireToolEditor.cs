using UnityEngine;
using UnityEditor;

namespace BuildingSystem.Editor
{
    [CustomEditor(typeof(FireTool))]
    public class FireToolEditor : UnityEditor.Editor
    {
        private FireTool tool;
        private GameObject ghostObject;

        private void OnEnable()
        {
            tool = (FireTool)target;
            SceneView.duringSceneGui += OnSceneGUI_Custom;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI_Custom;
            DestroyGhost();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Mode Switch:\n" +
                "1 – Place Fire Mode\n" +
                "2 – Remove Fire Mode\n\n" +
                "General Tools:\n" +
                "N / M – Floor down / up", MessageType.Info);
        }

        private void OnSceneGUI_Custom(SceneView sceneView)
        {
            if (Application.isPlaying)
            {
                DestroyGhost();
                return;
            }

            Event e = Event.current;
            HandleKeyboard(e);
            DrawHUD();
            DrawAllSpawnpoints();

            // Intercept passive events so Unity doesn't unselect our tool when clicking the floor
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }

            if (tool.currentMode == FireToolMode.Place)
            {
                RunPlaceMode(e, sceneView);
            }
            else if (tool.currentMode == FireToolMode.Remove)
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

            Color modeColor = tool.currentMode == FireToolMode.Place ? Color.red : new Color(1f, 0.3f, 0.3f);
            GUI.color = modeColor;
            GUILayout.Box($"FIRE TOOL: {tool.currentMode.ToString().ToUpper()}", style);
            GUI.color = Color.white;
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawAllSpawnpoints()
        {
            FireSpawnpoint[] spawnpoints = Object.FindObjectsByType<FireSpawnpoint>(FindObjectsSortMode.None);
            
            foreach (var sp in spawnpoints)
            {
                // Draw a bright red wireframe box
                Handles.color = new Color(1f, 0.2f, 0.1f, 1f);
                Handles.DrawWireCube(sp.transform.position + Vector3.up * 0.5f, Vector3.one);
                
                // Draw an orange upward arrow indicating spread direction
                Handles.color = new Color(1f, 0.6f, 0f, 1f);
                Handles.DrawLine(sp.transform.position + Vector3.up * 1f, sp.transform.position + Vector3.up * 2f);
                Handles.DrawWireDisc(sp.transform.position + Vector3.up * 2f, Vector3.up, 0.2f);
            }
        }

        private void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Alpha1) { tool.currentMode = FireToolMode.Place; DestroyGhost(); e.Use(); }
            if (e.keyCode == KeyCode.Alpha2) { tool.currentMode = FireToolMode.Remove; DestroyGhost(); e.Use(); }

            if (e.keyCode == KeyCode.N) { tool.currentFloor--; e.Use(); }
            if (e.keyCode == KeyCode.M) { tool.currentFloor++; e.Use(); }
        }

        private void RunPlaceMode(Event e, SceneView sceneView)
        {
            // Calculate floor height
            float floorY = tool.currentFloor * tool.gridSize;
            Vector3 planeOrigin = tool.transform.TransformPoint(new Vector3(0, floorY, 0));
            Plane gridPlane = new Plane(Vector3.up, planeOrigin);
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (gridPlane.Raycast(ray, out float enter))
            {
                Vector3 hitWorld = ray.GetPoint(enter);
                Vector3 localHit = tool.transform.InverseTransformPoint(hitWorld);
                
                // Snap to grid
                Vector3Int gridCoord = new Vector3Int(
                    Mathf.RoundToInt(localHit.x / tool.gridSize),
                    tool.currentFloor,
                    Mathf.RoundToInt(localHit.z / tool.gridSize)
                );

                Vector3 localSnapped = new Vector3(gridCoord.x * tool.gridSize, gridCoord.y * tool.gridSize, gridCoord.z * tool.gridSize);
                Vector3 worldSnapped = tool.transform.TransformPoint(localSnapped);

                UpdateGhost(worldSnapped);

                // Placement action
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlaceFireSpawnpoint(worldSnapped);
                    e.Use();
                }

                sceneView.Repaint();
            }
            else
            {
                DestroyGhost();
            }
        }

        private FireSpawnpoint cachedHoverSpawnpoint;

        private void RunRemoveMode(Event e, SceneView sceneView)
        {
            DestroyGhost();

            if (e.type != EventType.Layout && e.type != EventType.Repaint)
            {
                GameObject pickedObj = HandleUtility.PickGameObject(e.mousePosition, false);
                if (pickedObj != null)
                {
                    cachedHoverSpawnpoint = pickedObj.GetComponentInParent<FireSpawnpoint>();
                }
                else
                {
                    cachedHoverSpawnpoint = null;
                }
            }

            if (cachedHoverSpawnpoint != null)
            {
                Handles.color = new Color(1f, 0f, 0f, 0.5f);
                Handles.DrawWireCube(cachedHoverSpawnpoint.transform.position + Vector3.up * 0.5f, Vector3.one * 1.1f);
                
                if (e.type == EventType.Repaint)
                {
                    sceneView.Repaint();
                }

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Undo.DestroyObjectImmediate(cachedHoverSpawnpoint.gameObject);
                    cachedHoverSpawnpoint = null;
                    e.Use();
                }
            }
        }

        private void UpdateGhost(Vector3 worldPos)
        {
            if (ghostObject == null)
            {
                ghostObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghostObject.name = "__GhostFire__";
                ghostObject.hideFlags = HideFlags.HideAndDontSave;
                DestroyImmediate(ghostObject.GetComponent<Collider>());
                
                // Give it a transparent red material
                Renderer r = ghostObject.GetComponent<Renderer>();
                Material mat = new Material(Shader.Find("Standard"));
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0f, 0f, 0.4f);
                r.material = mat;
            }

            // Position it properly (the spawn point will be bottom-center on the grid cell)
            ghostObject.transform.position = worldPos + Vector3.up * 0.5f;
            ghostObject.transform.localScale = Vector3.one;
        }

        private void DestroyGhost()
        {
            if (ghostObject != null)
            {
                DestroyImmediate(ghostObject);
            }
        }

        private void PlaceFireSpawnpoint(Vector3 worldPos)
        {
            GameObject fireObj = new GameObject("FireSpawnpoint");
            fireObj.transform.position = worldPos;
            
            Transform parent = tool.GetFloorParent(tool.currentFloor);
            fireObj.transform.SetParent(parent, true);

            FireSpawnpoint spawnpoint = fireObj.AddComponent<FireSpawnpoint>();

            // Make it easier to click on the object in Remove Mode by adding a dummy trigger collider
            BoxCollider col = fireObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 0.5f, 0);
            col.size = Vector3.one;

            Undo.RegisterCreatedObjectUndo(fireObj, "Place Fire Spawnpoint");
            EditorUtility.SetDirty(tool.gameObject);
        }
    }
}
