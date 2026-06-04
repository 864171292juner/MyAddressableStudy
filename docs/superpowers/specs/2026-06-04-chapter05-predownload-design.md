# Chapter 05 预下载流程设计

## 目标

演示 `GetDownloadSizeAsync` + `DownloadDependenciesAsync` + `LoadAssetsAsync`（复数）的 API 用法，让每个 API 对应一个按钮操作，循序渐进理解预下载流程。

## 资产与分组

新建 3 个颜色不同的 Prefab，放入专属远端 Group：

| 资产 | Address | 颜色 |
|------|---------|------|
| Ch05CubeA.prefab | Ch05CubeA | 橙色 |
| Ch05CubeB.prefab | Ch05CubeB | 紫色 |
| Ch05CubeC.prefab | Ch05CubeC | 绿色 |

- Group 名：`Chapter05Remote`
- BuildPath / LoadPath：与 Ch04 相同（Remote）
- 三个资产统一打上 Label：`Chapter05`
- 资产路径：`Assets/_Chapters/Chapter05_PreDownload/DemoAssets/`

## 场景与 UI

文件：`Assets/_Chapters/Chapter05_PreDownload/Chapter05Scene.unity`

```
[ 检查下载大小 ]    [ 预下载 ]
[ 加载资产 ]        [ 清理 ]
[ 清除 Bundle 缓存 ][ 清空日志 ]

下载进度: --        （GameObject 名 "ProgressText"，预下载期间实时更新）

[ 日志面板 ]
```

## Manager 逻辑

文件：`Assets/_Chapters/Chapter05_PreDownload/Chapter05Manager.cs`

### 检查下载大小

```csharp
Addressables.GetDownloadSizeAsync("Chapter05").Completed += handle => {
    long size = handle.Result;
    _log.Log(size == 0
        ? "所有资产已缓存，无需下载 ✓"
        : $"需要下载 {size} bytes，点「预下载」开始");
    Addressables.Release(handle);
};
```

### 预下载（带进度）

```csharp
var handle = Addressables.DownloadDependenciesAsync("Chapter05");
StartCoroutine(TrackProgress(handle));
handle.Completed += h => {
    _log.Log(h.Status == AsyncOperationStatus.Succeeded
        ? "预下载完成 ✓ 可以加载资产"
        : $"下载失败 ✗ {h.OperationException?.Message}");
    Addressables.Release(h);
};

IEnumerator TrackProgress(AsyncOperationHandle handle) {
    while (!handle.IsDone) {
        _progressText.text = $"下载进度: {handle.PercentComplete * 100:F0}%";
        yield return null;
    }
    _progressText.text = "下载进度: 100%";
}
```

### 加载资产（LoadAssetsAsync 复数）

```csharp
// 注意：LoadAssetsAsync（复数）是本章新 API，一次加载 Label 下所有资产
Addressables.LoadAssetsAsync<GameObject>("Chapter05", null).Completed += handle => {
    foreach (var prefab in handle.Result) {
        // 排成一排显示
    }
    _loadHandle = handle;
};
```

LoadAssetsAsync 与 LoadAssetAsync 的区别：
- `LoadAssetAsync<T>(key)` → 加载单个资产，返回 `AsyncOperationHandle<T>`
- `LoadAssetsAsync<T>(key, callback)` → 加载 Label 下所有资产，返回 `AsyncOperationHandle<IList<T>>`

### 清理

```csharp
// 销毁所有实例
foreach (var inst in _instances) Destroy(inst);
_instances.Clear();
// 释放 Handle
if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
```

## Setup 脚本改动

`StudyProjectSetup.cs` 新增：

1. `CreateChapter05Assets()`：创建 3 个 Prefab + 材质
2. `GetOrCreateGroup(s, "Chapter05Remote", remote: true)`
3. `Tag()` 三个资产，同时调用 `entry.SetLabel("Chapter05", true, true)` 打 Label
4. `CreateChapter05Scene()`：按照上方 UI 布局建场景，挂 Chapter05Manager

## 新增 API 对照

| API | 本章用途 |
|-----|---------|
| `GetDownloadSizeAsync(key)` | 检查 Label 下所有资产的待下载字节数 |
| `DownloadDependenciesAsync(key)` | 预下载到磁盘缓存，不加载进内存 |
| `handle.PercentComplete` | 实时读取下载进度（0.0 ~ 1.0） |
| `LoadAssetsAsync<T>(key, callback)` | 按 Label 一次加载多个资产到内存 |
