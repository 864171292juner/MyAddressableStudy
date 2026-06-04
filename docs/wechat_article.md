# Unity Addressables 踩坑实录：从基础加载到 CDN 热更新全跑通

---

**[封面图：Unity 编辑器截图，展示 Addressables Groups 窗口 + 运行中的场景，叠加标题文字]**

---

## 前言

最近把 Unity Addressables 从头到尾跑了一遍，从最基础的资产加载，到 Handle 内存管理，再到配合阿里云 OSS 实现 CDN 热更新——踩了不少坑。

这篇文章把所有概念、代码、配置和坑点整理出来，尽量做到看完就能上手。

---

## 一、Addressables 是什么，为什么要用它

### 从 AssetBundle 说起

Unity 资源热更新的底层永远是 AssetBundle（AB 包），但直接用 AB 包有两个明显的痛点：

**痛点一：寻址方式耦合路径**

```csharp
// AB 包：你必须知道包名、路径、资产名，三者缺一不可
AssetBundle bundle = AssetBundle.LoadFromFile("assets/prefabs");
GameObject player = bundle.LoadAsset<GameObject>("Player");
// 文件移动、包重命名 → 代码报错
```

```csharp
// Addressables：只需要一个字符串地址，与物理路径完全解耦
Addressables.LoadAssetAsync<GameObject>("Player");
// 资源移到哪里、打进哪个包 → 代码不受影响
```

**痛点二：依赖关系要手动处理**

AB 包要求你按顺序加载：资源 A 依赖资源 B，必须先手动加载 B，再加载 A，否则报错。Addressables 直接加载资源 A，引擎自动分析并加载所有依赖项，无论这些依赖分散在哪个本地或远端包里，你完全不用关心。

**[配图：两栏对比图。左栏"AssetBundle"，画出需要手动管理的依赖链：先加载 Bundle B → 再加载 Bundle A；右栏"Addressables"，只有一个箭头：加载 A → 依赖自动解析]**

### Addressables 的核心思路

整个系统是**声明式**的：你只需要在编辑器里把资源标记为 "Addressable"，给它一个唯一的地址。构建时，Addressables 自动分析所有标记的资源，处理依赖关系，构建出优化的资源包和 Catalog 目录文件。

你告诉引擎"这个东西是可寻址的"，引擎处理后续所有复杂的打包逻辑。

**热更新是 Addressables 的核心设计目标之一**。它内置了 Content Catalog 机制，资源可以发布到本地或远端 CDN，运行时自动检查版本差异，按需下载更新内容。

**[配图：流程图。左边：开发者在 Inspector 里勾选 Addressable 复选框；中间：Build 自动生成 Bundle + Catalog；右边：运行时通过 Address 加载]**

### 和 YooAssets 的区别

如果你用过 YooAssets，上手 Addressables 时会有一个直观对比：

```csharp
// YooAssets：先拿 Package，再加载
YooAssets.GetPackage("DefaultPackage").LoadAssetAsync<GameObject>(prefabName);

// Addressables：直接用地址加载，不感知 Package 概念
Addressables.LoadAssetAsync<GameObject>("DemoCube");
```

**[配图：两段代码并排对比，用不同背景色区分 YooAssets（蓝色）和 Addressables（绿色）]**

| | Addressables | YooAssets |
|---|---|---|
| 官方背书 | Unity 官方 | 社区开源 |
| 国内热更实践 | 较少 | 非常成熟 |
| API 风格 | 地址驱动，全局加载 | Package 隔离，显式管理 |
| 配置复杂度 | 较高，坑多 | 相对规范 |

两者底层都是 AssetBundle，理解了 Addressables 的原理，YooAssets 也就触类旁通了。

---

## 二、核心概念

### Address（地址）

给每个资产分配一个字符串 key，运行时通过这个 key 加载，与文件路径完全解耦。

