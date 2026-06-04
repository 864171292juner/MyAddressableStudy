# Unity Addressables 完整笔记

> 涵盖核心概念、热更新流程、关键设置、以及实践中踩过的所有坑。

---

## 一、核心概念

### Address（地址）
给每个可寻址资产分配一个字符串 key，运行时通过这个 key 加载，与文件路径解耦。

```csharp
Addressables.LoadAssetAsync<GameObject>("DemoCube");
```

### Label（标签）
一个资产可以打多个 Label。按 Label 加载时，返回的是 Catalog 里该 Label 下第一个 entry 的资产（**不保证顺序**，不能靠文件名排序推断结果）。

### Group（组）
资产分组决定打包策略。每个 Group 生成一个 AssetBundle。  
- **Local Group**：bundle 打到 app 包内（Library → StreamingAssets）  
- **Remote Group**：bundle 上传到 CDN，运行时按需下载

### Catalog（目录）
记录所有资产的地址、所在 bundle、bundle 路径的 JSON 文件。  
运行时 Addressables 先加载 Catalog，再根据 Catalog 找到实际 bundle 文件。

### AsyncOperationHandle\<T\>
加载操作的句柄。持有句柄 = 持有资产引用计数。  
**引用计数规则**：
- 每次 `LoadAssetAsync` 引用计数 +1（即使加载同一资产）  
- `Addressables.Release(handle)` 引用计数 -1  
- 引用计数归零 → 资产从内存卸载 → 已 Instantiate 的实例材质丢失

---

## 二、Handle 与内存管理

### 正确姿势
```csharp
// 加载（引用计数 +1）
var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");

// 实例化（不增加引用计数，使用 handle.Result）
var instance = Instantiate(handle.Result);

// 销毁实例（不释放 handle！）
Destroy(instance);

// 释放 handle（引用计数 -1）
Addressables.Release(handle);
```

### 关键注意事项
- `Destroy(instance)` **不会**自动释放 handle，必须手动 `Release`
- 实例存活时至少保留 1 个 handle，否则资产被卸载，实例材质变粉红/丢失
- 同一资产加载多次 → 多个 handle，但它们共享同一份引用计数
- `AsyncOperationHandle` 是 struct，**不能用 `==` 比较两个 handle 是否指向同一资产**，应用计数管理而非比较

### 多 Handle 释放策略（同一资产）
```csharp
// 有实例时必须保留至少 1 个 handle
int mustKeep = _instances.Count > 0 ? 1 : 0;
for (int i = _handles.Count - 1; i >= mustKeep; i--)
{
    Addressables.Release(_handles[i]);
    _handles.RemoveAt(i);
}
```

---

## 三、场景加载

| 模式 | 行为 |
|------|------|
| `LoadSceneMode.Additive` | 叠加加载，不卸载当前场景 |
| `LoadSceneMode.Single` | 替换当前场景（自动卸载其他场景） |

```csharp
var handle = Addressables.LoadSceneAsync("SubSceneA", LoadSceneMode.Additive);
Addressables.UnloadSceneAsync(handle);
```

---

## 四、远端加载与热更新

### 热更新完整流程

```
[开发者]
1. 修改资产
2. Groups > Build > Update a Previous Build（推荐）
   或 New Build（全量重建）
3. 上传 ServerData/[Platform]/ 到 CDN
4. 在阿里云 OSS 控制台刷新 CDN 缓存

[用户端运行时]
5. CheckForCatalogUpdates()  → 对比 CDN .hash 与本地版本
6. UpdateCatalogs()          → 下载新 Catalog 到 persistentDataPath
7. LoadAssetAsync()          → 按新 Catalog 下载新 bundle
```

### New Build vs Update a Previous Build

| | New Build | Update a Previous Build |
|---|---|---|
| 用途 | 首次发布 / 大版本 | 热更新内容 |
| bundle 哈希 | 全部重新生成 | 只有变化的 bundle 哈希变化 |
| 用户下载量 | 全部 bundle | 仅变化的 bundle |
| catalog | 全量更新 | 仅记录变化 |

**热更新演示应使用 `Update a Previous Build`**，这才是生产环境真实做法。

### CheckForCatalogUpdates 返回值
返回需要更新的 **catalog 数量**，不是 bundle 数量。项目只有一个远端 catalog，所以结果只有 `0`（最新）或 `1`（有更新）。

### 两种缓存的区别
| 缓存类型 | 路径 | 清除方法 |
|--------|------|--------|
| Bundle 缓存 | `Caching.defaultCache.path` | `Caching.ClearCache()` |
| Catalog 缓存 | `Application.persistentDataPath/com.unity.addressables/` | 手动删目录 或代码删除 |

**`Caching.ClearCache()` 只清 bundle 缓存，不清 catalog 缓存！**

---

## 五、关键设置

### 1. Profile 配置
**Window > Asset Management > Addressables > Profiles**

| 字段 | 值 | 注意 |
|------|----|------|
| Remote.BuildPath | `ServerData/[BuildTarget]` | bundle 构建输出路径 |
| Remote.LoadPath | `https://your-cdn.com/Data` | **末尾不能有 `/`** |

### 2. Build Remote Catalog（必须开启）
**Window > Asset Management > Addressables > Settings**  
→ 勾选 **Build Remote Catalog**

不开启则不生成 `.hash` 文件，`CheckForCatalogUpdates` 永远返回"已是最新"，热更新无法工作。

### 3. Play Mode Script
**Window > Asset Management > Addressables > Groups > Play Mode Script**

