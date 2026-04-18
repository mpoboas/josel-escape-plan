using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the <see cref="StaticMapGenerator"/> wall board hierarchy and saves <c>Assets/Prefabs/WallMapBoard.prefab</c>.
/// Use when Unity MCP is not connected: menu <b>Tools → Building → Wall Map Board → Create Or Update Prefab</b>.
/// </summary>
public static class WallMapBoardPrefabCreator
{
    private const string PrefabPath = "Assets/Prefabs/WallMapBoard.prefab";

    [MenuItem("Tools/Building/Wall Map Board/Create Or Update Prefab")]
    public static void CreateOrUpdatePrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var root = new GameObject("WallMapBoard");
        try
        {
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localScale = new Vector3(1f, 1f, 0.06f);
            frame.transform.localPosition = Vector3.zero;
            ApplyLitColor(frame.GetComponent<MeshRenderer>(), new Color(0.28f, 0.28f, 0.32f));

            var camGo = new GameObject("MapCaptureCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 18f, 0f);
            camGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            cam.cullingMask = ~0;
            cam.depth = -10f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.enabled = false;
            camGo.SetActive(false);

            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGo.name = "YouAreHereMarker";
            markerGo.transform.SetParent(root.transform, false);
            markerGo.transform.localScale = Vector3.one * 0.12f;
            markerGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            Object.DestroyImmediate(markerGo.GetComponent<Collider>());
            ApplyUnlitColor(markerGo.GetComponent<MeshRenderer>(), new Color(0.9f, 0.12f, 0.1f));

            var canvasGo = new GameObject("MapCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0f, -0.034f);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = new Vector3(0.0016f, 0.0016f, 0.0016f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(900f, 900f);

            var rawGo = new GameObject("MapRawImage");
            rawGo.transform.SetParent(canvasGo.transform, false);
            var rawImage = rawGo.AddComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;

            var rawRt = rawImage.rectTransform;
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;
            rawRt.sizeDelta = Vector2.zero;
            rawRt.anchoredPosition = Vector2.zero;
            rawRt.localScale = Vector3.one;

            var gen = root.AddComponent<StaticMapGenerator>();
            gen.mapCamera = cam;
            gen.mapImage = rawImage;
            gen.youAreHereMarker = markerGo.transform;
            gen.mapCanvasRoot = canvasGo.transform;
            gen.generateOnStart = true;
            gen.captureResolution = 1024;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[WallMapBoardPrefabCreator] Saved prefab to " + PrefabPath +
                      ". Add an EventSystem to the scene if you need click-to-zoom in Play Mode.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ApplyLitColor(MeshRenderer r, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
        r.sharedMaterial = mat;
    }

    private static void ApplyUnlitColor(MeshRenderer r, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
        r.sharedMaterial = mat;
    }
}
