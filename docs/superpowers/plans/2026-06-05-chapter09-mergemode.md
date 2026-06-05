# Chapter 09 多 Key + MergeMode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 Chapter 09 场景，用 3 个颜色不同的本地 Prefab 演示 MergeMode.Union（并集）与 MergeMode.Intersection（交集）的区别。

**Architecture:** 新建 3 个 Prefab 放入 LocalContent 组，打 SetA / SetB 两个 Label；Alpha(红)=SetA，Beta(黄)=SetA+SetB，Gamma(蓝)=SetB；四个按钮各自展示不同加载结果，每次点击自动清理上次实例。

**Tech Stack:** Unity 2022 LTS, Addressables 1.22.x, TextMeshPro, C#

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `UnityProject/Assets/Editor/StudyProjectSetup.cs` | 修改 | 新增 Ch09 资产创建、Addressables 标签配置、场景搭建 |
| `UnityProject/Assets/_Chapters/Chapter09_MergeMode/Chapter09Manager.cs` | 新建 | 四个加载按钮逻辑，MergeMode 演示 |

---

### Task 1：StudyProjectSetup — Ch09 资产 + Addressables 配置

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中插入 `CreateChapter09Assets()` 调用**

找到 `CreateChapter05Assets();` 这一行，在其后添加：

```csharp
CreateChapter05Assets();
CreateChapter09Assets();      // ← 新增，必须在 ConfigureAddressables 之前
```

- [ ] **Step 2: 新增 `CreateChapter09Assets()` 方法**

在 `CreateChapter05Assets()` 方法之后添加：

```csharp
static void CreateChapter09Assets()
{
    const string dir = "Assets/_Chapters/Chapter09_MergeMode/DemoAssets";
    EnsureDir(dir);
    MakeCubePrefab(dir + "/Ch09CubeAlpha.prefab", dir + "/Ch09CubeAlphaMat.mat", new Color(0.9f, 0.2f, 0.2f)); // 红
    MakeCubePrefab(dir + "/Ch09CubeBeta.prefab",  dir + "/Ch09CubeBetaMat.mat",  new Color(1f,   0.85f, 0f));  // 黄
    MakeCubePrefab(dir + "/Ch09CubeGamma.prefab", dir + "/Ch09CubeGammaMat.mat", new Color(0.2f, 0.4f,  0.9f)); // 蓝
}
```

- [ ] **Step 3: 更新 `ConfigureAddressables()`，添加 SetA / SetB Label 和 Ch09 资产**

找到 `ConfigureAddressables()` 方法，在末尾 `}` 前添加：

```csharp
    s.AddLabel("SetA");
    s.AddLabel("SetB");
    TagAndLabel(s, "Assets/_Chapters/Chapter09_MergeMode/DemoAssets/Ch09CubeAlpha.prefab", "Ch09CubeAlpha", local, "SetA");
    TagAndLabel(s, "Assets/_Chapters/Chapter09_MergeMode/DemoAssets/Ch09CubeBeta.prefab",  "Ch09CubeBeta",  local, "SetA");
    TagAndLabel(s, "Assets/_Chapters/Chapter09_MergeMode/DemoAssets/Ch09CubeBeta.prefab",  "Ch09CubeBeta",  local, "SetB");
    TagAndLabel(s, "Assets/_Chapters/Chapter09_MergeMode/DemoAssets/Ch09CubeGamma.prefab", "Ch09CubeGamma", local, "SetB");
```

> **注意**：Ch09CubeBeta 需要调用两次 `TagAndLabel` 以同时打上 SetA 和 SetB 两个 Label。`TagAndLabel` 内部调用 `entry.SetLabel(label, true, true)`，追加不覆盖，两次调用安全。

- [ ] **Step 4: 运行 Setup 验证资产**

菜单 **StudyUnity > Setup All Chapters**

预期：Console 输出 `[StudyUnity] Setup complete!`，`Assets/_Chapters/Chapter09_MergeMode/DemoAssets/` 下出现 3 个 Prefab。

打开 **Addressables Groups**，在 LocalContent 组中确认：
- Ch09CubeAlpha → Labels 列显示 `SetA`
- Ch09CubeBeta  → Labels 列显示 `SetA SetB`
- Ch09CubeGamma → Labels 列显示 `SetB`

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Editor/StudyProjectSetup.cs
git commit -m "feat: StudyProjectSetup add Ch09 assets and SetA/SetB label config"
```

---

### Task 2：新建 Chapter09Manager.cs

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter09_MergeMode/Chapter09Manager.cs`