```csharp
Addressables.LoadAssetAsync<Sprite>("DemoSprite1"); // 参数就是 Address 名称
```

**[配图：Unity 编辑器里 Addressables Groups 窗口截图，展示资产列表中 Address 列的值，如 "DemoCube"、"DemoSprite"]**

### Label（标签）

一个资产可以打多个 Label，用来给一批资产归类。

**Address 和 Label 在运行时没有类型区别，都是 key。** 你用哪个 API、工程里怎么命名，决定了是"加载一个"还是"加载一批"。

**Label 的正确用途是"这批资源是一伙的"**——预加载、批量下载、批量释放、按模块热更。要加载"某一个"资产，永远用 Address，别指望 Label 单独完成这件事。

```csharp
// 按 Address 加载：精确加载指定资产
_spriteHandle = Addressables.LoadAssetAsync<Sprite>("DemoSprite1");

// 按 Label 加载：加载 Catalog 里该 Label 下的第一个 entry
// ⚠️ 顺序由 Catalog entry 顺序决定，不是文件名顺序，不能靠名称推断
_spriteHandle = Addressables.LoadAssetAsync<Sprite>("Label1_Sprites");
```

**[配图：Addressables Groups 窗口截图，展示同一个资产同时拥有 Address 和 Label 两列数据，Label 列显示 "Label1_Sprites"]**

### 自动依赖管理

这是 Addressables 解决"依赖地狱"的关键能力。

当你加载一个预制体时，它可能引用了材质、纹理、Shader，这些资源可能分散在不同的本地或远端 bundle 里。Addressables 会自动分析完整的依赖链，按正确顺序加载所有依赖项：

```csharp
// 一行代码，Addressables 自动处理所有依赖
var handle = Addressables.LoadAssetAsync<GameObject>("HeroWithEquipment");
```

对比传统 AB 包，这省去了大量手动追踪依赖、按序加载的样板代码。

### Group（组）

资产分组决定打包策略，一个 Group 生成一个 AssetBundle。

- **Local Group**：bundle 打入 app 包，用户安装即有，不走网络
- **Remote Group**：bundle 上传 CDN，运行时按需下载，支持热更新

### Catalog（目录）

一个 JSON 文件，记录了所有资产的地址、所在 bundle 文件名、bundle 的下载路径。运行时 Addressables 先加载 Catalog，再根据 Catalog 找到实际 bundle。

**热更新的本质就是更新这个 Catalog。**

**[配图：Catalog 示意图。画一个 JSON 文件图标，用箭头指向三个内容："资产地址 → DemoCube"、"Bundle 文件名 → localcontent_assets_all_xxx.bundle"、"Bundle 路径 → https://cdn.../Data/"]**

---

## 三、Handle 与内存管理

这部分是 Addressables 最容易踩坑的地方，核心是**引用计数**。

### 规则

```
LoadAssetAsync()          → 引用计数 +1
Addressables.Release()    → 引用计数 -1
引用计数归零              → 资产从内存卸载
```

### 完整流程

```csharp
// 1. 加载（引用计数 +1）
var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
handle.Completed += h => {
    // 2. 实例化（不影响引用计数）
    var instance = Instantiate(h.Result);
};

// 3. 销毁实例（只移除 GameObject，引用计数不变！）
Destroy(instance);

// 4. 释放 Handle（引用计数 -1，归零后资产卸载）
Addressables.Release(handle);
```

**[配图：时序图，横轴是时间，纵轴是引用计数数值。展示 Load(+1)、Instantiate(不变)、Destroy(不变)、Release(-1) 四个节点的引用计数变化]**

### 坑：Destroy ≠ Release

`Destroy(gameObject)` 只销毁场景里的实例，**不触发引用计数释放**。不手动 Release，内存永远不释放。

```csharp
Destroy(instance);            // ✓ 实例从场景消失
Addressables.Release(handle); // ✓ 必须额外调用，否则内存不释放
```

