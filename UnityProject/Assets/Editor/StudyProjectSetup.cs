#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.Text;

public static class StudyProjectSetup
{
    private const string LocalGroup = "LocalContent";
    private const string RemoteGroup = "RemoteContent";

    private static TMP_FontAsset _chineseFont;

    [MenuItem("StudyUnity/Setup All Chapters")]
    public static void SetupAll()
    {
        _chineseFont = EnsureChineseFontAsset();   // ← add this line
        CreateDemoAssets();
        ConfigureAddressables();
        CreateChapter01Scene();
        CreateChapter02Scene();
        CreateChapter03Scenes();
        CreateChapter04Scene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StudyUnity] Setup complete! Open any chapter scene and press Play.");
    }

    static TMP_FontAsset EnsureChineseFontAsset()
    {
        const string assetPath = "Assets/_Shared/Fonts/ChineseDynamic.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;

        EnsureDir("Assets/_Shared/Fonts");

        var candidates = new[] {
            "/System/Library/Fonts/PingFang.ttc",
            "/Library/Fonts/Arial Unicode.ttf",
            "C:\\Windows\\Fonts\\msyh.ttc",
            "C:\\Windows\\Fonts\\simhei.ttf",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc"
        };

        string found = null;
        foreach (var c in candidates)
            if (File.Exists(c)) { found = c; break; }

        if (found == null)
        {
            Debug.LogWarning("[StudyUnity] No CJK font found on this machine. Chinese text will not render. See Docs/00_Overview.md for manual setup.");
            return null;
        }

        var dstName = "ChineseFont" + Path.GetExtension(found);
        var dstAssetPath = "Assets/_Shared/Fonts/" + dstName;
        var dstFull = Path.Combine(Application.dataPath, "_Shared/Fonts/" + dstName);

        if (!File.Exists(dstFull))
        {
            File.Copy(found, dstFull);
            AssetDatabase.ImportAsset(dstAssetPath);
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(dstAssetPath);
        if (font == null)
        {
            Debug.LogWarning($"[StudyUnity] Failed to import font at {dstAssetPath}. Try importing it manually.");
            return null;
        }

        fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, TMP_FontAsset.GlyphRenderMode.SDFAA, 512, 512, TMP_FontAsset.AtlasPopulationMode.Dynamic);
        fa.name = "ChineseDynamic";
        AssetDatabase.CreateAsset(fa, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StudyUnity] Chinese TMP font created (Dynamic) from: {found}");
        return fa;
    }

    // ── Demo Assets ──────────────────────────────────────────────

    static void CreateDemoAssets()
    {
        EnsureDir("Assets/_Chapters/Chapter01_BasicLoad/DemoAssets");
        EnsureDir("Assets/_Chapters/Chapter04_Remote/DemoAssets");

        MakeCubePrefab(
            "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab",
            "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCubeMat.mat",
            new Color(0.2f, 0.8f, 1f));

        MakeSpriteTex("Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoSprite.png");

        MakeCubePrefab(
            "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCube.prefab",
            "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCubeMat.mat",
            new Color(1f, 0.3f, 0.2f));
    }

    static void MakeCubePrefab(string prefabPath, string matPath, Color color)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return;
        var mat = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(mat, matPath);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
    }

    static void MakeSpriteTex(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
        var tex = new Texture2D(128, 128);
        var pixels = new Color[128 * 128];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.2f, 0.9f, 0.3f);
        tex.SetPixels(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.SaveAndReimport();
    }

    // ── Addressable Groups ────────────────────────────────────────

    static void ConfigureAddressables()
    {
        var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var local = GetOrCreateGroup(s, LocalGroup, remote: false);
        var remote = GetOrCreateGroup(s, RemoteGroup, remote: true);

        Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab", "DemoCube", local);
        Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoSprite.png", "DemoSprite", local);
        Tag(s, "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCube.prefab", "RemoteCube", remote);
    }

    static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings s, string name, bool remote)
    {
        var g = s.FindGroup(name);
        if (g != null) return g;
        g = s.CreateGroup(name, false, false, false, null,
            typeof(ContentUpdateGroupSchema),
            typeof(BundledAssetGroupSchema));
        if (remote)
        {
            var schema = g.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(s, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(s, AddressableAssetSettings.kRemoteLoadPath);
        }
        return g;
    }

    static void Tag(AddressableAssetSettings s, string assetPath, string address, AddressableAssetGroup group)
    {
        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) { Debug.LogWarning($"[StudyUnity] Asset not found: {assetPath}"); return; }
        var entry = s.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
    }

    // ── Scene Helpers ─────────────────────────────────────────────

    static (UnityEngine.SceneManagement.Scene scene, GameObject canvas) NewScene(string savePath)
    {
        EnsureDir(Path.GetDirectoryName(savePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
        cam.orthographic = false;
        camGo.transform.position = new Vector3(0, 1, -10);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        var evSys = new GameObject("EventSystem");
        evSys.AddComponent<EventSystem>();
        evSys.AddComponent<StandaloneInputModule>();

        var canvasGo = new GameObject("Canvas");
        var cv = canvasGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        return (scene, canvasGo);
    }

    static GameObject Btn(GameObject parent, string label, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -4);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.45f, 0.78f);
        go.AddComponent<Button>();
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = 22;
        if (_chineseFont != null) tmp.font = _chineseFont;
        return go;
    }

    static GameObject Txt(GameObject parent, string name, string text, Vector2 aMin, Vector2 aMax, int fontSize = 26)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(10, 4); rt.offsetMax = new Vector2(-10, -4);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = Color.white;
        if (_chineseFont != null) tmp.font = _chineseFont;
        return go;
    }

    static void AddLogPanel(GameObject canvas)
    {
        var panel = new GameObject("LogPanel");
        panel.transform.SetParent(canvas.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0, 0);
        panelRt.anchorMax = new Vector2(1, 0.32f);
        panelRt.offsetMin = new Vector2(10, 10);
        panelRt.offsetMax = new Vector2(-10, -5);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.75f);

        var scrollGo = new GameObject("ScrollRect");
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(6, 6); scrollRt.offsetMax = new Vector2(-6, -6);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0, 0);
        var logTmp = contentGo.AddComponent<TextMeshProUGUI>();
        logTmp.text = "日志输出...";
        logTmp.fontSize = 18; logTmp.color = new Color(0.3f, 1f, 0.4f);
        if (_chineseFont != null) logTmp.font = _chineseFont;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRt;
        panel.AddComponent<DebugLogPanel>();
    }

    // ── Chapter 01 ────────────────────────────────────────────────

    static void CreateChapter01Scene()
    {
        var (scene, canvas) = NewScene("Assets/_Chapters/Chapter01_BasicLoad/Chapter01Scene.unity");

        Txt(canvas, "Title", "Chapter 01 — 基础加载", new Vector2(0, 0.92f), Vector2.one, 32);
        Btn(canvas, "加载 Cube Prefab", new Vector2(0, 0.78f), new Vector2(0.5f, 0.92f));
        Btn(canvas, "加载 Sprite", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
        Btn(canvas, "清理场景", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
        Btn(canvas, "清空日志", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));

        var imgGo = new GameObject("SpriteDisplay");
        imgGo.transform.SetParent(canvas.transform, false);
        var imgRt = imgGo.AddComponent<RectTransform>();
        imgRt.anchorMin = new Vector2(0.3f, 0.36f); imgRt.anchorMax = new Vector2(0.7f, 0.64f);
        var img = imgGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f);

        AddLogPanel(canvas);

        new GameObject("Chapter01Manager").AddComponent<Chapter01Manager>();
        EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter01_BasicLoad/Chapter01Scene.unity");
    }

    // ── Chapter 02 ────────────────────────────────────────────────

    static void CreateChapter02Scene()
    {
        var (scene, canvas) = NewScene("Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Scene.unity");

        Txt(canvas, "Title", "Chapter 02 — Handle 与内存管理", new Vector2(0, 0.92f), Vector2.one, 32);
        Btn(canvas, "加载 (LoadAssetAsync)", new Vector2(0, 0.78f), new Vector2(0.5f, 0.92f));
        Btn(canvas, "实例化 (Instantiate)", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
        Btn(canvas, "释放 Handle (Release)", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
        Btn(canvas, "销毁实例 (Destroy)", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
        Btn(canvas, "清空日志", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.64f));
        Txt(canvas, "StatusText", "Handle 数: 0    实例数: 0", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 24);

        AddLogPanel(canvas);

        new GameObject("Chapter02Manager").AddComponent<Chapter02Manager>();
        EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Scene.unity");
    }

    // ── Chapter 03 ────────────────────────────────────────────────

    static void CreateChapter03Scenes()
    {
        MakeSubScene("Assets/_Chapters/Chapter03_SceneLoad/SubSceneA.unity", "Sub Scene A", new Color(0.1f, 0.1f, 0.5f));
        MakeSubScene("Assets/_Chapters/Chapter03_SceneLoad/SubSceneB.unity", "Sub Scene B", new Color(0.4f, 0.1f, 0.4f));

        var (scene, canvas) = NewScene("Assets/_Chapters/Chapter03_SceneLoad/Chapter03MainScene.unity");

        Txt(canvas, "Title", "Chapter 03 — 场景加载", new Vector2(0, 0.92f), Vector2.one, 32);
        Btn(canvas, "Additive 加载 SubSceneA", new Vector2(0f, 0.78f), new Vector2(0.5f, 0.92f));
        Btn(canvas, "Additive 加载 SubSceneB", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
        Btn(canvas, "Single 加载 SubSceneA", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
        Btn(canvas, "卸载 SubSceneA", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
        Btn(canvas, "卸载 SubSceneB", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.64f));
        Btn(canvas, "清空日志", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.64f));
        Txt(canvas, "SceneListText", "当前已加载子场景: （无）", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 22);

        AddLogPanel(canvas);

        new GameObject("Chapter03Manager").AddComponent<Chapter03Manager>();
        EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter03_SceneLoad/Chapter03MainScene.unity");

        var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var local = s.FindGroup(LocalGroup) ?? GetOrCreateGroup(s, LocalGroup, remote: false);
        Tag(s, "Assets/_Chapters/Chapter03_SceneLoad/SubSceneA.unity", "SubSceneA", local);
        Tag(s, "Assets/_Chapters/Chapter03_SceneLoad/SubSceneB.unity", "SubSceneB", local);
    }

    static void MakeSubScene(string path, string label, Color bgColor)
    {
        EnsureDir(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var floorGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floorGo.name = "Floor";
        var matPath = path.Replace(".unity", "_Mat.mat");
        var mat = new Material(Shader.Find("Standard")) { color = bgColor };
        AssetDatabase.CreateAsset(mat, matPath);
        floorGo.GetComponent<Renderer>().sharedMaterial = mat;

        var labelGo = new GameObject("Label");
        labelGo.transform.position = new Vector3(0, 1.5f, 0);
        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = label; tmp.fontSize = 6; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

        EditorSceneManager.SaveScene(scene, path);
    }

    // ── Chapter 04 ────────────────────────────────────────────────

    static void CreateChapter04Scene()
    {
        var (scene, canvas) = NewScene("Assets/_Chapters/Chapter04_Remote/Chapter04Scene.unity");

        Txt(canvas, "Title", "Chapter 04 — Remote 加载 & Catalog 热更新", new Vector2(0, 0.92f), Vector2.one, 28);
        Btn(canvas, "检查 Catalog 更新", new Vector2(0f, 0.78f), new Vector2(0.5f, 0.92f));
        Btn(canvas, "更新 Catalog", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
        Btn(canvas, "加载 Remote 资源", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
        Btn(canvas, "清理", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
        Btn(canvas, "清空日志", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.64f));

        AddLogPanel(canvas);

        new GameObject("Chapter04Manager").AddComponent<Chapter04Manager>();
        EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter04_Remote/Chapter04Scene.unity");
    }

    // ── Utilities ─────────────────────────────────────────────────

    static void EnsureDir(string path)
    {
        if (string.IsNullOrEmpty(path) || Directory.Exists(path)) return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
#endif
