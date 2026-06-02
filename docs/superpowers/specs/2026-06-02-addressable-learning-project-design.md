# Addressable Learning Project — Design Spec

**Date:** 2026-06-02  
**Target:** Unity 2022 LTS，完全新手，从零掌握 Addressable

---

## 目标

创建一个可直接运行的 Unity 工程，配套 Markdown 文档，系统覆盖 Unity Addressable Asset System 的核心知识点——从基础标记与加载，到内存管理、场景管理，再到 Remote 加载与 Catalog 热更新。

---

## 工程结构

```
StudyUnity/
├── UnityProject/
│   ├── Assets/
│   │   ├── _Chapters/
│   │   │   ├── Chapter01_BasicLoad/
│   │   │   ├── Chapter02_MemoryMgmt/
│   │   │   ├── Chapter03_SceneLoad/
│   │   │   └── Chapter04_Remote/
│   │   ├── _Shared/
│   │   └── AddressableAssetsData/      # Addressable 配置（自动生成，纳入版本控制）
│   └── Packages/manifest.json
├── Docs/
│   ├── 00_Overview.md
│   ├── 01_BasicLoad.md
│   ├── 02_MemoryMgmt.md
│   ├── 03_SceneLoad.md
│   └── 04_Remote.md
└── (无本地服务器，Remote 资源托管在阿里云 OSS)
```

---

## 各章设计

### Chapter 01 — 基础标记与加载

**学习目标：** 理解 Addressable 与 Resources.Load 的区别，完成第一次 Addressable 资源加载。

**场景内容：**
- 一个 Cube Prefab、一张 Sprite，标记为 Addressable
- 两个按钮：「加载 Prefab」「加载 Sprite」
- 日志面板输出加载状态

**涉及 API：**
- `Addressables.LoadAssetAsync<T>(address)`
- `Instantiate` / `Image.sprite` 赋值
- `AsyncOperationHandle.Status`

---

### Chapter 02 — Handle 与内存管理

**学习目标：** 掌握 `AsyncOperationHandle` 生命周期，正确调用 `Release`，理解引用计数。

**场景内容：**
- 同一资源可重复加载多次，UI 实时显示当前 Handle 数量
- 「加载」「实例化」「Release Handle」「Destroy 实例」四个独立按钮
- 日志面板演示：只 Destroy 实例不 Release 会怎样；只 Release 不 Destroy 会怎样

**涉及 API：**
- `Addressables.Release(handle)`
- `Addressables.ReleaseInstance(gameObject)`
- `handle.IsValid()`

---

### Chapter 03 — 场景加载

**学习目标：** 用 Addressable 动态加载/卸载场景，理解 Additive 模式。

**场景内容：**
- 主场景（始终存在）+ 两个可动态加载的子场景（SubSceneA、SubSceneB）
- 按钮控制：Additive 加载、Single 加载、卸载指定场景
- 日志面板显示当前已加载的场景列表

**涉及 API：**
- `Addressables.LoadSceneAsync(address, LoadSceneMode.Additive)`
- `Addressables.UnloadSceneAsync(sceneInstance)`
- `SceneInstance` 持有与释放

---

### Chapter 04 — Remote 加载与 Catalog 更新

**学习目标：** 配置 Remote Group，Build Content，用本地 HTTP 服务器模拟 CDN，实现 Catalog 热更新。

**场景内容：**
- 一个 Remote Prefab（与 Chapter01 的本地 Prefab 视觉不同，便于区分）
- 按钮流程：「检查 Catalog 更新」→「更新 Catalog」→「加载 Remote 资源」
- 日志面板输出每一步的状态

**涉及 API：**
- `Addressables.CheckForCatalogUpdates()`
- `Addressables.UpdateCatalogs(catalogs)`
- Profile 中 Remote.BuildPath / Remote.LoadPath 配置

**额外步骤（文档说明）：**
1. 在阿里云 OSS 创建 Bucket，开启公共读权限
2. 在 Addressable Profile 中配置 `Remote.BuildPath` 和 `Remote.LoadPath`（指向 OSS Bucket URL）
3. 在 Editor 执行 `Build > New Build > Default Build Script`
4. 将 Remote 组产物（`ServerData/` 下对应平台目录内容）上传到 OSS Bucket
5. 进入 Play Mode 测试

---

## 共享组件（_Shared）

### DebugLogPanel

所有章节复用的 UI 日志组件。

```
DebugLogPanel
├── 滚动文本区域（最多显示 20 条，超出自动滚到底部）
└── 清空按钮
```

对外接口：`DebugLogPanel.Log(string message)`，自动附加时间戳。

---

## 文档结构

每篇 Markdown 文档包含：
1. 本章学习目标
2. 核心概念说明（配图或代码片段）
3. 场景操作步骤
4. 关键代码解析
5. 常见问题 / 踩坑提示

`00_Overview.md` 额外包含：Addressable 整体架构图（ASCII）、与 Resources/AssetBundle 的对比表、安装方式。

---

## 技术约束

- Unity 版本：2022 LTS（2022.3.x）
- Addressable 包版本：`com.unity.addressables` 1.21.x（2022 LTS 对应的稳定版）
- 不使用 URP/HDRP，使用 Built-in Render Pipeline，降低环境依赖
- 所有资源使用 Unity 内置原始体（Cube、Sphere）或纯色贴图，不依赖外部美术资源
- 代码风格：C#，类名与文件名一致，每个脚本只做一件事

---

## 不在范围内

- Addressable Profiler 深度分析
- 多平台 Bundle 压缩格式对比（LZ4 / LZMA）
- WebGL / 移动端特殊配置
- 与第三方资源管理框架（YooAsset 等）的对比
