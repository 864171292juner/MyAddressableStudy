# Unity Addressables 进阶：预下载、AssetReference、async/await 与热更新全攻略

---

**[封面图：Unity 运行截图，左侧日志面板显示"下载进度: 73%"，右侧场景出现三个不同颜色的 Cube，叠加标题文字]**

---

## 前言

上一篇把 Addressables 从零跑通：基础加载、Handle 内存管理、场景加载，最后配合阿里云 OSS 实现了 CDN 热更新。

这篇把剩下的进阶用法一次讲完：

- **预下载**：进关卡前把资源备好，进入后秒开
- **AssetReference**：告别魔法字符串，用 Inspector 管引用
- **async/await**：比回调更清晰的异步写法
- **增量热更**：Update a Previous Build，只让用户下载变化的部分
- **多 Key + MergeMode**：并集/交集批量加载
- **Addressables Profiler**：可视化追踪内存，排查 Handle 泄漏

---

## 一、预下载：进关卡前把资源备好

### 为什么需要预下载

上一篇用的 `LoadAssetAsync` 是"用到了才下"。对于进入对局就必须显示的英雄模型、技能特效，这会让玩家对着空场景等 1-2 秒，体验很差。

王者荣耀的方案是**在进对局前统一下载**，进入后走本地缓存，秒开。对应的 API 是 `DownloadDependenciesAsync`。

### 三个 API 的分工

```
检查大小                   预下载到磁盘缓存              实际加载进内存
GetDownloadSizeAsync  →  DownloadDependenciesAsync  →  LoadAssetsAsync
     ↓                           ↓                           ↓
"需要下载 8 MB"           进度条从 0% 跑到 100%          几乎瞬间完成
```

`DownloadDependenciesAsync` **只把 bundle 写入磁盘缓存，不加载进内存**。之后调 `LoadAssetsAsync`，发现本地有缓存，直接读磁盘，速度接近瞬间完成。

### 检查大小：先问要下多少

`GetDownloadSizeAsync` 接收的是任意 key，和 `LoadAssetAsync` 的参数规则完全一样：

```csharp
// 传 Label：检查该 Label 下所有资产的总下载大小（最常用）
Addressables.GetDownloadSizeAsync("RemoteLabel");

// 传 Address：只检查某一个资产
Addressables.GetDownloadSizeAsync("RemoteCube");

// 传列表：检查多个 key 涉及的 bundle 总大小
var keys = new List<string> { "Chapter03_Hero", "Chapter03_Map" };
Addressables.GetDownloadSizeAsync(keys as IEnumerable);
```

没有设置 Label 也没关系，直接传 address 就行。实际项目通常**按 Label 检查整个模块**，这样不用在代码里硬编码资产列表，模块里增减资产也不用改代码。

```csharp
private void OnCheckSizeClick()
{
    var handle = Addressables.GetDownloadSizeAsync("RemoteLabel");
    handle.Completed += h =>
    {
        long bytes = h.Result;
        if (bytes == 0)
            _log.Log("无需下载（本地资产或已有缓存）✓");
        else
            _log.Log($"需要下载 {bytes / 1024f / 1024f:F2} MB");
        Addressables.Release(handle);
    };
}
```

`bytes == 0` 有两种情况：
- 资产在 **Local Group**（打进包体，不走网络），天然为 0
- 资产在 **Remote Group** 但本地已有 bundle 缓存，不需要重新下载

两种情况都可以直接跳过预下载步骤。

### 预下载 + 实时进度条

```csharp
private void OnPreDownloadClick()
{
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

注意：`DownloadDependenciesAsync` 返回的是**非泛型** `AsyncOperationHandle`，没有返回值，只有成功/失败状态。

**[配图：两列对比截图。左列"未预下载"：日志面板显示进度从 0% 爬升到 100%，耗时约 4 秒；右列"已预下载"：日志直接显示"加载完成 ✓"，无进度过程]**

### PercentComplete 覆盖的不只是下载

`PercentComplete` 覆盖整个操作生命周期，包括下载、解压、加载进内存。预下载后再加载，进度仍然从 0 跑到 100，只是这个过程极快，不会卡在某个值。

---

## 二、AssetReference：告别魔法字符串

### 字符串加载的隐患

```csharp
Addressables.LoadAssetAsync<GameObject>("DemoCubbe"); // 拼错了，编译通过，运行时才崩
```

字符串地址是"魔法字符串"——拼错、资产被删、地址改名，编译器完全不知道，只有运行时才暴露问题。

### AssetReference 的做法

```csharp
public class Chapter06Manager : MonoBehaviour
{
    [SerializeField] AssetReferenceGameObject _cubeRef; // Inspector 里直接拖拽