| 模式 | 用途 |
|------|------|
| Use Asset Database | 开发调试，不使用 bundle |
| Simulate Groups | 模拟 Addressables 行为 |
| **Use Existing Build** | 使用实际构建的 bundle，测试热更新时必须用这个 |

### 4. Group Schema（代码配置）
每次运行 Setup 时必须**强制更新** Remote group 的 schema，不能因为 group 已存在就跳过：

```csharp
// 错误做法：FindGroup 找到就直接返回，不更新路径
var g = s.FindGroup(name) ?? s.CreateGroup(...);

// 正确做法：无论 group 是否存在，都更新 schema
var g = s.FindGroup(name) ?? s.CreateGroup(name, ...);
if (remote)
{
    var schema = g.GetSchema<BundledAssetGroupSchema>();
    schema.BuildPath.SetVariableByName(s, AddressableAssetSettings.kRemoteBuildPath);
    schema.LoadPath.SetVariableByName(s, AddressableAssetSettings.kRemoteLoadPath);
}
```

---

## 六、踩坑记录

### 坑 1：Standard Shader 显示环境反射纹理
**现象**：Cube 上显示房间/场景的反射贴图，而不是纯色  
**原因**：Standard shader `_GlossyReflections: 1` + `_Glossiness: 0.5` 会采样环境 cubemap  
**解决**：Demo 用 `Unlit/Color` shader，完全无光照无反射

### 坑 2：Build Remote Catalog 未开启
**现象**：`CheckForCatalogUpdates` 每次都返回"已是最新"，无法触发热更新  
**原因**：没有生成 `.hash` 文件，无法对比版本  
**解决**：在 Addressables Settings 里勾选 **Build Remote Catalog**

### 坑 3：Remote.LoadPath 末尾有斜杠导致双斜杠 URL
**现象**：Addressables 请求 `https://cdn.com/Data//catalog_0.1.json`（双斜杠）  
**原因**：`Remote.LoadPath = "https://cdn.com/Data/"` 末尾带 `/`，Addressables 拼接文件名时又加 `/`  
**后果**：CDN 将单斜杠和双斜杠视为不同资源缓存，双斜杠 URL 可能返回旧版内容  
**解决**：`Remote.LoadPath` 末尾不加 `/`

### 坑 4：Catalog 缓存污染（persistentDataPath）
**现象**：CDN 内容已更新，但加载时仍报旧 bundle hash 找不到  
**原因**：`Application.persistentDataPath/com.unity.addressables/` 里有旧 catalog，Addressables 初始化时优先使用缓存而非重新下载  
**解决**：清理时同时删除 catalog 缓存目录

```csharp
Caching.ClearCache(); // 清 bundle 缓存
var catalogCache = Path.Combine(Application.persistentDataPath, "com.unity.addressables");
if (Directory.Exists(catalogCache))
    Directory.Delete(catalogCache, true);
```

**重要**：清理后必须**停止并重新进入 Play 模式**，内存中的旧 catalog 才会失效。在当前 Play 会话内清理不影响已加载到内存的 catalog。

### 坑 5：Addressables 初始化早于 Start()
**现象**：`Addressables.ResourceManager.WebRequestOverride` 在 `Start()` 里设置，但初始化的 HTTP 请求没有被拦截  
**原因**：`Addressables.InitializeAsync()` 在 MonoBehaviour.Start() 之前自动触发  
**影响**：调试时需要注意，初始化阶段的网络请求无法通过 Start() 里的 Override 捕获

### 坑 6：Group 已存在时 Schema 不更新
**现象**：`New Build` 后 bundle 仍然生成到 Library 而非 ServerData  
**原因**：Setup 脚本用 `FindGroup` 找到已有 group 就直接返回，没有更新 Remote Schema 的 BuildPath/LoadPath  
**解决**：见坑 3 代码，无论 group 存在与否都强制设置 schema 路径

### 坑 7：CDN 缓存刷新必须在控制台操作
**现象**：上传新文件到 OSS 后，Unity 仍然下载到旧内容  
**原因**：CDN 边缘节点有缓存，OSS 更新后 CDN 不会立即同步  
**解决**：每次上传后，在**阿里云 OSS CDN 控制台**手动刷新对应 URL 的缓存

### 坑 8：AsyncOperationHandle struct 相等性不可靠
**现象**：用 `==` 或 Dictionary 查找 handle 时结果异常  
**原因**：`AsyncOperationHandle<T>` 是 struct，同一资产的两次 LoadAssetAsync 返回的 handle 无法用 `==` 可靠比较  
**解决**：改用 List + 计数管理，不依赖 handle 相等性比较

### 坑 9：Destroy 不释放 Handle
**现象**：`Destroy(instance)` 后内存仍然占用  
**原因**：销毁 GameObject 实例不会触发 Addressables 的引用计数释放  
**解决**：销毁实例和释放 handle 是两个独立操作，必须都做

---

## 七、典型工作流（热更新演示）

```
1. New Build（首次）→ 上传 ServerData/ 到 CDN → 刷新 CDN 缓存
2. 修改资产（如材质颜色）
3. Update a Previous Build → 上传新文件到 CDN → 刷新 CDN 缓存
4. 清空 bundle + catalog 缓存（点「清理」按钮）
5. 停止 Play 模式 → 重新进入 Play 模式
6. 点「检查 Catalog 更新」→ 应显示"发现 1 个需要更新"
7. 点「更新 Catalog」
8. 点「加载 Remote 资源」→ 看到新材质
```
