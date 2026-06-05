# Chapter 07 async/await 写法设计

## 目标

演示用 `async/await` 替代 `.Completed +=` 回调的现代写法，同一场景放两个按钮，让读者直观看到两种风格的代码差异。

## 核心对比

```csharp
// 旧：回调风格（Ch01 用的方式）
void OnCallbackLoad()
{
    var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
    handle.Completed += h =>
    {
        if (h.Status == AsyncOperationStatus.Succeeded)
            Instantiate(h.Result, new Vector3(-2, 0, 0), Quaternion.identity);
        else
            _log.Log($"失败 {h.OperationException?.Message}");
    };
}

// 新：async/await 风格
async void OnAwaitLoad()
{
    var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
    try
    {
        var prefab = await handle.Task;
        Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity);
    }
    catch (Exception e)
    {
        _log.Log($"失败 {e.Message}");
    }
}
```

不依赖 UniTask，使用 Unity 原生 `AsyncOperationHandle.Task`（返回 `Task<T>`）。

## 资产

复用 Ch01 DemoCube，不新建资产。

## 场景 UI

文件：`Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Scene.unity`

```
[ 回调方式加载 ]      [ async/await 加载 ]
[ 清理 ]              [ 清空日志 ]
StatusText: "状态: 就绪"

[ 日志面板 ]
```

## Manager 逻辑

文件：`Assets/_Chapters/Chapter07_AsyncAwait/Chapter07Manager.cs`

- `_callbackHandle`：回调方式加载的 handle，实例放在 x=-2
- `_awaitHandle`：await 方式加载的 handle，实例放在 x=+2
- 两个实例可同时存在，清理时各自 Release
- StatusText 显示当前状态（"加载中..."、"完成 ✓"、"失败 ✗"）
- 错误处理：回调用 `h.Status` 判断；await 用 `try/catch`

```csharp
private async void OnAwaitLoadClick()
{
    if (_awaitHandle.IsValid()) { _log.Log("已加载，请先「清理」"); return; }
    SetStatus("加载中...");
    _awaitHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
    try
    {
        var prefab = await _awaitHandle.Task;
        _awaitInstance = Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity);
        SetStatus("完成 ✓");
        _log.Log("await 加载完成");
    }
    catch (Exception e)
    {
        SetStatus("失败 ✗");
        _log.Log($"await 加载失败: {e.Message}");
        Addressables.Release(_awaitHandle);
        _awaitHandle = default;
    }
}
```

## Setup 脚本改动

`StudyProjectSetup.cs` 新增：
1. `CreateChapter07Scene()`：按 UI 布局建场景，挂 Chapter07Manager
2. `SetupAll()` 添加调用

## 注意事项

- `async void` 仅用于事件回调入口（Unity 按钮回调）；内部异步逻辑推荐用 `async Task`
- `await handle.Task` 在主线程继续执行（Unity SynchronizationContext 保证）
- 错误处理：await 方式下 `AsyncOperationException` 会被 try/catch 捕获；回调方式需判断 `h.Status`
