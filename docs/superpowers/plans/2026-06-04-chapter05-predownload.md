# Chapter 05 预下载流程 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 Chapter 05 场景，演示 GetDownloadSizeAsync / DownloadDependenciesAsync / LoadAssetsAsync 三个 API，每个 API 对应一个按钮，带实时进度显示。

**Architecture:** 新建 3 个远端 Prefab 放入 `Chapter05Remote` Group，统一打 `Chapter05` Label；Chapter05Manager 按钮驱动，协程轮询 PercentComplete 更新进度文字；StudyProjectSetup 负责资产创建、Addressables 配置、场景搭建。

**Tech Stack:** Unity 2022 LTS, Addressables 1.22.x, TextMeshPro, C#

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `UnityProject/Assets/Editor/StudyProjectSetup.cs` | 修改 | 新增 Ch05 资产创建、Addressables 配置、场景搭建 |
| `UnityProject/Assets/_Chapters/Chapter05_PreDownload/Chapter05Manager.cs` | 新建 | 按钮逻辑、预下载进度、资产加载 |

---

### Task 1：StudyProjectSetup — Ch05 资产 + Addressables 配置

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中插入 `CreateChapter05Assets()` 调用**

找到 `SetupAll()` 方法，**只添加 `CreateChapter05Assets()` 这一行**（`CreateChapter05Scene()` 在 Task 3 添加，现在还不加）：

```csharp
[MenuItem("StudyUnity/Setup All Chapters")]
public static void SetupAll()
{
    _chineseFont = EnsureChineseFontAsset();
    CreateDemoAssets();
    CreateChapter05Assets();      // ← 新增，必须在 ConfigureAddressables 之前
    ConfigureAddressables();
    CreateChapter01Scene();
    CreateChapter02Scene();
    CreateChapter03Scenes();
    CreateChapter04Scene();
    // CreateChapter05Scene() 在 Task 3 添加
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
    Debug.Log("[StudyUnity] Setup complete! Open any chapter scene and press Play.");
}
```

- [ ] **Step 2: 新增 `CreateChapter05Assets()` 方法**

在 `CreateDemoAssets()` 方法之后添加：

```csharp
static void CreateChapter05Assets()
{
    EnsureDir("Assets/_Chapters/Chapter05_PreDownload/DemoAssets");
    MakeCubePrefab(
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeA.prefab",
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeAMat.mat",
        new Color(1f, 0.5f, 0f));   // 橙色
    MakeCubePrefab(
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeB.prefab",
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeBMat.mat",
        new Color(0.6f, 0.2f, 0.8f)); // 紫色
    MakeCubePrefab(
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeC.prefab",
        "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeCMat.mat",
        new Color(0.2f, 0.8f, 0.3f)); // 绿色
}
```

- [ ] **Step 3: 更新 `ConfigureAddressables()`，添加 Ch05 分组和 Label**

将 `ConfigureAddressables()` 全部替换为：

```csharp
static void ConfigureAddressables()
{
    var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
    var local  = GetOrCreateGroup(s, LocalGroup,  remote: false);
    var remote = GetOrCreateGroup(s, RemoteGroup, remote: true);
    var ch05   = GetOrCreateGroup(s, "Chapter05Remote", remote: true);

    Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab",  "DemoCube",   local);
    Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoSprite.png",   "DemoSprite", local);
    Tag(s, "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCube.prefab",   "RemoteCube", remote);

    s.AddLabel("Chapter05"); // 已存在时无副作用
    TagAndLabel(s, "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeA.prefab", "Ch05CubeA", ch05, "Chapter05");
    TagAndLabel(s, "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeB.prefab", "Ch05CubeB", ch05, "Chapter05");
    TagAndLabel(s, "Assets/_Chapters/Chapter05_PreDownload/DemoAssets/Ch05CubeC.prefab", "Ch05CubeC", ch05, "Chapter05");
}
```

- [ ] **Step 4: 新增 `TagAndLabel()` 工具方法**

在 `Tag()` 方法之后添加：

```csharp
static void TagAndLabel(AddressableAssetSettings s, string assetPath, string address,
    AddressableAssetGroup group, string label)
{
    var guid = AssetDatabase.AssetPathToGUID(assetPath);
    if (string.IsNullOrEmpty(guid)) { Debug.LogWarning($"[StudyUnity] Asset not found: {assetPath}"); return; }
    var entry = s.CreateOrMoveEntry(guid, group, false, false);
    entry.address = address;
    entry.SetLabel(label, true, true);
}
```