### 坑：实例存活时不能释放所有 Handle

如果你加载了多个 handle，但已经用其中一个实例化了对象，此时必须至少保留 1 个 handle。否则引用计数归零，资产被卸载，场景里的实例**立刻变粉红（材质丢失）**。

**[配图：两张对比截图。左边 Cube 正常蓝色；右边 Cube 变粉红。下方说明：Handle 全部释放后资产被卸载]**

### 坑：AsyncOperationHandle 的 struct 相等性陷阱

`AsyncOperationHandle<T>` 是 struct，**不能用 `==` 可靠地比较两个 handle 是否指向同一资产**。应该用 List 管理所有 handle，用计数来判断释放时机，而不是依赖相等性比较。

### InstantiateAsync vs LoadAssetAsync + Instantiate

Addressables 提供了两种实例化方式，区别如下：

```csharp
// 方式一：一步完成，直接拿到场景中的实例
var instance = await Addressables.InstantiateAsync("DemoCube");
// 释放时：
Addressables.ReleaseInstance(instance); // 自动销毁实例 + 释放引用计数

// 方式二：先加载资源，再手动实例化
var handle = await Addressables.LoadAssetAsync<GameObject>("DemoCube");
var instance = Instantiate(handle.Result);
// 释放时需要两步：
Destroy(instance);
Addressables.Release(handle);
```

| | InstantiateAsync | LoadAssetAsync + Instantiate |
|---|---|---|
| 步骤 | 一步 | 两步 |
| 释放 | `ReleaseInstance(instance)` 一次搞定 | `Destroy` + `Release` 分开 |
| 适用场景 | 只需要一个实例 | 需要从同一资产创建多个实例，或需要持有资产引用 |

**[配图：两段代码并排对比，左边 InstantiateAsync 流程图（一步），右边 LoadAssetAsync 流程图（两步）]**

---

## 四、场景加载

Addressables 加载场景有两种模式：

| 模式 | 效果 |
|------|------|
| `LoadSceneMode.Additive` | 叠加加载，不卸载当前场景，多个场景共存 |
| `LoadSceneMode.Single` | 替换当前场景，自动卸载其他所有场景 |

```csharp
// 叠加加载
var handle = Addressables.LoadSceneAsync("SubSceneA", LoadSceneMode.Additive);

// 卸载（必须用当时的 handle）
Addressables.UnloadSceneAsync(handle);
```

⚠️ 卸载场景必须保存加载时返回的 handle，不能事后重新 LoadSceneAsync 再卸载。

**[配图：Unity Scene 视图截图，展示两个子场景叠加加载的效果，两个不同颜色的地板同时出现在场景中]**

---

## 五、远端加载与 CDN 热更新

### 完整代码流程

热更新分三步，每步都有对应的 API。

**第一步：检查是否有更新**

```csharp
private void OnCheckCatalogClick()
{
    _log.Log("CheckForCatalogUpdates() 开始...");
    Addressables.CheckForCatalogUpdates(false).Completed += handle =>
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _pendingCatalogs = handle.Result;
            _log.Log(_pendingCatalogs.Count == 0
                ? "Catalog 已是最新 ✓"
                : $"发现 {_pendingCatalogs.Count} 个 Catalog 需要更新 → 点「更新 Catalog」");
        }
        else
        {
            _log.Log($"检查失败 ✗ {handle.OperationException?.Message}");
        }
        Addressables.Release(handle);
    };
}
```

`CheckForCatalogUpdates` 返回的是**需要更新的 Catalog 数量**，不是 bundle 数量。项目只有一个远端 Catalog，所以结果只有 `0`（最新）或 `1`（有更新）。

**第二步：下载新 Catalog**

