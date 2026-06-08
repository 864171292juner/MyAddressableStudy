# Unity Addressables 预下载：进关卡前把资源备好

---

**[封面图：Unity 运行截图，左侧日志面板显示"下载进度: 73%"，右侧场景空白；对比右图日志面板显示"加载完成 ✓"，场景中出现彩色 Cube，叠加标题文字]**

---

## 前言

上一篇跑通了 CDN 热更新：检查 Catalog → 更新 Catalog → 加载远端资源。但那套流程有个问题——资源是"用到了才下"。

对于非时间敏感的资源（背景音乐、展示图鉴），这完全没问题。但对于进入对局就必须立刻显示的英雄模型、技能特效，"用到了才下"会让玩家对着空场景等 1-2 秒，体验很差。

王者荣耀的做法是：**进对局前先检查需要下载多少，让玩家在加载界面等完，进入后秒开**。

这篇文章把这套流程从零跑通：
- `GetDownloadSizeAsync`：先问一下要下多少
- `DownloadDependenciesAsync`：把 bundle 下到本地磁盘
- `PercentComplete`：实时进度条
- 对比演示：有没有提前下载，差距有多大

---

## 一、三个 API，一套完整流程

```
检查大小                   预下载到磁盘缓存              实际加载进内存
GetDownloadSizeAsync  →  DownloadDependenciesAsync  →  LoadAssetsAsync
     ↓                           ↓                           ↓
"需要下载 12 MB"          进度条从 0% 跑到 100%          瞬间完成（走缓存）
```

关键点：`DownloadDependenciesAsync` **只把 bundle 写入磁盘缓存，不加载进内存**。之后调 `LoadAssetsAsync`，发现本地有缓存，直接读磁盘，速度接近瞬间完成。

---

## 二、GetDownloadSizeAsync：先问要下多少

```csharp
private void OnCheckSizeClick()
{
    _log.Log("GetDownloadSizeAsync 开始...");
    var handle = Addressables.GetDownloadSizeAsync("RemoteLabel");
    handle.Completed += h =>
    {
        if (h.Status == AsyncOperationStatus.Succeeded)
        {
            long bytes = h.Result;
            if (bytes == 0)
                _log.Log("无需下载（已有缓存）✓");
            else
                _log.Log($"需要下载 {bytes / 1024f / 1024f:F2} MB");
        }
        else
        {
            _log.Log($"检查失败 ✗ {h.OperationException?.Message}");
        }
        Addressables.Release(handle);
    };
}
```

**`bytes == 0` 有两种情况：**
1. 从未下载，但资产在本地 Group（Local），不走网络
2. 之前已下载过，bundle 在磁盘缓存里

两种情况都不需要下载，`DownloadDependenciesAsync` 传入后会立刻完成。

**[配图：日志面板截图，显示"需要下载 8.45 MB"]**

---

## 三、DownloadDependenciesAsync：下到磁盘

```csharp
private void OnPreDownloadClick()
{
    _log.Log("DownloadDependenciesAsync 开始...");
    _downloadHandle = Addressables.DownloadDependenciesAsync("RemoteLabel");
    _downloadHandle.Completed += h =>
    {
        if (h.Status == AsyncOperationStatus.Succeeded)
            _log.Log("预下载完成 ✓ 资源已缓存到本地");
        else
            _log.Log($"预下载失败 ✗ {h.OperationException?.Message}");
        Addressables.Release(_downloadHandle);
        _downloadHandle = default;
    };
    StartCoroutine(TrackProgress(_downloadHandle));
}
```

注意：`DownloadDependenciesAsync` 的 handle **类型是 `AsyncOperationHandle`（非泛型）**，不是 `AsyncOperationHandle<T>`，因为它没有有意义的返回值，只有成功/失败状态。

---

## 四、进度条：PercentComplete

```csharp
private IEnumerator TrackProgress(AsyncOperationHandle handle)
{
    while (!handle.IsDone)
    {
        _progressText.text = $"下载进度: {handle.PercentComplete * 100:F0}%";
        yield return null;
    }
    _progressText.text = "下载进度: 100%";
}
```

`PercentComplete` 的值范围是 `0f ~ 1f`，每帧更新就能得到流畅的进度条。

**[配图：运行截图，进度文字显示"下载进度: 47%"，日志面板显示"DownloadDependenciesAsync 开始..."]**

---

## 五、对比演示：有没有预下载，差距有多大

这是整个章节最直观的部分。场景里有两个按钮：

**不预下载，直接加载：**
```csharp
// 点击后日志：
// [12:00:00] LoadAssetsAsync 开始...
// [12:00:00] 加载进度: 0%
// [12:00:01] 加载进度: 23%
// [12:00:03] 加载进度: 78%
// [12:00:04] 加载完成 ✓  共 3 个资产
```

**先预下载，再加载：**
```csharp
// 预下载阶段（进关卡前）：
// [12:00:00] 预下载开始...  下载进度: 0%
// [12:00:02]                下载进度: 100%
// [12:00:02] 预下载完成 ✓

// 加载阶段（进关卡后，走本地缓存）：
// [12:00:05] LoadAssetsAsync 开始...
// [12:00:05] 加载完成 ✓  共 3 个资产   ← 几乎瞬间
```

**[配图：两列对比截图。左列"未预下载"：日志面板显示进度从 0% 爬升到 100%，耗时 4 秒；右列"已预下载"：日志显示直接完成，无进度过程]**