    private void OnRefLoadClick()
    {
        if (!_cubeRef.RuntimeKeyIsValid()) { _log.Log("未赋值，请检查 Inspector"); return; }
        _refHandle = _cubeRef.LoadAssetAsync<GameObject>();
        _refHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
                _refInstance = Instantiate(h.Result, new Vector3(2f, 0, 0), Quaternion.identity);
        };
    }

    private void OnClearClick()
    {
        if (_refInstance != null) { Destroy(_refInstance); _refInstance = null; }
        if (_refHandle.IsValid()) _cubeRef.ReleaseAsset(); // 注意：不是 Addressables.Release
        _refHandle = default;
    }
}
```

**[配图：Unity Inspector 截图，Chapter06Manager 组件展示 Cube Ref 字段显示 "DemoCube" 而非 None]**

### 释放方式不同，原因是内部状态

`AssetReference` 在调用 `LoadAssetAsync` 时，内部也记录了一份 handle 引用。如果用 `Addressables.Release(_refHandle)` 直接释放，bundle 确实卸载了，但 `_cubeRef` 内部还以为自己持有有效 handle，下次加载会出问题。必须用 `_cubeRef.ReleaseAsset()`，让它自己清理内部状态。

| 加载方式 | 释放方式 |
|---------|---------|
| `Addressables.LoadAssetAsync(key)` | `Addressables.Release(handle)` |
| `_ref.LoadAssetAsync<T>()` | `_ref.ReleaseAsset()` |

**原则：谁加载谁释放，保持状态一致。**

### AssetReference 本质是 GUID 包装类

```csharp
// 源码（简化）
public class AssetReference
{
    [SerializeField] string m_AssetGUID; // 核心：资产的 GUID
}
public class AssetReferenceGameObject : AssetReferenceT<GameObject>
{
    public AssetReferenceGameObject(string guid) : base(guid) { }
}
```

这意味着可以**不拖拽、用代码构造**，适合服务器下发资产 key 的动态换装场景：

```csharp
// 服务器下发 GUID，动态加载新皮肤
var skinRef = new AssetReferenceGameObject(guidFromServer);
skinRef.LoadAssetAsync<GameObject>();
```

---

## 三、async/await：比回调更清晰的异步写法

### 回调方式（.Completed）

```csharp
private void OnCallbackLoadClick()
{
    _callbackHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
    _callbackHandle.Completed += h =>
    {
        if (h.Status == AsyncOperationStatus.Succeeded)
        {
            _callbackInstance = Instantiate(h.Result, new Vector3(-2f, 0, 0), Quaternion.identity);
            SetStatus("完成 ✓");
            _log.Log("【回调方式】加载完成");
        }
        else
        {
            SetStatus("失败 ✗");
            _log.Log($"【回调方式】失败 {h.OperationException?.Message}");
        }
    };
}
```

### async/await 方式

```csharp
private async void OnAwaitLoadClick()
{
    _awaitHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
    SetStatus("加载中(await)...");
    try
    {
        var prefab = await _awaitHandle.Task;     // 挂起，把线程还给 Unity
        _awaitInstance = Instantiate(prefab, new Vector3(2f, 0, 0), Quaternion.identity);
        SetStatus("完成 ✓");
        _log.Log("【async/await】加载完成");
    }
    catch (Exception e)
    {
        SetStatus("失败 ✗");
        _log.Log($"【async/await】失败 {e.Message}");
        Addressables.Release(_awaitHandle);
        _awaitHandle = default;
    }
}
```

两种写法**逻辑完全等价**，区别只在代码风格：

| | .Completed 回调 | async/await |
|---|---|---|
| 代码结构 | 嵌套回调 | 线性顺序 |
| 错误处理 | 检查 `h.Status` | `try/catch` |
| 多个连续异步操作 | 回调嵌套回调（难读） | 一行接一行（清晰）|
| Unity 版本 | 无限制 | 2019+（实际项目基本满足）|

简单的单次加载，两者差别不大，选哪个都行。**有多个连续异步操作时，async/await 优势才明显：**

```csharp
// 回调方式：三层嵌套，越写越往右
Addressables.GetDownloadSizeAsync("Chapter03").Completed += sizeHandle =>
{
    if (sizeHandle.Result > 0)
    {
        Addressables.DownloadDependenciesAsync("Chapter03").Completed += dlHandle =>
        {
            Addressables.LoadAssetsAsync<GameObject>("Chapter03", null).Completed += loadHandle =>
            {
                // 终于到这里了...
            };
        };
    }
};