```csharp
private void OnUpdateCatalogClick()
{
    if (_pendingCatalogs == null || _pendingCatalogs.Count == 0)
    {
        _log.Log("无待更新 Catalog，请先点「检查 Catalog 更新」");
        return;
    }
    _log.Log("UpdateCatalogs() 开始...");
    Addressables.UpdateCatalogs(_pendingCatalogs, false).Completed += handle =>
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
            _log.Log("Catalog 更新完成 ✓ 现在可以加载最新 Remote 资源");
        else
            _log.Log($"更新失败 ✗ {handle.OperationException?.Message}");
        Addressables.Release(handle);
        _pendingCatalogs = null;
    };
}
```

**第三步：加载远端资产**

```csharp
private void OnLoadRemoteClick()
{
    if (_remoteHandle.IsValid())
    {
        _log.Log("Remote 资源已加载，请先「清理」再重新加载");
        return;
    }
    _log.Log("LoadAssetAsync<GameObject>(\"RemoteCube\") 从 OSS 加载...");
    _remoteHandle = Addressables.LoadAssetAsync<GameObject>("RemoteCube");
    _remoteHandle.Completed += h =>
    {
        if (h.Status == AsyncOperationStatus.Succeeded)
        {
            _remoteInstance = Instantiate(h.Result, Vector3.up * 2f, Quaternion.identity);
            _log.Log($"Remote 资源加载成功 ✓ {h.Result.name}");
        }
        else
        {
            _log.Log($"失败 ✗ {h.OperationException?.Message}");
        }
    };
}
```

**[配图：运行时截图，展示日志面板依次出现"检查中 → 发现1个更新 → 更新完成 → 加载成功"的完整流程]**

---

## 六、关键设置

### 1. Build Remote Catalog（必须开启）

**Window > Asset Management > Addressables > Settings → 勾选 Build Remote Catalog**

**[配图：Addressables Settings 窗口截图，红框圈出已勾选的 Build Remote Catalog 选项]**

不开启则不生成 `.hash` 文件，`CheckForCatalogUpdates` 永远返回"已是最新"，热更新完全失效。

### 2. Play Mode Script（编辑器测试热更新必改）

编辑器里默认是 **Use Asset Database**，直接读本地资产，完全不走 CDN。测试热更新时必须切换：

**Addressables Groups 顶部 → Play Mode Script → Use Existing Build**

| 选项 | 加载来源 | 说明 |
|------|---------|------|
| Use Asset Database | 工程资产文件 | 最快，完全不走 Addressables，开发调试用 |
| Simulate Groups | 工程资产文件 | 模拟分组和依赖，实际还是读本地 |
| **Use Existing Build** | 上次 Content Build 产物 | 走真实加载路径，Remote 组的资产去 CDN 下载 |

**[配图：Addressables Groups 窗口顶部截图，展示 Play Mode Script 下拉菜单，Use Existing Build 被选中]**

### 3. Build 流程

**[配图：Addressables Groups 菜单截图，展示 Build 子菜单，New Build 和 Update a Previous Build 两个选项]**

| | New Build | Update a Previous Build |
|---|---|---|
| 适用场景 | 首次发布 | 热更新内容 |
| Bundle 哈希 | 全部重新生成 | 只有变化的 bundle 变化 |
| 用户下载量 | 全部 bundle | 仅变化的 bundle |

**第一次 Build 用 New Build，之后热更用 Update a Previous Build**。New Build 虽然也能触发热更检测，但相当于让用户重新下载全部内容，失去热更意义。

### 4. Profile 配置

`Remote.LoadPath` 填写 CDN 地址，**末尾不能加 `/`**（原因见下方踩坑第 3 条）：

```
✓ 正确：https://static0.xesimg.com/addressables/Data
✗ 错误：https://static0.xesimg.com/addressables/Data/
```

---

## 七、踩坑全记录

### 坑 1：Cube 上出现房间反射贴图

**现象**：加载出来的 Cube 表面出现房间镜像，像个镜面球  
**原因**：使用了 Standard Shader，开启了 GlossyReflections，会自动采样场景环境 cubemap  
**解决**：Demo 展示用 `Unlit/Color` Shader，无光照无反射，只显示纯色