---

## 六、`PercentComplete` 包含哪些阶段

这里有个容易误解的地方：`LoadAssetsAsync` 的 `PercentComplete` **不只是下载进度**，它覆盖整个操作的生命周期：

```
0% ──── 下载 bundle ──── 解压/校验 ──── 加载进内存 ──── 100%
```

所以当 bundle 已预下载时，`PercentComplete` 仍然会从 0 跑到 100，只是这个过程非常快（只有解压 + 加载阶段）。你不会看到进度条卡在 0% 不动，但确实快得你可能来不及截图。

---

## 七、踩坑

### 坑 1：预下载后加载还是很慢

**原因**：预下载时传的 key 和加载时传的 key 不一致，缓存没命中。

```csharp
// ❌ 预下载传 Label，加载传 Address
Addressables.DownloadDependenciesAsync("RemoteLabel");
Addressables.LoadAssetsAsync<GameObject>("RemoteCube", null); // 不同的 key！

// ✓ 传同样的 key，或者传能覆盖同一批资产的 key
Addressables.DownloadDependenciesAsync("RemoteLabel");
Addressables.LoadAssetsAsync<GameObject>("RemoteLabel", null); // ✓
```

预下载是按 key 下载 **依赖的 bundle**，加载时如果走到同一个 bundle，就命中缓存。关键是两个 key 要能映射到相同的 bundle。

---

### 坑 2：重复调用 DownloadDependenciesAsync 浪费流量

**原因**：没有先调 `GetDownloadSizeAsync` 检查，直接每次进关卡都预下载。

如果已经有缓存，`DownloadDependenciesAsync` 虽然会快速完成（不重新下载），但每次调用仍然有 HTTP 请求校验 bundle 完整性的开销。

**正确做法**：先 `GetDownloadSizeAsync`，返回 0 就跳过预下载直接加载。

```csharp
var sizeHandle = Addressables.GetDownloadSizeAsync("RemoteLabel");
await sizeHandle.Task;
if (sizeHandle.Result > 0)
{
    // 需要下载，走预下载流程
    await Addressables.DownloadDependenciesAsync("RemoteLabel").Task;
}
Addressables.Release(sizeHandle);
// 直接加载（有缓存走缓存，无缓存重新下）
var loadHandle = Addressables.LoadAssetsAsync<GameObject>("RemoteLabel", null);
```

---

### 坑 3：进度条跳变，不流畅

**原因**：`TrackProgress` 协程里每帧更新，但 `PercentComplete` 的更新频率取决于 bundle 下载的分片粒度，较大的 bundle 可能会在 0% 停留很长时间后突然跳到 80%。

**解决**：这是 Addressables 底层行为，无法精细控制单个 bundle 内部进度。可以把大 bundle 拆成多个小 bundle（按资产数量合理分组），或者叠加一个假进度动画让视觉上更流畅。

---

## 八、完整代码结构

```csharp
public class Chapter05Manager : MonoBehaviour
{
    private AsyncOperationHandle _downloadHandle;
    private AsyncOperationHandle<IList<GameObject>> _loadHandle;
    private readonly List<GameObject> _instances = new List<GameObject>();

    // 1. 检查大小
    private void OnCheckSizeClick() { ... }

    // 2. 预下载（带进度条）
    private void OnPreDownloadClick() { ... }
    private IEnumerator TrackLoadProgress(AsyncOperationHandle<IList<GameObject>> handle) { ... }

    // 3. 直接加载（无预下载，走完整下载+加载流程）
    private void OnLoadAssetsClick()
    {
        _loadHandle = Addressables.LoadAssetsAsync<GameObject>("RemoteLabel", null);
        _loadHandle.Completed += h => { /* 实例化 */ };
        StartCoroutine(TrackLoadProgress(_loadHandle));
    }

    // 4. 清理
    private void OnClearClick()
    {
        foreach (var inst in _instances) Destroy(inst);
        _instances.Clear();
        if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
        _loadHandle = default;
    }

    private void OnDestroy()
    {
        if (_downloadHandle.IsValid()) Addressables.Release(_downloadHandle);
        if (_loadHandle.IsValid())     Addressables.Release(_loadHandle);
    }
}
```

---

## 总结

| API | 作用 | 返回值 |
|-----|------|--------|
| `GetDownloadSizeAsync(key)` | 查询需要下载的字节数 | `long`（0 = 无需下载）|
| `DownloadDependenciesAsync(key)` | 下载 bundle 到磁盘缓存 | 无返回值，只有成功/失败 |
| `LoadAssetsAsync(key, ...)` | 加载进内存并返回资产列表 | `IList<T>` |
| `handle.PercentComplete` | 当前操作进度 | `0f ~ 1f`，覆盖下载+加载全程 |

**流程口诀：先查大小 → 有则预下 → 无缝加载。**

---

## 下一篇预告

这两篇都在用字符串地址加载资产。字符串有一个隐患：拼错了、资产被删了，编译时不会报错，只有运行时才崩——尤其在大项目里很难排查。

Unity 提供了 `AssetReference` 来解决这个问题：在 Inspector 里直接拖拽引用，编辑器帮你保证引用有效，代码里不再出现魔法字符串。

下一篇：**AssetReference——告别魔法字符串，用 Inspector 管理资产引用**。

---

*示例工程代码见文末链接。*
