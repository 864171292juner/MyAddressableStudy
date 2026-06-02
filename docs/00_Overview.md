# Addressable Asset System — 概念总览

## 什么是 Addressable？

Addressable Asset System 是 Unity 官方的资源管理方案，用来替代 `Resources.Load` 和手动管理 AssetBundle。

核心思想：给每个资源分配一个**地址（Address）**字符串，代码通过地址加载资源，而不关心资源在磁盘上的位置。

## 和 Resources.Load 有什么区别？

| 特性 | Resources.Load | Addressables |
|------|---------------|--------------|
| 资源存放位置 | 必须在 `Resources/` 文件夹 | 任意位置 |
| 加载方式 | 同步阻塞 | 异步非阻塞 |
| 内存管理 | 手动 `Resources.UnloadAsset` | `Addressables.Release(handle)` |
| 远程加载 | 不支持 | 支持（CDN/OSS） |
| 打包控制 | 自动全部打进包 | 精细控制哪些打本地、哪些放远端 |

## 核心概念

**Address（地址）：** 资源的唯一字符串标识，如 `"DemoCube"`。代码用这个字符串来加载资源。

**Group（组）：** 资源的打包分组。同一个 Group 的资源打成一个 Bundle。可以设置 Local（本地包）或 Remote（远端 CDN）。

**Profile（配置文件）：** 定义 Build Path 和 Load Path 的变量模板。切换 Profile 可以快速切换开发/生产环境的 CDN 地址。

**AsyncOperationHandle：** 所有 Addressable 操作都返回这个 Handle。它代表一次异步操作，持有结果和状态，必须在不再需要时调用 `Release` 释放。

**Catalog：** 记录所有 Addressable 资源地址和位置的清单文件。Remote Catalog 存放在 CDN 上，App 启动时可以检查并下载最新版本，实现热更新。

## 安装

在 `Packages/manifest.json` 的 `dependencies` 中添加：

```json
"com.unity.addressables": "1.22.3"
```

重启 Unity 后，在 `Window > Asset Management > Addressables > Groups` 点击 **"Create Addressables Settings"** 完成初始化。

## 工程结构说明

```
_Chapters/
├── Chapter01_BasicLoad/    ← LoadAssetAsync 基础用法
├── Chapter02_MemoryMgmt/   ← Handle 生命周期和 Release
├── Chapter03_SceneLoad/    ← 场景加载和卸载
└── Chapter04_Remote/       ← OSS Remote 加载 + Catalog 热更新
```

每章都是一个独立场景，直接在 Editor 中打开场景按 Play 即可运行。