**[配图：两张对比截图。左：Cube 表面出现房间反射（Standard Shader）；右：Cube 显示纯黄色（Unlit/Color Shader）]**

---

### 坑 2：Check 永远显示"已是最新"

**现象**：明明改了资产、重新 Build 并上传了 CDN，点检查还是返回"已是最新"  
**原因**：**Build Remote Catalog 没有勾选**，没有生成 `.hash` 文件，没有 hash 就没法做版本对比，`CheckForCatalogUpdates` 永远返回空列表  
**解决**：Window > Asset Management > Addressables > Settings → 勾选 Build Remote Catalog → 重新 New Build

**[配图：Addressables Settings 截图，红框标注 Build Remote Catalog 复选框，显示未勾选状态（错误示例）]**

---

### 坑 3：CDN 请求出现双斜杠 `//`

**现象**：Unity 发出的请求是 `https://cdn.com/Data//catalog_0.1.json`（双斜杠）  
**原因**：`Remote.LoadPath` 末尾带 `/`，Addressables 拼接文件名时又自动加了 `/`

```
Remote.LoadPath = "https://cdn.com/Data/"
拼接后 → https://cdn.com/Data//catalog_0.1.json  ← 双斜杠！
```

**后果**：CDN 把 `Data/catalog_0.1.json` 和 `Data//catalog_0.1.json` 视为**两个不同的缓存资源**。你上传的是单斜杠路径，双斜杠命中的是旧的 CDN 缓存，导致始终拿到旧版 catalog

**[配图：HTTP 请求日志截图，红框圈出 URL 中的双斜杠 `//`]**

**解决**：`Remote.LoadPath` 末尾去掉 `/`

**[配图：Profile 窗口截图，对比修改前（有斜杠）和修改后（无斜杠）的 Remote.LoadPath 值]**

---

### 坑 4：Catalog 缓存污染

**现象**：CDN 内容已更新，bundle 文件正确，但加载时报"找不到旧 bundle hash"  
**原因**：`Application.persistentDataPath/com.unity.addressables/` 里有旧 catalog，Addressables 初始化时优先使用这个缓存而非重新下载  
**解决**：清理时需要同时清除两种缓存

```csharp
// Bundle 缓存（只清这个是不够的！）
Caching.ClearCache();

// Catalog 缓存（必须额外清）
var catalogCache = Path.Combine(Application.persistentDataPath, "com.unity.addressables");
if (Directory.Exists(catalogCache))
    Directory.Delete(catalogCache, true);
```

---

### 坑 5：清缓存后必须重启 Play 模式

**现象**：点了"清理"按钮，但加载结果还是旧的  
**原因**：Addressables 初始化发生在 `MonoBehaviour.Start()` 之前，旧 Catalog 已经加载进内存。在 Play 模式内删文件，不影响内存里已加载的 Catalog  
**正确操作顺序**：

```
点「清理」→ 停止 Play 模式 → 重新进入 Play 模式 → 加载
```

**[配图：操作步骤示意图。Step1: 点清理按钮；Step2: 点 Unity 停止按钮；Step3: 点 Unity 播放按钮；Step4: 点加载按钮。用带序号的箭头连接]**

---

### 坑 6：两种缓存傻傻分不清

Addressables 有两套完全独立的缓存机制：

| 缓存类型 | 存储内容 | 路径 | 清除方法 |
|--------|---------|------|--------|
| Bundle 缓存 | 下载的 .bundle 文件 | `Caching.defaultCache.path` | `Caching.ClearCache()` |
| Catalog 缓存 | 下载的 catalog.json | `persistentDataPath/com.unity.addressables/` | 手动删目录 |

`Caching.ClearCache()` 只清 bundle 缓存，对 Catalog 缓存完全无效。很多热更新测试反复失败就是因为 Catalog 缓存没清。

