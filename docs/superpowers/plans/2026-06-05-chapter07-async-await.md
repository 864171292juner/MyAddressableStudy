# Chapter 07 async/await Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 Chapter 07 场景，同一场景并排演示回调写法与 async/await 写法，让读者直观对比两种代码风格。

**Architecture:** 复用 Ch01 DemoCube（不新建资产）；Chapter07Manager 包含两个独立加载路径，回调方式和 await 方式各持一个 handle 和实例；StatusText 实时反映当前加载状态。

**Tech Stack:** Unity 2022 LTS, Addressables 1.22.x, TextMeshPro, C# async/await（原生 Task，不依赖 UniTask）

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `UnityProject/Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Manager.cs` | 新建 | 回调 vs async/await 两种加载方式 |
| `UnityProject/Assets/Editor/StudyProjectSetup.cs` | 修改 | 新增 CreateChapter07Scene，SetupAll 添加调用 |

---

### Task 1：新建 Chapter07Manager.cs

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Manager.cs`

- [ ] **Step 1: 创建文件，写入完整实现**

```csharp
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter07Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private TextMeshProUGUI _statusText;

    private AsyncOperationHandle<GameObject> _callbackHandle;
    private AsyncOperationHandle<GameObject> _awaitHandle;
    private GameObject _callbackInstance;
    private GameObject _awaitInstance;

    private void Start()
    {
        _log        = FindObjectOfType<DebugLogPanel>();
        _statusText = GameObject.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

        WireButton("回调方式加载",      OnCallbackLoadClick);
        WireButton("async/await 加载",  OnAwaitLoadClick);
        WireButton("清理",              OnClearClick);
        WireButton("清空日志",           () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void SetStatus(string text)
    {
        if (_statusText != null) _statusText.text = $"状态: {text}";
    }

    // ── 回调方式 ────────────────────────────────────────────────────

    private void OnCallbackLoadClick()
    {
        if (_callbackHandle.IsValid()) { _log.Log("回调方式已加载，请先「清理」"); return; }
        _log.Log("【回调方式】LoadAssetAsync 开始...");
        SetStatus("加载中(回调)...");
        _callbackHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        _callbackHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _callbackInstance = Instantiate(h.Result, new Vector3(-2f, 0, 0), Quaternion.identity);
                SetStatus("完成 ✓");
                _log.Log("【回调方式】加载完成 ✓ 实例在左侧");
            }
            else
            {
                SetStatus("失败 ✗");
                _log.Log($"【回调方式】失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    // ── async/await 方式 ────────────────────────────────────────────

    private async void OnAwaitLoadClick()
    {
        if (_awaitHandle.IsValid()) { _log.Log("async/await 方式已加载，请先「清理」"); return; }
        _log.Log("【async/await】LoadAssetAsync 开始...");
        SetStatus("加载中(await)...");
        _awaitHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        try
        {
            var prefab = await _awaitHandle.Task;
            _awaitInstance = Instantiate(prefab, new Vector3(2f, 0, 0), Quaternion.identity);
            SetStatus("完成 ✓");
            _log.Log("【async/await】加载完成 ✓ 实例在右侧");
        }
        catch (Exception e)
        {
            SetStatus("失败 ✗");
            _log.Log($"【async/await】失败 ✗ {e.Message}");
            Addressables.Release(_awaitHandle);
            _awaitHandle = default;
        }
    }

    // ── 清理 ────────────────────────────────────────────────────────

    private void OnClearClick()
    {
        if (_callbackInstance != null) { Destroy(_callbackInstance); _callbackInstance = null; }
        if (_awaitInstance    != null) { Destroy(_awaitInstance);    _awaitInstance    = null; }
        if (_callbackHandle.IsValid()) Addressables.Release(_callbackHandle);
        if (_awaitHandle.IsValid())    Addressables.Release(_awaitHandle);
        _callbackHandle = default;
        _awaitHandle    = default;
        SetStatus("就绪");
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_callbackHandle.IsValid()) Addressables.Release(_callbackHandle);
        if (_awaitHandle.IsValid())    Addressables.Release(_awaitHandle);
    }
}
```

- [ ] **Step 2: 确认 Unity 编译无报错**

等待 Unity 编译完成，Console 无红色报错。

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Manager.cs
git commit -m "feat: add Chapter07Manager for async/await vs callback demo"
```

---

### Task 2：StudyProjectSetup — Ch07 场景

**Files:**
- Modify: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

- [ ] **Step 1: 在 `SetupAll()` 中添加 `CreateChapter07Scene()` 调用**

找到 `CreateChapter06Scene();` 这一行，在其后添加：

```csharp
CreateChapter06Scene();
CreateChapter07Scene();   // ← 新增
```

- [ ] **Step 2: 新增 `CreateChapter07Scene()` 方法**

在 `CreateChapter06Scene()` 方法之后添加：

```csharp
// ── Chapter 07 ────────────────────────────────────────────────

static void CreateChapter07Scene()
{
    var (scene, canvas) = NewScene("Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Scene.unity");

    Txt(canvas, "Title", "Chapter 07 — async/await", new Vector2(0, 0.92f), Vector2.one, 32);
    Btn(canvas, "回调方式加载",      new Vector2(0f,   0.78f), new Vector2(0.5f, 0.92f));
    Btn(canvas, "async/await 加载",  new Vector2(0.5f, 0.78f), new Vector2(1f,   0.92f));
    Btn(canvas, "清理",              new Vector2(0f,   0.64f), new Vector2(0.5f, 0.78f));
    Btn(canvas, "清空日志",           new Vector2(0.5f, 0.64f), new Vector2(1f,   0.78f));
    Txt(canvas, "StatusText", "状态: 就绪", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 24);

    AddLogPanel(canvas);

    new GameObject("Chapter07Manager").AddComponent<Chapter07Manager>();
    EditorSceneManager.SaveScene(scene,
        "Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Scene.unity");
}
```

- [ ] **Step 3: 运行 Setup**

菜单 **StudyUnity > Setup All Chapters**

预期：Console 输出 `[StudyUnity] Setup complete!`，`Chapter07Scene.unity` 出现在 `Assets/_Chapters/Chapter07_AsyncAwait/` 下。

- [ ] **Step 4: Commit**

```bash
git add UnityProject/Assets/Editor/StudyProjectSetup.cs
git add UnityProject/Assets/_Chapters/Chapter07_AsyncAwait/
git commit -m "feat: Chapter07 async/await scene and setup"
```

---

### Task 3：Play Mode 验证

**Files:** 无代码改动

- [ ] **Step 1: 进入 Play 模式，打开 Chapter07Scene**

Play Mode Script 保持 **Use Asset Database**。

- [ ] **Step 2: 验证回调方式**

点「回调方式加载」→ StatusText 短暂显示 `状态: 加载中(回调)...`，然后变为 `状态: 完成 ✓`，场景左侧（x=-2）出现 Cube，日志显示 `【回调方式】加载完成 ✓`。

- [ ] **Step 3: 验证 async/await 方式**

点「async/await 加载」→ StatusText 短暂显示 `状态: 加载中(await)...`，然后变为 `状态: 完成 ✓`，场景右侧（x=+2）出现 Cube，日志显示 `【async/await】加载完成 ✓`。

- [ ] **Step 4: 验证清理**

点「清理」→ 两个 Cube 消失，StatusText 显示 `状态: 就绪`。
