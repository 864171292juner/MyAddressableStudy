# Chapter 06 AssetReference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 Chapter 06 场景，对比字符串地址加载与 AssetReference 拖拽引用两种方式。

**Architecture:** 复用 Ch01 的 DemoCube 资产（不新建资产）；Chapter06Manager 持有一个 `[SerializeField] AssetReferenceGameObject _cubeRef` 字段；StudyProjectSetup 通过 `SerializedObject` 自动把 DemoCube GUID 写入该字段，无需手动拖拽。

**Tech Stack:** Unity 2022 LTS, Addressables 1.22.x, TextMeshPro, C#

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `UnityProject/Assets/_Chapters/Chapter06_AssetReference/Chapter06Manager.cs` | 新建 | 字符串加载 vs AssetReference 加载的按钮逻辑 |
| `UnityProject/Assets/Editor/StudyProjectSetup.cs` | 修改 | 新增场景搭建 + `SetAssetReferenceField` 工具方法 |

---

### Task 1：新建 Chapter06Manager.cs

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter06_AssetReference/Chapter06Manager.cs`

- [ ] **Step 1: 创建目录并写入完整实现**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter06Manager : MonoBehaviour
{
    [SerializeField] AssetReferenceGameObject _cubeRef;

    private DebugLogPanel _log;
    private AsyncOperationHandle<GameObject> _stringHandle;
    private AsyncOperationHandle<GameObject> _refHandle;
    private GameObject _stringInstance;
    private GameObject _refInstance;

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        WireButton("字符串加载",    OnStringLoadClick);
        WireButton("AssetRef 加载", OnRefLoadClick);
        WireButton("清理",          OnClearClick);
        WireButton("清空日志",       () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void OnStringLoadClick()
    {
        if (_stringHandle.IsValid()) { _log.Log("字符串方式已加载，请先「清理」"); return; }
        _log.Log("Addressables.LoadAssetAsync<GameObject>(\"DemoCube\") 开始...");
        _stringHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        _stringHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _stringInstance = Instantiate(h.Result, new Vector3(-2f, 0, 0), Quaternion.identity);
                _log.Log($"字符串加载完成 ✓  {h.Result.name}  (左侧)");
            }
            else
            {
                _log.Log($"字符串加载失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private void OnRefLoadClick()
    {
        if (_refHandle.IsValid()) { _log.Log("AssetReference 方式已加载，请先「清理」"); return; }
        if (!_cubeRef.RuntimeKeyIsValid()) { _log.Log("_cubeRef 未赋值，Inspector 检查"); return; }
        _log.Log("_cubeRef.LoadAssetAsync<GameObject>() 开始...");
        _refHandle = _cubeRef.LoadAssetAsync<GameObject>();
        _refHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _refInstance = Instantiate(h.Result, new Vector3(2f, 0, 0), Quaternion.identity);
                _log.Log($"AssetRef 加载完成 ✓  {h.Result.name}  (右侧)");
            }
            else
            {
                _log.Log($"AssetRef 加载失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private void OnClearClick()
    {
        if (_stringInstance != null) { Destroy(_stringInstance); _stringInstance = null; }
        if (_refInstance    != null) { Destroy(_refInstance);    _refInstance    = null; }
        if (_stringHandle.IsValid()) Addressables.Release(_stringHandle);
        if (_refHandle.IsValid())    _cubeRef.ReleaseAsset();
        _stringHandle = default;
        _refHandle    = default;
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_stringHandle.IsValid()) Addressables.Release(_stringHandle);
        if (_refHandle.IsValid())    _cubeRef.ReleaseAsset();
    }
}
```

- [ ] **Step 2: 确认 Unity 编译无报错**