---

### 坑 7：CDN 上传后必须手动刷新缓存

**现象**：已上传新文件到 OSS，Unity 下载到的仍是旧内容  
**原因**：CDN 边缘节点有独立缓存，OSS 源站更新后 CDN 不立即同步  
**解决**：每次上传后，在**阿里云 OSS CDN 控制台 → 刷新预热**，手动提交对应 URL 的缓存刷新请求

**[配图：阿里云 OSS 控制台"刷新预热"页面截图，输入框中填写了 catalog 和 bundle 的 CDN URL]**

---

### 坑 8：Group 已存在时 Schema 路径不更新

**现象**：重新跑 Editor Setup 脚本后，Remote bundle 依然构建到 Library 而非 ServerData  
**原因**：Setup 脚本用 `FindGroup` 找到已有 group 就直接返回，没有重新设置 BuildPath/LoadPath  
**解决**：无论 group 是否已存在，都强制更新一次 Schema 路径配置

```csharp
var g = s.FindGroup(name) ?? s.CreateGroup(name, ...);
if (remote) // 无论如何都设置，不跳过
{
    var schema = g.GetSchema<BundledAssetGroupSchema>();
    schema.BuildPath.SetVariableByName(s, AddressableAssetSettings.kRemoteBuildPath);
    schema.LoadPath.SetVariableByName(s, AddressableAssetSettings.kRemoteLoadPath);
}
```

---

### 坑 9：Destroy 不释放 Handle

**现象**：`Destroy(instance)` 后内存仍然占用，重复加载越来越卡  
**原因**：销毁 GameObject 实例不会触发 Addressables 的引用计数释放  
**解决**：销毁实例和释放 Handle 是两个独立操作，必须都做

```csharp
Destroy(instance);            // 只管场景
Addressables.Release(handle); // 只管内存
```

---

### 坑 10：重复资源（Duplicated Assets）

**现象**：包体或内存意外翻倍——两个 Remote Group 都引用了同一张贴图（如 stone.png），打出来的两个 bundle 里各有一份完整拷贝  
**原因**：stone.png 没有被添加到任何 Addressables Group（称为 Implicit 隐式资源）。打包时 Unity 会在每个引用它的 bundle 里各内嵌一份完整拷贝

**[配图：Addressables Analyze 窗口截图，展示 Check for Duplicate Assets 规则，列出被多个 Bundle 各自包含的 Implicit 资产]**

**发现方法**：`Window > Asset Management > Addressables > Analyze` → 运行 **Check for Duplicate Assets** 规则，被标记为 Implicit 且出现在多个 Bundle 中的资产即为重复

**解决**：把 stone.png 也变成 Explicit（显式）资源，放到独立的 `SharedTextures` Group，让其他 bundle 引用这个共享 bundle，而不是各自内嵌。Pack Mode 选 **Pack Together**（所有共享资产合成一个 bundle）或 **Pack Separately**（每个资产各自一个 bundle）均可。

| | Pack Together | Pack Separately |
|---|---|---|
| 下载粒度 | 整包，一次下全部共享资产 | 按需，只下载实际用到的 |
| 热更新效率 | 任意一张贴图变更 → 整个 SharedTextures bundle 重新下载 | 只有变更的那张贴图对应的 bundle 重新下载 |
| HTTP 请求数 | 少 | 多（每个资产一个请求） |
| 适合场景 | 共享资产总是一起用 | 共享资产分散使用，希望精细控制下载量 |

**自动依赖与复用**：无论哪种模式，GroupA 加载时 Addressables 都会自动分析依赖链，发现引用了 stone.png 就自动下载并加载对应 bundle，无需手动处理。GroupB 加载时 stone.bundle 已在内存，引用计数 +1 直接复用，不重新下载。最后一个持有者释放后引用计数归零，bundle 才从内存卸载。