// async/await：线性，一眼看懂
var sizeHandle = Addressables.GetDownloadSizeAsync("Chapter03");
await sizeHandle.Task;
if (sizeHandle.Result > 0)
    await Addressables.DownloadDependenciesAsync("Chapter03").Task;
var loadHandle = Addressables.LoadAssetsAsync<GameObject>("Chapter03", null);
await loadHandle.Task;
// 直接用 loadHandle.Result
```

### await 不是阻塞

`await` 不是"卡住等"。执行到 `await` 那行时，方法挂起，把线程还给 Unity，Unity 继续跑其他帧和响应输入。加载完成后，方法从 `await` 那行恢复，继续往下执行。`await` 之后的所有代码（包括 `{}` 外面的）都在加载完成后才执行。

不需要 UniTask，Unity 2019+ 原生支持 `AsyncOperationHandle.Task`。

**[配图：运行截图，日志面板显示"【回调方式】加载完成 ✓ 实例在左侧"和"【async/await】加载完成 ✓ 实例在右侧"，场景中左右各一个 Cube]**

---

## 四、Update a Previous Build：真正的增量热更

### New Build 的问题

每次热更都用 `New Build`，相当于让用户重新下载全部内容——所有 bundle 的哈希全部重新生成，全部"变化"，全部需要重新下载。

```
New Build 每次热更：
  用户下载量 = 所有 bundle 总大小（10 MB、50 MB、100 MB...）
```

### Update a Previous Build 的做法

只重新打**有变化**的 bundle，没变化的 bundle 哈希不变，用户不需要重新下载：

```
Update a Previous Build 热更：
  只有修改过的资产对应的 bundle 哈希变化
  用户下载量 = 变化的 bundle 大小（可能只有几十 KB）
```

**[配图：两栏对比图。左栏"New Build"：所有 bundle 重新生成，全部下载；右栏"Update a Previous Build"：只有变化的 bundle 重新生成，其余不动]**

### 操作步骤

```
首次发布：
  Groups > Build > New Build > Default Build Script
  → 上传 ServerData/ 全部文件到 CDN

后续热更：
  修改资产（改颜色、换模型、加新资产...）
  → Groups > Build > Update a Previous Build
  → 上传 ServerData/ 到 CDN（只需上传有变化的文件）
  → CDN 控制台刷新缓存
```

| | New Build | Update a Previous Build |
|---|---|---|
| 适用场景 | 首次发布 / 大版本重构 | 日常热更新 |
| Bundle 哈希 | 全部重新生成 | 只有变化的 bundle |
| 用户下载量 | 全部 bundle | 仅变化部分 |
| 依赖 | 无 | 依赖上一次 New Build 产物 |

### Update a Previous Build 依赖什么

`New Build` 执行完后，会在 `Assets/AddressableAssetsData/` 目录下生成一个文件：

```
Assets/AddressableAssetsData/addressables_content_state.bin
```

这个文件记录了上次 Build 时每个资产的哈希值。`Update a Previous Build` 靠对比这个文件和当前资产状态，判断哪些 bundle 需要重新打包。

**这个文件必须保留，建议加入版本控制（git track 它）。** 如果删了或换了机器找不到，`Update a Previous Build` 会报错，只能重新 `New Build`，用户就得全量下载了。

**[配图：Finder 截图，展示 Assets/AddressableAssetsData/ 目录下的 addressables_content_state.bin 文件]**

---

## 五、多 Key + MergeMode：并集与交集

### 单 Key 加载的局限

```csharp
// 只能按一个 Label 加载一批
Addressables.LoadAssetsAsync<GameObject>("SetA", null);
```

如果要同时加载多个分类的全部资产，或者只加载同时属于多个分类的资产（比如"限定角色"），单 Key 做不到。

### LoadAssetsAsync 多 Key 重载

传入 `IList<object>` 加上第三个 `MergeMode` 参数：

```csharp
// 注意 key 列表类型是 List<object>，不是 List<string>
Addressables.LoadAssetsAsync<GameObject>(
    new List<object> { "SetA", "SetB" },
    null,                           // 每加载完一个资产的回调，不需要可以传 null
    Addressables.MergeMode.Union    // 合并模式
);
```

### 三种 MergeMode

用三个 Prefab 演示。先看资产的 Label 分配：

```
Alpha（红）  → Label: SetA
Beta（黄）   → Label: SetA + SetB   ← 同时拥有两个 Label
Gamma（蓝）  → Label: SetB
```

传入 keys = ["SetA", "SetB"] 时，三种模式的结果：

| 模式 | 含义 | 加载结果 |
|------|------|---------|
| `Union` | **并集**，匹配任一 Key 的资产都要 | 红 + 黄 + 蓝（3个）|
| `Intersection` | **交集**，必须同时匹配所有 Key | 只有黄（1个，因为只有 Beta 同时属于 SetA 和 SetB）|
| `UseFirst` | 只用第一个 Key 的结果，其余忽略 | 红 + 黄（2个，等同单独加载 SetA）|

**[配图：三张示意图，每张画两个圆圈代表 SetA 和 SetB，Union 高亮整个两圆，Intersection 只高亮两圆重叠部分，UseFirst 只高亮第一个圆]**

```csharp
// Union：批量预加载"角色"和"道具"两个分类的全部资产
var unionKeys = new List<object> { "角色", "道具" };
Addressables.LoadAssetsAsync<GameObject>(unionKeys, null, Addressables.MergeMode.Union);