- [ ] **Step 1: 创建文件，写入完整实现**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class Chapter09Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private AsyncOperationHandle<IList<GameObject>> _activeHandle;
    private readonly List<GameObject> _instances = new List<GameObject>();

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        WireButton("加载 SetA",              () => LoadByKey("SetA"));
        WireButton("加载 SetB",              () => LoadByKey("SetB"));
        WireButton("Union(SetA|SetB)",       OnUnionClick);
        WireButton("Intersection(SetA∩SetB)", OnIntersectionClick);
        WireButton("清理",                   OnClearClick);
        WireButton("清空日志",                () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void LoadByKey(string key)
    {
        ClearInstances();
        _log.Log($"LoadAssetsAsync(\"{key}\") 开始...");
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(key, null);
        _activeHandle.Completed += h => OnLoaded(h, key);
    }

    private void OnUnionClick()
    {
        ClearInstances();
        _log.Log("LoadAssetsAsync(Union: SetA | SetB) 开始...");
        var keys = new List<object> { "SetA", "SetB" };
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(
            keys, null, Addressables.MergeMode.Union);
        _activeHandle.Completed += h => OnLoaded(h, "Union");
    }

    private void OnIntersectionClick()
    {
        ClearInstances();
        _log.Log("LoadAssetsAsync(Intersection: SetA ∩ SetB) 开始...");
        var keys = new List<object> { "SetA", "SetB" };
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(
            keys, null, Addressables.MergeMode.Intersection);
        _activeHandle.Completed += h => OnLoaded(h, "Intersection");
    }

    private void OnLoaded(AsyncOperationHandle<IList<GameObject>> h, string label)
    {
        if (h.Status != AsyncOperationStatus.Succeeded)
        {
            _log.Log($"{label} 加载失败 ✗ {h.OperationException?.Message}");
            Addressables.Release(_activeHandle);
            _activeHandle = default;
            return;
        }
        float spacing  = 2.5f;
        float startX   = -(h.Result.Count - 1) * spacing / 2f;
        for (int i = 0; i < h.Result.Count; i++)
            _instances.Add(Instantiate(h.Result[i],
                new Vector3(startX + i * spacing, 0, 0), Quaternion.identity));
        _log.Log($"{label} 加载完成 ✓  共 {h.Result.Count} 个资产");
    }

    private void ClearInstances()
    {
        foreach (var inst in _instances) Destroy(inst);
        _instances.Clear();
        if (_activeHandle.IsValid()) Addressables.Release(_activeHandle);
        _activeHandle = default;
    }

    private void OnClearClick()
    {
        ClearInstances();
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_activeHandle.IsValid()) Addressables.Release(_activeHandle);
    }
}
```

- [ ] **Step 2: 确认 Unity 编译无报错**

等待 Unity 编译完成，Console 无红色报错。

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/_Chapters/Chapter09_MergeMode/Chapter09Manager.cs
git commit -m "feat: add Chapter09Manager for MergeMode Union/Intersection demo"
```

---

### Task 3：StudyProjectSetup — Ch09 场景

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中添加 `CreateChapter09Scene()` 调用**

找到 `CreateChapter07Scene();` 这一行，在其后添加：

```csharp
CreateChapter07Scene();
CreateChapter09Scene();   // ← 新增
```

- [ ] **Step 2: 新增 `CreateChapter09Scene()` 方法**

在 `CreateChapter07Scene()` 方法之后添加：

```csharp
// ── Chapter 09 ────────────────────────────────────────────────

static void CreateChapter09Scene()
{
    var (scene, canvas) = NewScene("Assets/_Chapters/Chapter09_MergeMode/Chapter09Scene.unity");

    Txt(canvas, "Title", "Chapter 09 — 多 Key + MergeMode", new Vector2(0, 0.92f), Vector2.one, 30);
    Btn(canvas, "加载 SetA",               new Vector2(0f,   0.78f), new Vector2(0.5f, 0.92f));
    Btn(canvas, "加载 SetB",               new Vector2(0.5f, 0.78f), new Vector2(1f,   0.92f));
    Btn(canvas, "Union(SetA|SetB)",        new Vector2(0f,   0.64f), new Vector2(0.5f, 0.78f));
    Btn(canvas, "Intersection(SetA∩SetB)", new Vector2(0.5f, 0.64f), new Vector2(1f,   0.78f));
    Btn(canvas, "清理",                    new Vector2(0f,   0.5f),  new Vector2(0.5f, 0.64f));
    Btn(canvas, "清空日志",                 new Vector2(0.5f, 0.5f),  new Vector2(1f,   0.64f));

    AddLogPanel(canvas);

    new GameObject("Chapter09Manager").AddComponent<Chapter09Manager>();
    EditorSceneManager.SaveScene(scene,
        "Assets/_Chapters/Chapter09_MergeMode/Chapter09Scene.unity");
}
```

- [ ] **Step 3: 运行 Setup**

菜单 **StudyUnity > Setup All Chapters**

预期：Console 输出 `[StudyUnity] Setup complete!`，`Chapter09Scene.unity` 出现在 `Assets/_Chapters/Chapter09_MergeMode/` 下。

- [ ] **Step 4: Commit**

```bash
git add UnityProject/Assets/Editor/StudyProjectSetup.cs
git add UnityProject/Assets/_Chapters/Chapter09_MergeMode/
git commit -m "feat: Chapter09 MergeMode scene and setup"
```

---

### Task 4：Play Mode 验证

**Files:** 无代码改动

- [ ] **Step 1: 进入 Play 模式，打开 Chapter09Scene**

Play Mode Script 保持 **Use Asset Database**（Ch09 用本地资产）。

- [ ] **Step 2: 验证 SetA（预期 2 个 Cube）**

点「加载 SetA」→ 出现 2 个 Cube：红色(Alpha) + 黄色(Beta)，日志显示 `SetA 加载完成 ✓  共 2 个资产`。

- [ ] **Step 3: 验证 SetB（预期 2 个 Cube）**

点「加载 SetB」→ 上次实例自动消失，出现 2 个 Cube：黄色(Beta) + 蓝色(Gamma)，日志显示 `SetB 加载完成 ✓  共 2 个资产`。

- [ ] **Step 4: 验证 Union（预期 3 个 Cube）**

点「Union(SetA|SetB)」→ 出现 3 个 Cube：红 + 黄 + 蓝，日志显示 `Union 加载完成 ✓  共 3 个资产`。

- [ ] **Step 5: 验证 Intersection（预期 1 个 Cube）**

点「Intersection(SetA∩SetB)」→ 只出现 1 个黄色 Cube(Beta)，日志显示 `Intersection 加载完成 ✓  共 1 个资产`。
