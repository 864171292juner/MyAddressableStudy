# Chapter 06 AssetReference 设计

## 目标

演示 `AssetReference` 的用法，与字符串地址加载做对比，展示 Inspector 拖拽引用的优势。

## 核心对比

| 方式 | 代码 | 优缺点 |
|------|------|--------|
| 字符串地址 | `Addressables.LoadAssetAsync<GameObject>("DemoCube")` | 简单直接，但魔法字符串容易拼错、IDE 无法检查 |
| AssetReference | `_cubeRef.LoadAssetAsync<GameObject>()` | Inspector 拖拽配置，编译期类型安全，无魔法字符串 |

## 资产

复用 Ch01 已有资产，不新建：
- `DemoCube`（LocalContent 组，地址 "DemoCube"）

## 场景 UI

文件：`Assets/_Chapters/Chapter06_AssetReference/Chapter06Scene.unity`

```
[ 字符串加载 ]        [ AssetRef 加载 ]
[ 清理 ]              [ 清空日志 ]

[ 日志面板 ]
```

## Manager 逻辑

文件：`Assets/_Chapters/Chapter06_AssetReference/Chapter06Manager.cs`

```csharp
[SerializeField] AssetReferenceGameObject _cubeRef;

// 字符串加载
var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
handle.Completed += h => { Instantiate(h.Result, ...); _stringHandle = h; };

// AssetReference 加载
var handle = _cubeRef.LoadAssetAsync<GameObject>();
handle.Completed += h => { Instantiate(h.Result, ...); _refHandle = h; };

// 释放（对应方式不同）
Addressables.Release(_stringHandle);
_cubeRef.ReleaseAsset();
```

字符串加载的实例显示在左侧（x = -2），AssetRef 加载的实例显示在右侧（x = +2），可同时存在用于对比。

## Setup 脚本改动

`StudyProjectSetup.cs` 新增：

1. `CreateChapter06Scene()`：按上方 UI 布局建场景，挂 Chapter06Manager
2. 用 `SerializedObject` 把 DemoCube 的 GUID 写入 `_cubeRef.m_AssetGUID`，无需手动拖拽
3. `SetupAll()` 添加 `CreateChapter06Scene()` 调用

## Setup 中设置 AssetReference 的方法

```csharp
static void SetAssetReference(Object component, string fieldName, string assetPath)
{
    var guid = AssetDatabase.AssetPathToGUID(assetPath);
    var so = new SerializedObject(component);
    var prop = so.FindProperty(fieldName);
    prop.FindPropertyRelative("m_AssetGUID").stringValue = guid;
    so.ApplyModifiedProperties();
}
```

## 新增 API 对照

| API | 用途 |
|-----|------|
| `AssetReferenceGameObject` | 类型安全的 GameObject 引用，只能拖拽 Prefab |
| `_ref.LoadAssetAsync<T>()` | 等价于 `Addressables.LoadAssetAsync<T>(key)` |
| `_ref.ReleaseAsset()` | 释放加载的资产（不销毁实例） |
| `_ref.InstantiateAsync()` | 一步加载并实例化（对应 `Addressables.InstantiateAsync`） |
| `_ref.RuntimeKeyIsValid()` | 检查引用是否已在 Inspector 赋值 |