等待 Unity 编译完成，Console 无红色报错。

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/_Chapters/Chapter06_AssetReference/Chapter06Manager.cs
git commit -m "feat: add Chapter06Manager for AssetReference vs string address demo"
```

---

### Task 2：StudyProjectSetup — Ch06 场景

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中添加 `CreateChapter06Scene()` 调用**

找到 `SetupAll()`，在 `CreateChapter05Scene();` 后添加一行：

```csharp
CreateChapter05Scene();
CreateChapter06Scene();   // ← 新增
```

- [ ] **Step 2: 新增 `SetAssetReferenceField()` 工具方法**

在 `TagAndLabel()` 方法之后添加：

```csharp
static void SetAssetReferenceField(Object component, string fieldName, string assetPath)
{
    var guid = AssetDatabase.AssetPathToGUID(assetPath);
    if (string.IsNullOrEmpty(guid)) { Debug.LogWarning($"[StudyUnity] Asset not found: {assetPath}"); return; }
    var so = new SerializedObject(component);
    var prop = so.FindProperty(fieldName);
    if (prop == null) { Debug.LogWarning($"[StudyUnity] Field not found: {fieldName}"); return; }
    prop.FindPropertyRelative("m_AssetGUID").stringValue = guid;
    so.ApplyModifiedProperties();
}
```

- [ ] **Step 3: 新增 `CreateChapter06Scene()` 方法**

在 `CreateChapter05Scene()` 方法之后添加（`// ── Utilities` 注释行之前）：

```csharp
// ── Chapter 06 ────────────────────────────────────────────────

static void CreateChapter06Scene()
{
    var (scene, canvas) = NewScene("Assets/_Chapters/Chapter06_AssetReference/Chapter06Scene.unity");

    Txt(canvas, "Title", "Chapter 06 — AssetReference", new Vector2(0, 0.92f), Vector2.one, 32);
    Btn(canvas, "字符串加载",     new Vector2(0f,   0.78f), new Vector2(0.5f, 0.92f));
    Btn(canvas, "AssetRef 加载",  new Vector2(0.5f, 0.78f), new Vector2(1f,   0.92f));
    Btn(canvas, "清理",           new Vector2(0f,   0.64f), new Vector2(0.5f, 0.78f));
    Btn(canvas, "清空日志",        new Vector2(0.5f, 0.64f), new Vector2(1f,   0.78f));

    AddLogPanel(canvas);

    var managerGo = new GameObject("Chapter06Manager");
    var manager = managerGo.AddComponent<Chapter06Manager>();
    SetAssetReferenceField(manager, "_cubeRef",
        "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab");

    EditorSceneManager.SaveScene(scene,
        "Assets/_Chapters/Chapter06_AssetReference/Chapter06Scene.unity");
}
```

- [ ] **Step 4: 运行 Setup**

菜单 **StudyUnity > Setup All Chapters**

预期：Console 输出 `[StudyUnity] Setup complete!`，无报错，`Chapter06Scene.unity` 出现在 `Assets/_Chapters/Chapter06_AssetReference/` 下。

- [ ] **Step 5: 验证 Inspector 赋值**

在 Project 窗口打开 `Chapter06Scene.unity`，选中 `Chapter06Manager` GameObject，Inspector 中 `Cube Ref` 字段应显示 `DemoCube`（不是 None）。

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/Editor/StudyProjectSetup.cs
git add UnityProject/Assets/_Chapters/Chapter06_AssetReference/
git commit -m "feat: Chapter06 AssetReference scene and setup"
```

---

### Task 3：Play Mode 验证

**Files:** 无代码改动

- [ ] **Step 1: 进入 Play 模式，打开 Chapter06Scene**

Play Mode Script 保持 **Use Asset Database**（Ch06 用本地资产，无需 CDN）。

- [ ] **Step 2: 验证字符串加载**

点「字符串加载」→ 日志显示 `字符串加载完成 ✓`，场景左侧（x=-2）出现蓝色 Cube。

- [ ] **Step 3: 验证 AssetReference 加载**

点「AssetRef 加载」→ 日志显示 `AssetRef 加载完成 ✓`，场景右侧（x=+2）出现蓝色 Cube。

两个 Cube 同时存在（加载了同一资产的两个实例）。

- [ ] **Step 4: 验证清理**

点「清理」→ 两个 Cube 消失，日志显示 `Handle 已释放`。
