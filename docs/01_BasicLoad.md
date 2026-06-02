# Chapter 01 — 基础加载

## 学习目标

- 在 Inspector 中将资源标记为 Addressable 并设置 Address
- 用 `Addressables.LoadAssetAsync<T>` 通过字符串地址加载资源
- 理解 `AsyncOperationHandle` 和 `AsyncOperationStatus`

## 场景操作

打开 `Chapter01Scene`，按 Play：

1. 点「加载 Cube Prefab」→ 场景中出现青色 Cube，日志显示"成功 ✓"
2. 点「加载 Sprite」→ UI 中间显示绿色方块
3. 点「清理场景」→ Cube 和 Sprite 消失，Handle 被释放

## 关键代码解析

```csharp
// 发起异步加载，立即返回 handle（此时加载还未完成）
var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");

// 注册完成回调
handle.Completed += h =>
{
    if (h.Status == AsyncOperationStatus.Succeeded)
    {
        // h.Result 是加载到的 GameObject（未实例化）
        Instantiate(h.Result);
    }
};
```

## 常见问题

**问：为什么不直接用 `Resources.Load`？**
答：`Resources.Load` 是同步的，会阻塞主线程。资源越大、卡顿越明显。`Addressables` 是异步的，不影响帧率，而且支持 Remote 加载。

**问：加载完不 Release 会怎样？**
答：资源留在内存里，引用计数不归零，即使场景切换也不会被卸载。长期不 Release 会导致内存泄漏。