// Intersection：只加载同时带有"角色"和"限定"标签的资产（限定角色）
var intersectKeys = new List<object> { "角色", "限定" };
Addressables.LoadAssetsAsync<GameObject>(intersectKeys, null, Addressables.MergeMode.Intersection);
```

**[配图：四张运行截图，分别对应"加载 SetA（2个）"、"加载 SetB（2个）"、"Union（3个）"、"Intersection（1个黄色）"，场景中 Cube 数量和颜色不同]**

---

## 六、Addressables Profiler：可视化追踪内存

调试内存问题时，光靠日志很难看清当前有哪些资产在内存里、有没有 Handle 泄漏。Addressables 提供了专门的 Profiler 模块来可视化这些信息。

### 开启方法

**第一步：开启事件记录**

**Window > Asset Management > Addressables > Settings → 勾选 Send Profiler Events**

**[配图：Addressables Settings 窗口截图，红框圈出已勾选的 Send Profiler Events 复选框]**

⚠️ 开启后有性能开销，只在调试时开，发布前务必关掉。

**第二步：Play Mode Script 切换为 Use Existing Build**

Addressables Profiler 只追踪真实的 bundle 加载操作。**Use Asset Database 模式下完全没有数据**，因为那个模式直接读工程文件，绕过了整个 bundle 系统。

Play Mode Script 必须切到 **Use Existing Build**，Profiler 才有内容可以显示。前提是本地已经做过 Content Build（构建产物存在），如果从未 Build 过需要先执行一次。

**第三步：打开 Profiler 窗口**

底部 tab 栏点 **Profiler**，或菜单 **Window > Analysis > Profiler**，左侧 Profiler Modules 下找到 **Addressable Assets** 模块。

> 注意：底部还有一个 **Addressables Event Viewer** tab，但它已经被标记为废弃（deprecated），直接用 Profiler 即可。

**[配图：Unity 底部 tab 栏截图，标注 Profiler tab，以及 Profiler 窗口里 Addressable Assets 模块展示 Asset Bundles、Assets、Scenes、Catalogs 四项]**

### 四个指标的含义

| 指标 | 含义 |
|------|------|
| **Asset Bundles** | 当前加载在内存中的 bundle 数量 |
| **Assets** | 当前加载在内存中的资产数量 |
| **Scenes** | 当前加载的场景数量 |
| **Catalogs** | 当前加载的 Catalog 数量 |

### 典型用法

**排查 Handle 泄漏：**
操作完「清理」按钮后，Asset Bundles 和 Assets 的数量应该归零。如果清理后数量没有下降，说明有 Handle 没有调用 `Release`，bundle 仍然留在内存里。

**确认加载状态：**
点击「加载资产」后，能看到 Asset Bundles 和 Assets 数量上升；点「清理」后应该回落。数量一直增长不回落就是泄漏。

**双重释放排查：**
对同一个 Handle 调用两次 `Release`，Console 会报 "Releasing handle that has already been released"。检查 `OnDestroy` 和 `OnClearClick` 是否都在释放同一个 Handle 而没有 `IsValid()` 判断。

---

## 七、踩坑记录

### 坑 1：预下载后加载还是很慢

**现象**：明明做了预下载，`LoadAssetsAsync` 仍然有明显延迟  
**原因**：预下载传的 key 和加载传的 key 不一致，没有命中同一个 bundle 缓存

```csharp
// ❌ key 不一致，缓存没命中
Addressables.DownloadDependenciesAsync("RemoteLabel");
Addressables.LoadAssetsAsync<GameObject>("RemoteCube", null); // 不同 key

