# Chapter 04 — Remote 加载 & Catalog 热更新

## 学习目标

- 配置 Addressable Group 的 Remote Build/Load Path 指向阿里云 OSS
- 执行 Content Build，将 Remote 组产物上传到 OSS
- 用 `CheckForCatalogUpdates` + `UpdateCatalogs` 实现不重新打包 App 的资源热更新

## 前置步骤（只做一次）

### 1. 创建 OSS Bucket

阿里云控制台 → 对象存储 OSS → 创建 Bucket：
- 名称：自定义（如 `unity-addressables`）
- 读写权限：**公共读**

### 2. 配置 Addressable Profile

Unity: `Window > Asset Management > Addressables > Profiles`

选择 Default Profile，修改：
- `Remote.BuildPath` = `ServerData/[BuildTarget]`
- `Remote.LoadPath` = `https://your-bucket.oss-cn-hangzhou.aliyuncs.com/[BuildTarget]`

> 将 URL 替换为你的 Bucket 实际 Endpoint。

### 3. Build Content

`Window > Asset Management > Addressables > Groups` → **Build > New Build > Default Build Script**

Build 完成后 `ServerData/` 目录出现在项目根目录（Assets 同级）。

### 4. 上传到 OSS

将 `ServerData/<Platform>/` 目录下的所有文件上传到 Bucket 根目录下对应平台的文件夹：

```bash
ossutil cp -r ServerData/StandaloneOSX/ oss://your-bucket/StandaloneOSX/ --acl public-read
```

或通过 OSS 网页控制台手动上传。

## 场景操作

打开 `Chapter04Scene`，按 Play：

1. 点「检查 Catalog 更新」→ 日志显示是否有新版本
2. 点「更新 Catalog」（如有更新）→ 下载最新 Catalog
3. 点「加载 Remote 资源」→ 红色 Cube 从 OSS 下载并实例化

## Catalog 热更新流程

```
修改 Remote 资源 → 重新 Build → 上传新文件到 OSS
                                        ↓
App 运行中 → CheckForCatalogUpdates() → UpdateCatalogs()
                                        ↓
                            下次 LoadAssetAsync 加载新版本
```

App 本体无需重新打包或发布。

## 常见问题

**问：本地 Build 的资源也需要上传吗？**
答：不需要。Local 组的资源打进 App 包体，Remote 组的资源才需要上传到 OSS。

**问：CheckForCatalogUpdates 返回空列表是正常的吗？**
答：正常，说明本地 Catalog 已经是最新版本。