- [ ] **Step 5: 在 Unity 编辑器中验证**

菜单 **StudyUnity > Setup All Chapters** 运行。

预期：Console 输出 `[StudyUnity] Setup complete!`，`Assets/_Chapters/Chapter05_PreDownload/DemoAssets/` 下出现 3 个 Prefab。

---

### Task 2：新建 Chapter05Manager.cs

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter05_PreDownload/Chapter05Manager.cs`

- [ ] **Step 1: 创建文件，写入完整实现**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter05Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private TextMeshProUGUI _progressText;
    private AsyncOperationHandle _downloadHandle;
    private AsyncOperationHandle<IList<GameObject>> _loadHandle;
    private readonly List<GameObject> _instances = new List<GameObject>();

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        _progressText = GameObject.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();

        WireButton("检查下载大小",     OnCheckSizeClick);
        WireButton("预下载",          OnPreDownloadClick);
        WireButton("加载资产",         OnLoadAssetsClick);
        WireButton("清理",            OnClearClick);
        WireButton("清除 Bundle 缓存", OnClearBundleCacheClick);
        WireButton("清空日志",         () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void OnCheckSizeClick()
    {
        _log.Log("GetDownloadSizeAsync(\"Chapter05\") 开始...");
        Addressables.GetDownloadSizeAsync("Chapter05").Completed += handle =>
        {
            long size = handle.Result;
            _log.Log(size == 0
                ? "所有资产已缓存，无需下载 ✓"
                : $"需要下载 {size} bytes，点「预下载」开始");
            Addressables.Release(handle);
        };
    }

    private void OnPreDownloadClick()
    {
        if (_downloadHandle.IsValid()) { _log.Log("正在下载中，请稍候..."); return; }
        _log.Log("DownloadDependenciesAsync(\"Chapter05\") 开始...");
        _downloadHandle = Addressables.DownloadDependenciesAsync("Chapter05");
        StartCoroutine(TrackProgress(_downloadHandle));
        _downloadHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
                _log.Log("预下载完成 ✓ 可以点「加载资产」");
            else
                _log.Log($"下载失败 ✗ {h.OperationException?.Message}");
            Addressables.Release(_downloadHandle);
            _downloadHandle = default;
        };
    }

    private IEnumerator TrackProgress(AsyncOperationHandle handle)
    {
        while (!handle.IsDone)
        {
            if (_progressText != null)
                _progressText.text = $"下载进度: {handle.PercentComplete * 100:F0}%";
            yield return null;
        }
        if (_progressText != null)
            _progressText.text = "下载进度: 100%";
    }

    private void OnLoadAssetsClick()
    {
        if (_loadHandle.IsValid()) { _log.Log("资产已加载，请先「清理」再重新加载"); return; }
        _log.Log("LoadAssetsAsync<GameObject>(\"Chapter05\") 开始...");
        _loadHandle = Addressables.LoadAssetsAsync<GameObject>("Chapter05", null);
        _loadHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                for (int i = 0; i < h.Result.Count; i++)
                    _instances.Add(Instantiate(h.Result[i],
                        new Vector3((i - 1) * 2.5f, 0, 0), Quaternion.identity));
                _log.Log($"加载完成 ✓ 共 {h.Result.Count} 个资产");
            }
            else
            {
                _log.Log($"加载失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private void OnClearClick()
    {
        foreach (var inst in _instances) Destroy(inst);
        _instances.Clear();
        if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnClearBundleCacheClick()
    {
        bool cleared = Caching.ClearCache();
        _log.Log(cleared
            ? "Bundle 磁盘缓存已清除 ✓ 下次加载将从 CDN 重新下载"
            : "清除失败 — 可能有 Bundle 正在被使用，先「清理」再试");
    }

    private void OnDestroy()
    {
        if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
    }
}
```

- [ ] **Step 2: 确认 Unity 编译无报错**

