# Chapter 02 — Handle 与内存管理

## 学习目标

- 理解 `AsyncOperationHandle` 的生命周期
- 正确使用 `Addressables.Release(handle)` 释放资源
- 区分「Destroy 实例」和「Release Handle」的区别

## 场景操作

打开 `Chapter02Scene`，按 Play，按照以下顺序点按钮：

1. 点「加载 (LoadAssetAsync)」3次 → Handle 数: 3
2. 点「实例化 (Instantiate)」→ Cube 出现
3. 点「销毁实例 (Destroy)」→ Cube 消失，但注意日志："Destroy 不会释放 Handle"
4. 点「释放 Handle (Release)」→ Handle 数 -1

## 关键概念：引用计数

Addressables 用**引用计数**管理内存。每次 `LoadAssetAsync` 计数 +1，每次 `Release` 计数 -1。计数归零时资源才真正从内存卸载。

```
LoadAssetAsync  → 引用计数: 1
LoadAssetAsync  → 引用计数: 2
Release(handle1) → 引用计数: 1  （资源还在内存，第二个 handle 还有效）
Release(handle2) → 引用计数: 0  （资源卸载）
```

## 常见陷阱

**只 Destroy 不 Release：** GameObject 从场景移除，但 Addressable Bundle 仍然占用内存。

**只 Release 不 Destroy：** Handle 释放，但 GameObject 的 Renderer 引用变成 missing，可能报错。

正确做法：先 `Destroy(gameObject)`，再 `Addressables.Release(handle)`。  
或者用 `Addressables.ReleaseInstance(gameObject)` 同时完成两件事（适用于 `InstantiateAsync` 创建的实例）。
