# Chapter 09 多 Key 加载 + MergeMode 设计

## 目标

演示 `LoadAssetsAsync` 传入多个 Key 时，`MergeMode.Union`（并集）和 `MergeMode.Intersection`（交集）的区别，用 3 个颜色不同的 Cube 做可视化演示。

## 资产设计

新建 3 个本地 Prefab，分配不同 Label 组合：

| 资产 | Label | 颜色 | 含义 |
|------|-------|------|------|
| Ch09CubeAlpha.prefab | SetA | 红色 | 只在 SetA |
| Ch09CubeBeta.prefab | SetA + SetB | 黄色 | SetA 和 SetB 的交集 |
| Ch09CubeGamma.prefab | SetB | 蓝色 | 只在 SetB |

**Group：LocalContent**（无需 CDN，专注 API 演示）

预期加载结果：

| 操作 | 结果 | 数量 |
|------|------|------|
| 加载 SetA | Alpha + Beta | 2 |
| 加载 SetB | Beta + Gamma | 2 |
| Union(SetA \| SetB) | Alpha + Beta + Gamma | 3 |
| Intersection(SetA ∩ SetB) | Beta（黄） | 1 |

## 场景 UI

文件：`Assets/_Chapters/Chapter09_MergeMode/Chapter09Scene.unity`

```
[ 加载 SetA ]         [ 加载 SetB ]
[ Union(SetA|SetB) ]  [ Intersection(SetA∩SetB) ]
[ 清理 ]              [ 清空日志 ]

[ 日志面板 ]
```

## Manager 逻辑

文件：`Assets/_Chapters/Chapter09_MergeMode/Chapter09Manager.cs`

```csharp
// 单 Label 加载（复用 Ch05 的 LoadAssetsAsync 模式）
private void OnLoadSetAClick()
{
    var handle = Addressables.LoadAssetsAsync<GameObject>("SetA", null);
    handle.Completed += h => SpawnAll(h.Result, "SetA");
    _handles.Add(handle);
}

// 多 Key Union（并集）
private void OnUnionClick()
{
    var keys = new List<object> { "SetA", "SetB" };
    var handle = Addressables.LoadAssetsAsync<GameObject>(
        keys, null, Addressables.MergeMode.Union);
    handle.Completed += h => SpawnAll(h.Result, "Union");
    _handles.Add(handle);
}

// 多 Key Intersection（交集）
private void OnIntersectionClick()
{
    var keys = new List<object> { "SetA", "SetB" };
    var handle = Addressables.LoadAssetsAsync<GameObject>(
        keys, null, Addressables.MergeMode.Intersection);
    handle.Completed += h => SpawnAll(h.Result, "Intersection");
    _handles.Add(handle);
}

// 排列实例：横向等距排开
private void SpawnAll(IList<GameObject> prefabs, string source)
{
    float spacing = 2.5f;
    float startX = -(prefabs.Count - 1) * spacing / 2f;
    for (int i = 0; i < prefabs.Count; i++)
        _instances.Add(Instantiate(prefabs[i],
            new Vector3(startX + i * spacing, 0, 0), Quaternion.identity));
    _log.Log($"{source} → {prefabs.Count} 个资产");
}
```

清理时遍历 `_handles` 依次 Release，销毁所有 `_instances`。

## Setup 脚本改动

`StudyProjectSetup.cs` 新增：

1. `CreateChapter09Assets()`：创建 3 个 Prefab（Unlit/Color，纯色方块）
2. `ConfigureAddressables()` 添加 SetA / SetB Label，Tag 三个资产：
   - Alpha → SetA label
   - Beta → SetA + SetB label
   - Gamma → SetB label
   - 三者放 LocalContent 组
3. `CreateChapter09Scene()`：按上方 UI 布局建场景，挂 Chapter09Manager
4. `SetupAll()` 添加对应调用

## 新增 API 对照

| API | 说明 |
|-----|------|
| `LoadAssetsAsync<T>(IList<object> keys, callback, MergeMode)` | 按多个 Key 批量加载 |
| `MergeMode.Union` | 匹配任意一个 Key 的资产都加载（并集） |
| `MergeMode.Intersection` | 只加载同时匹配所有 Key 的资产（交集） |
| `MergeMode.UseFirst` | 只使用第一个 Key 的结果（等价于单 Key 加载） |