保存文件后等待 Unity 编译完成，Console 无红色报错。

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/_Chapters/Chapter05_PreDownload/Chapter05Manager.cs
git commit -m "feat: add Chapter05Manager for pre-download flow"
```

---

### Task 3：StudyProjectSetup — Ch05 场景

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中添加 `CreateChapter05Scene()` 调用**

找到 Task 1 修改过的 `SetupAll()`，把注释行替换为真实调用：

```csharp
CreateChapter04Scene();
CreateChapter05Scene();   // ← 取消注释，改为正式调用
```

- [ ] **Step 2: 新增 `CreateChapter05Scene()` 方法**

在 `CreateChapter04Scene()` 方法之后添加：

```csharp
static void CreateChapter05Scene()
{
    var (scene, canvas) = NewScene("Assets/_Chapters/Chapter05_PreDownload/Chapter05Scene.unity");

    Txt(canvas, "Title", "Chapter 05 — 预下载流程", new Vector2(0, 0.92f), Vector2.one, 32);
    Btn(canvas, "检查下载大小",     new Vector2(0f,   0.78f), new Vector2(0.5f, 0.92f));
    Btn(canvas, "预下载",          new Vector2(0.5f,  0.78f), new Vector2(1f,   0.92f));
    Btn(canvas, "加载资产",         new Vector2(0f,   0.64f), new Vector2(0.5f, 0.78f));
    Btn(canvas, "清理",            new Vector2(0.5f,  0.64f), new Vector2(1f,   0.78f));
    Btn(canvas, "清除 Bundle 缓存", new Vector2(0f,   0.5f),  new Vector2(0.5f, 0.64f));
    Btn(canvas, "清空日志",         new Vector2(0.5f,  0.5f),  new Vector2(1f,   0.64f));
    Txt(canvas, "ProgressText", "下载进度: --", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 24);

    AddLogPanel(canvas);

    new GameObject("Chapter05Manager").AddComponent<Chapter05Manager>();
    EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter05_PreDownload/Chapter05Scene.unity");
}
```

- [ ] **Step 3: 运行 Setup**

菜单 **StudyUnity > Setup All Chapters**

预期：Console 输出 `[StudyUnity] Setup complete!`，无报错，`Chapter05Scene.unity` 出现在 `Assets/_Chapters/Chapter05_PreDownload/` 下。

- [ ] **Step 4: 验证 Addressables Groups 窗口**

打开 **Window > Asset Management > Addressables > Groups**，确认：
- `Chapter05Remote` 组存在，BuildPath = RemoteBuildPath，LoadPath = RemoteLoadPath
- 三个资产（Ch05CubeA / B / C）的 Labels 列显示 `Chapter05`

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Editor/StudyProjectSetup.cs
git add UnityProject/Assets/_Chapters/Chapter05_PreDownload/
git add UnityProject/Assets/AddressableAssetsData/
git commit -m "feat: Chapter05 scene and Addressables configuration"
```

---

### Task 4：构建 + 上传 CDN + 验证

**Files:** 无代码改动，操作步骤。

- [ ] **Step 1: 构建**

**Groups > Build > New Build > Default Build Script**（首次包含 Ch05 资产，必须 New Build）

预期：`ServerData/OSX/` 下出现新的 `.bundle` 和 `.hash` 文件。

- [ ] **Step 2: 上传 CDN**

将 `UnityProject/ServerData/OSX/` 下所有文件上传至阿里云 OSS。

- [ ] **Step 3: 刷新 CDN 缓存**

阿里云 OSS 控制台 → **刷新预热** → 提交 catalog 和新 bundle 文件的 URL。

- [ ] **Step 4: 编辑器切换 Play Mode Script**

**Addressables Groups 顶部 → Play Mode Script → Use Existing Build**

- [ ] **Step 5: 验证预下载完整流程**

进入 Play 模式，打开 Chapter05Scene：

1. 点「清除 Bundle 缓存」确保无本地缓存
2. 点「检查下载大小」→ 日志显示 `需要下载 XXX bytes`
3. 点「预下载」→ 进度文字从 `0%` 变化到 `100%`，日志显示 `预下载完成 ✓`
4. 点「加载资产」→ 场景中出现 3 个 Cube（橙/紫/绿），日志显示 `加载完成 ✓ 共 3 个资产`
5. 点「清理」→ Cube 消失
6. 再次点「加载资产」→ 立即加载（无 CDN 请求，因为已缓存）
7. 点「检查下载大小」→ 显示 `所有资产已缓存，无需下载 ✓`

- [ ] **Step 6: Commit**

```bash
git add UnityProject/ProjectSettings/
git commit -m "feat: Chapter05 pre-download flow complete"
```
