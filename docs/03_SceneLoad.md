# Chapter 03 — 场景加载

## 学习目标

- 用 `Addressables.LoadSceneAsync` 动态加载场景
- 区分 `Additive`（叠加）和 `Single`（替换）加载模式
- 用 `Addressables.UnloadSceneAsync` 卸载场景并释放内存

## 场景操作

打开 `Chapter03MainScene`，按 Play：

1. 点「Additive 加载 SubSceneA」→ 蓝色地板出现
2. 点「Additive 加载 SubSceneB」→ 紫色地板也出现（两个场景同时存在）
3. 点「卸载 SubSceneA」→ 蓝色地板消失
4. 点「Single 加载 SubSceneA」→ 当前所有场景被替换（主场景也消失）
   > **注意：** Single 模式会销毁当前场景（包括 Chapter03Manager 本身），场景切换后 UI 会消失，这是正常现象。重新在 Editor 中打开 Chapter03MainScene 即可恢复。

## 关键代码解析

```csharp
// Additive 加载：新场景叠加在当前场景之上
var handle = Addressables.LoadSceneAsync("SubSceneA", LoadSceneMode.Additive);

handle.Completed += h =>
{
    // h.Result 是 SceneInstance，需要保存用于卸载
    _loadedScenes["SubSceneA"] = h;
};

// 卸载：传入之前保存的 handle
Addressables.UnloadSceneAsync(_loadedScenes["SubSceneA"]);
```

## 注意事项

- 必须**保存 handle** 才能卸载场景。失去 handle 等于失去对场景的引用。
- `Single` 模式会卸载所有当前场景，慎用于有持久 Manager 的项目。
- Sub Scene 必须在 Addressable Groups 中标记，不需要加入 Build Settings。