```
修复前：
  GroupA.bundle → Hero Prefab + stone.png（副本1）
  GroupB.bundle → Boss Prefab + stone.png（副本2）

修复后：
  GroupA.bundle → Hero Prefab（引用 SharedTextures.bundle）
  GroupB.bundle → Boss Prefab（引用 SharedTextures.bundle）
  SharedTextures.bundle → stone.png（唯一一份）
```

---

## 八、完整热更新操作流程

**[配图：完整流程图，分"首次发布"和"热更新"两个泳道，步骤用带序号的方框连线展示]**

**首次发布：**
1. Groups > Build > **New Build** > Default Build Script
2. 上传 `ServerData/[Platform]/` 全部文件到 CDN
3. 阿里云 OSS 控制台刷新 CDN 缓存

**内容热更新：**
4. 修改资产（改材质颜色、换模型等）
5. Groups > Build > **Update a Previous Build**
6. 上传 `ServerData/[Platform]/` 更新的文件到 CDN
7. 阿里云 OSS 控制台刷新 CDN 缓存

**编辑器验证：**
8. 点「清理」→ 停止 Play 模式 → 重新进入 Play 模式
9. 点「检查 Catalog 更新」→ 显示"发现 1 个需要更新"
10. 点「更新 Catalog」→ 点「加载 Remote 资源」→ 看到新内容 ✓

---

## 总结

几个最关键的点，出问题先对这张表：

| 症状 | 原因 | 解决 |
|------|------|------|
| Check 永远显示最新 | Build Remote Catalog 未勾选 | Settings 里勾上，重新 New Build |
| CDN 拿到旧 catalog | URL 双斜杠，命中旧缓存 | Remote.LoadPath 末尾去掉 `/` |
| 清缓存没效果 | 只清了 bundle 缓存 | 同时清 persistentDataPath/com.unity.addressables/ |
| 清缓存后还是旧的 | 旧 catalog 已在内存 | 清理后重启 Play 模式 |
| 实例材质变粉红 | Handle 全部释放，引用计数归零 | 实例存活时保留至少 1 个 Handle |
| 上传后 CDN 还是旧的 | CDN 边缘节点缓存 | OSS 控制台手动刷新 URL 缓存 |
| Remote bundle 跑到 Library | Group Schema 路径未更新 | Setup 脚本里强制每次重设 Schema 路径 |
| 包体/内存意外翻倍 | 隐式依赖资源被多个 Bundle 各自打包 | Analyze 窗口运行 Check for Duplicate Assets，添加 SharedTextures Group |

---

## 尾声：按需加载够用吗？

本文所有示例用的都是 `LoadAssetAsync` 按需加载——点按钮，资产从 CDN 下载，显示在屏幕上。对于演示项目，这完全够用。

但如果你做的是对局类游戏，进入对局后才开始下载英雄模型，网络延迟 1-2 秒，模型还没到对局已经开始——这是不可接受的。王者荣耀那种"进入前下载 XXX MB"的方案，用的是另一个 API：`DownloadDependenciesAsync`。

| | 按需加载（LoadAssetAsync） | 预下载（DownloadDependenciesAsync） |
|---|---|---|
| 下载时机 | 用到时才下载 | 进入关卡前统一下载 |
| 用户感知 | 可能有加载延迟 | 进入前统一等待，进入后秒开 |
| 运行时网络依赖 | 有 | 无（全走本地缓存） |
| 适合场景 | 非时间敏感资源 | 对局、关卡等必须即时可用的场景 |

`DownloadDependenciesAsync` 只把 bundle 下载到磁盘缓存，不加载进内存。配合 `GetDownloadSizeAsync` 告诉用户需要下载多少字节，再配合 `PercentComplete` 做进度条，就是完整的"进关卡前检查更新"流程。

**下一篇**会从零跑通这套预下载流程：检查大小 → 用户确认 → 带进度条下载 → 进入场景。

---

*示例工程代码见文末链接。*