// ✓ 传相同的 key
Addressables.DownloadDependenciesAsync("RemoteLabel");
Addressables.LoadAssetsAsync<GameObject>("RemoteLabel", null);
```

### 坑 2：AssetReference 用 Addressables.Release 释放

**现象**：`_cubeRef.ReleaseAsset()` 没调，改用 `Addressables.Release(_refHandle)`，第二次加载时报错  
**原因**：`Addressables.Release` 只释放引用计数，不清理 `_cubeRef` 内部的 handle 记录，导致 `_cubeRef` 认为自己仍有有效 handle  
**解决**：`AssetReference` 加载的资产，必须用 `_ref.ReleaseAsset()` 释放

### 坑 3：async void 里的异常被吞掉

**现象**：await 加载失败，没有任何日志，也没有异常抛出  
**原因**：`async void` 方法里如果 `try/catch` 范围写错，异常会被静默吞掉，不会传播到调用方  
**解决**：`await` 必须包在 `try/catch` 里，catch 块里手动输出日志

```csharp
try
{
    var prefab = await _awaitHandle.Task;
    // ...
}
catch (Exception e)
{
    _log.Log($"失败 ✗ {e.Message}"); // 不加这行，失败时什么都看不到
    Addressables.Release(_awaitHandle);
    _awaitHandle = default;
}
```

### 坑 4：Update a Previous Build 找不到上次的 Build 产物

**现象**：点 `Update a Previous Build`，弹出对话框让你手动选文件，或直接报"找不到之前的 Build"  
**原因**：`Assets/AddressableAssetsData/addressables_content_state.bin` 被删了，或者换了机器没有同步这个文件  
**解决**：把这个文件加入 git 版本控制，每次 New Build 后连同它一起提交。换机器时 pull 下来即可，不需要重新 New Build

### 坑 5：MergeMode.Intersection 返回空列表

**现象**：Intersection 加载，`h.Result.Count == 0`，场景里什么都没有  
**原因**：没有任何资产同时拥有所有指定的 Label，交集为空集  
**解决**：在 Addressables Groups 窗口里确认目标资产确实同时拥有所有 Label，Beta 要显示 `SetA SetB` 两个 Label

**[配图：Addressables Groups 窗口截图，Ch09CubeBeta 的 Labels 列显示 "SetA SetB"]**

### 坑 6：Addressables Profiler 没有数据

**现象**：打开 Profiler，Addressable Assets 模块时间线空白，什么都没有  
**原因 1**：Play Mode Script 是 **Use Asset Database**，这个模式绕过 bundle 系统，Profiler 没有任何操作可以记录  
**原因 2**：Addressables Settings 里 **Send Profiler Events** 未勾选  
**解决**：两个都要确认——勾选 Send Profiler Events，并将 Play Mode Script 切换为 **Use Existing Build**（本地需有 Content Build 产物，做过 Build 的项目直接切即可），重新进入 Play 模式后数据才会出现

---

## 总结

| 场景 | API | 要点 |
|------|-----|------|
| 进关卡前批量下载 | `DownloadDependenciesAsync` | 只写磁盘，不进内存；先用 `GetDownloadSizeAsync` 判断是否需要下载 |
| 类型安全的资产引用 | `AssetReferenceGameObject` | Inspector 拖拽或代码传 GUID 构造；释放用 `ReleaseAsset()` |
| 现代异步写法 | `await handle.Task` | `async void` 只做入口；`await` 不阻塞主线程；错误用 `try/catch` |
| 增量热更新 | `Update a Previous Build` | 首次用 New Build，后续热更用 Update；保留 `content_state.bin` |
| 批量加载多分类 | `LoadAssetsAsync + MergeMode` | Union = 并集；Intersection = 交集 |
| 内存问题排查 | Event Viewer | 开启 Send Profiler Events；观察引用计数曲线 |

---

## 尾声

到这里，Addressables 的核心用法基本覆盖完了。两篇文章从 Handle 内存管理、CDN 热更新，到预下载、AssetReference、async/await、增量热更、MergeMode，是一条完整的学习路径。

实际项目里这些能力会组合使用——`AssetReference` 管引用，`DownloadDependenciesAsync` 提前缓存，`async/await` 写清晰的加载流程，`Update a Previous Build` 控制用户下载量，`MergeMode.Intersection` 精准筛选限定资产。

理解了这套体系，YooAssets 等社区方案也是同样的思路，触类旁通。

---

*示例工程代码见文末链接。*
