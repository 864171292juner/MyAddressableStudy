# Addressable Learning Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a 4-chapter Unity 2022 LTS project that teaches Addressable Asset System from basic loading to remote loading with Alibaba Cloud OSS, with a single-click Editor setup script that bootstraps all scenes and assets automatically.

**Architecture:** Each chapter has one focused Manager script. A shared `DebugLogPanel` handles log UI. Manager scripts auto-wire their UI dependencies at runtime via `FindObjectOfType` and `GameObject.Find`, so the Editor setup script only needs to create the GameObjects — no manual Inspector wiring required. A single `StudyProjectSetup` Editor script creates demo assets, configures Addressable groups, and generates all scenes.

**Tech Stack:** Unity 2022.3 LTS, com.unity.addressables 1.21.21, TextMeshPro 3.0.x, C#, Alibaba Cloud OSS (Chapter 04)

---

## File Map

| File | Purpose |
|------|---------|
| `UnityProject/Packages/manifest.json` | Add Addressables + TMP package refs |
| `UnityProject/Assets/_Shared/DebugLogPanel.cs` | Shared scrollable log UI, auto-finds children in Awake |
| `UnityProject/Assets/_Chapters/Chapter01_BasicLoad/Chapter01Manager.cs` | String-address async load demo |
| `UnityProject/Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Manager.cs` | Handle count + release lifecycle demo |
| `UnityProject/Assets/_Chapters/Chapter03_SceneLoad/Chapter03Manager.cs` | Additive/Single scene load + unload demo |
| `UnityProject/Assets/_Chapters/Chapter04_Remote/Chapter04Manager.cs` | CheckForCatalogUpdates + remote load demo |
| `UnityProject/Assets/Editor/StudyProjectSetup.cs` | Menu: creates prefabs, configures Addressable groups, generates all scenes |
| `Docs/00_Overview.md` | Addressable concepts, install, architecture |
| `Docs/01_BasicLoad.md` | Chapter 01 guide |
| `Docs/02_MemoryMgmt.md` | Chapter 02 guide |
| `Docs/03_SceneLoad.md` | Chapter 03 guide |
| `Docs/04_Remote.md` | Chapter 04 guide + OSS upload workflow |

---

### Task 1: Create Unity Project and Install Addressables

**Files:**
- Manual: Create via Unity Hub
- Modify: `UnityProject/Packages/manifest.json`

- [ ] **Step 1: Create the Unity project**

  Unity Hub → New Project → **3D (Built-in Render Pipeline)** → Unity 2022.3.x → Name: `StudyUnity` → Location: `/Users/tal/ClaudeProjects/StudyUnity/` → Create.

  Wait for Unity to finish loading the empty project.

- [ ] **Step 2: Close Unity, then add Addressables to manifest.json**

  Open `UnityProject/Packages/manifest.json`. Add `"com.unity.addressables": "1.21.21"` to the `dependencies` block. Example result:

  ```json
  {
    "dependencies": {
      "com.unity.addressables": "1.21.21",
      "com.unity.textmeshpro": "3.0.6",
      "com.unity.modules.physics": "1.0.0"
    }
  }
  ```

  Keep all existing entries; only add the addressables line.

- [ ] **Step 3: Reopen Unity and verify installation**

  Unity Hub → open project. Unity resolves packages. When loading is complete:
  - `Window > Package Manager` → confirm **Addressables 1.21.21** is installed.
  - `Window > Asset Management > Addressables > Groups` → click **"Create Addressables Settings"** in the window that opens. This creates `Assets/AddressableAssetsData/`.

- [ ] **Step 4: Commit**

  ```bash
  cd /Users/tal/ClaudeProjects/StudyUnity
  git init
  git add UnityProject/Packages/manifest.json UnityProject/Assets/AddressableAssetsData/
  git commit -m "feat: init Unity 2022 project with Addressables 1.21.21"
  ```

---

### Task 2: Create Folder Structure

**Files:**
- Create: all chapter + shared + editor directories

- [ ] **Step 1: Create directories**

  ```bash
  mkdir -p UnityProject/Assets/_Shared
  mkdir -p UnityProject/Assets/_Chapters/Chapter01_BasicLoad
  mkdir -p UnityProject/Assets/_Chapters/Chapter02_MemoryMgmt
  mkdir -p UnityProject/Assets/_Chapters/Chapter03_SceneLoad
  mkdir -p UnityProject/Assets/_Chapters/Chapter04_Remote
  mkdir -p UnityProject/Assets/Editor
  mkdir -p Docs
  ```

- [ ] **Step 2: Commit**

  ```bash
  git add UnityProject/Assets/
  git commit -m "feat: create project folder structure"
  ```

---

### Task 3: DebugLogPanel Shared Component

**Files:**
- Create: `UnityProject/Assets/_Shared/DebugLogPanel.cs`

- [ ] **Step 1: Write DebugLogPanel.cs**

  ```csharp
  using System.Collections.Generic;
  using TMPro;
  using UnityEngine;
  using UnityEngine.UI;

  public class DebugLogPanel : MonoBehaviour
  {
      private TextMeshProUGUI _logText;
      private ScrollRect _scrollRect;

      private const int MaxLines = 20;
      private readonly List<string> _lines = new();

      private void Awake()
      {
          _scrollRect = GetComponentInChildren<ScrollRect>();
          _logText = _scrollRect.content.GetComponent<TextMeshProUGUI>();
      }

      public void Log(string message)
      {
          var time = System.DateTime.Now.ToString("HH:mm:ss");
          _lines.Add($"[{time}] {message}");
          if (_lines.Count > MaxLines)
              _lines.RemoveAt(0);
          _logText.text = string.Join("\n", _lines);
          Canvas.ForceUpdateCanvases();
          _scrollRect.verticalNormalizedPosition = 0f;
      }

      public void Clear()
      {
          _lines.Clear();
          _logText.text = string.Empty;
      }
  }
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/_Shared/DebugLogPanel.cs
  git commit -m "feat: add DebugLogPanel shared component"
  ```

---

### Task 4: Chapter01Manager — Basic Load

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter01_BasicLoad/Chapter01Manager.cs`

- [ ] **Step 1: Write Chapter01Manager.cs**

  ```csharp
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using UnityEngine.UI;

  public class Chapter01Manager : MonoBehaviour
  {
      private DebugLogPanel _log;
      private Image _spriteTarget;

      private AsyncOperationHandle<GameObject> _cubeHandle;
      private AsyncOperationHandle<Sprite> _spriteHandle;
      private GameObject _cubeInstance;

      private void Start()
      {
          _log = FindObjectOfType<DebugLogPanel>();
          _spriteTarget = GameObject.Find("SpriteDisplay")?.GetComponent<Image>();

          WireButton("加载 Cube Prefab", OnLoadCubeClick);
          WireButton("加载 Sprite", OnLoadSpriteClick);
          WireButton("清理场景", OnClearClick);
          WireButton("清空日志", () => _log.Clear());
      }

      private void WireButton(string buttonName, UnityEngine.Events.UnityAction action)
      {
          var go = GameObject.Find(buttonName);
          go?.GetComponent<Button>()?.onClick.AddListener(action);
      }

      private void OnLoadCubeClick()
      {
          _log.Log("LoadAssetAsync<GameObject>(\"DemoCube\") 开始...");
          _cubeHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
          _cubeHandle.Completed += h =>
          {
              if (h.Status == AsyncOperationStatus.Succeeded)
              {
                  _cubeInstance = Instantiate(h.Result, Vector3.zero, Quaternion.identity);
                  _log.Log($"成功 ✓ 已实例化: {h.Result.name}");
              }
              else
              {
                  _log.Log($"失败 ✗ {h.OperationException?.Message}");
              }
          };
      }

      private void OnLoadSpriteClick()
      {
          _log.Log("LoadAssetAsync<Sprite>(\"DemoSprite\") 开始...");
          _spriteHandle = Addressables.LoadAssetAsync<Sprite>("DemoSprite");
          _spriteHandle.Completed += h =>
          {
              if (h.Status == AsyncOperationStatus.Succeeded)
              {
                  if (_spriteTarget != null) _spriteTarget.sprite = h.Result;
                  _log.Log($"成功 ✓ Sprite 已显示: {h.Result.name}");
              }
              else
              {
                  _log.Log($"失败 ✗ {h.OperationException?.Message}");
              }
          };
      }

      private void OnClearClick()
      {
          if (_cubeInstance != null) Destroy(_cubeInstance);
          if (_cubeHandle.IsValid()) Addressables.Release(_cubeHandle);
          if (_spriteHandle.IsValid()) Addressables.Release(_spriteHandle);
          if (_spriteTarget != null) _spriteTarget.sprite = null;
          _log.Log("场景已清理，Handle 已释放");
      }

      private void OnDestroy()
      {
          if (_cubeHandle.IsValid()) Addressables.Release(_cubeHandle);
          if (_spriteHandle.IsValid()) Addressables.Release(_spriteHandle);
      }
  }
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/_Chapters/Chapter01_BasicLoad/Chapter01Manager.cs
  git commit -m "feat: Chapter01Manager basic async load demo"
  ```

---

### Task 5: Chapter02Manager — Handle and Memory Management

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Manager.cs`

- [ ] **Step 1: Write Chapter02Manager.cs**

  ```csharp
  using System.Collections.Generic;
  using TMPro;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using UnityEngine.UI;

  public class Chapter02Manager : MonoBehaviour
  {
      private DebugLogPanel _log;
      private TextMeshProUGUI _statusText;

      private readonly List<AsyncOperationHandle<GameObject>> _handles = new();
      private readonly List<GameObject> _instances = new();

      private void Start()
      {
          _log = FindObjectOfType<DebugLogPanel>();
          _statusText = GameObject.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

          WireButton("加载 (LoadAssetAsync)", OnLoadClick);
          WireButton("实例化 (Instantiate)", OnInstantiateClick);
          WireButton("释放 Handle (Release)", OnReleaseHandleClick);
          WireButton("销毁实例 (Destroy)", OnDestroyInstanceClick);
          WireButton("清空日志", () => _log.Clear());

          RefreshStatus();
      }

      private void WireButton(string buttonName, UnityEngine.Events.UnityAction action)
      {
          GameObject.Find(buttonName)?.GetComponent<Button>()?.onClick.AddListener(action);
      }

      private void OnLoadClick()
      {
          _log.Log("LoadAssetAsync<GameObject>(\"DemoCube\") 开始...");
          var handle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
          handle.Completed += h =>
          {
              if (h.Status == AsyncOperationStatus.Succeeded)
              {
                  _handles.Add(h);
                  _log.Log($"加载成功 ✓  当前 Handle 数: {_handles.Count}");
                  RefreshStatus();
              }
              else
              {
                  _log.Log($"失败 ✗ {h.OperationException?.Message}");
              }
          };
      }

      private void OnInstantiateClick()
      {
          if (_handles.Count == 0)
          {
              _log.Log("没有可用 Handle，请先点「加载」");
              return;
          }
          var go = Instantiate(_handles[_handles.Count - 1].Result);
          go.transform.position = Random.insideUnitSphere * 3f;
          _instances.Add(go);
          _log.Log($"Instantiate 完成  实例数: {_instances.Count}");
          RefreshStatus();
      }

      private void OnReleaseHandleClick()
      {
          if (_handles.Count == 0) { _log.Log("没有 Handle 可释放"); return; }
          var last = _handles[_handles.Count - 1];
          _handles.RemoveAt(_handles.Count - 1);
          Addressables.Release(last);
          _log.Log($"Release(handle) ✓  剩余 Handle: {_handles.Count}");
          _log.Log("  → 已有实例不受影响，但引用计数 -1");
          RefreshStatus();
      }

      private void OnDestroyInstanceClick()
      {
          if (_instances.Count == 0) { _log.Log("没有实例可销毁"); return; }
          var last = _instances[_instances.Count - 1];
          _instances.RemoveAt(_instances.Count - 1);
          Destroy(last);
          _log.Log($"Destroy(gameObject) ✓  剩余实例: {_instances.Count}");
          _log.Log("  → 注意: Destroy 不会释放 Handle，需要单独 Release");
          RefreshStatus();
      }

      private void RefreshStatus()
      {
          if (_statusText != null)
              _statusText.text = $"Handle 数: {_handles.Count}    实例数: {_instances.Count}";
      }

      private void OnDestroy()
      {
          foreach (var go in _instances) if (go != null) Destroy(go);
          foreach (var h in _handles) if (h.IsValid()) Addressables.Release(h);
      }
  }
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Manager.cs
  git commit -m "feat: Chapter02Manager handle lifecycle demo"
  ```

---

### Task 6: Chapter03Manager — Scene Loading

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter03_SceneLoad/Chapter03Manager.cs`

- [ ] **Step 1: Write Chapter03Manager.cs**

  ```csharp
  using System.Collections.Generic;
  using TMPro;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using UnityEngine.ResourceManagement.ResourceProviders;
  using UnityEngine.SceneManagement;
  using UnityEngine.UI;

  public class Chapter03Manager : MonoBehaviour
  {
      private DebugLogPanel _log;
      private TextMeshProUGUI _sceneListText;

      private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loaded = new();

      private void Start()
      {
          _log = FindObjectOfType<DebugLogPanel>();
          _sceneListText = GameObject.Find("SceneListText")?.GetComponent<TextMeshProUGUI>();

          WireButton("Additive 加载 SubSceneA", () => LoadScene("SubSceneA", LoadSceneMode.Additive));
          WireButton("Additive 加载 SubSceneB", () => LoadScene("SubSceneB", LoadSceneMode.Additive));
          WireButton("Single 加载 SubSceneA", () => LoadScene("SubSceneA", LoadSceneMode.Single));
          WireButton("卸载 SubSceneA", () => UnloadScene("SubSceneA"));
          WireButton("卸载 SubSceneB", () => UnloadScene("SubSceneB"));
          WireButton("清空日志", () => _log.Clear());

          RefreshSceneList();
      }

      private void WireButton(string name, UnityEngine.Events.UnityAction action)
      {
          GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
      }

      private void LoadScene(string address, LoadSceneMode mode)
      {
          if (mode == LoadSceneMode.Additive && _loaded.ContainsKey(address))
          {
              _log.Log($"{address} 已处于加载状态，跳过");
              return;
          }
          _log.Log($"LoadSceneAsync(\"{address}\", {mode}) 开始...");
          var handle = Addressables.LoadSceneAsync(address, mode);
          handle.Completed += h =>
          {
              if (h.Status == AsyncOperationStatus.Succeeded)
              {
                  if (mode == LoadSceneMode.Additive)
                      _loaded[address] = h;
                  _log.Log($"加载成功 ✓  场景: {h.Result.Scene.name}  模式: {mode}");
              }
              else
              {
                  _log.Log($"失败 ✗ {h.OperationException?.Message}");
              }
              RefreshSceneList();
          };
      }

      private void UnloadScene(string address)
      {
          if (!_loaded.TryGetValue(address, out var handle))
          {
              _log.Log($"{address} 未加载，无法卸载");
              return;
          }
          _loaded.Remove(address);
          _log.Log($"UnloadSceneAsync(\"{address}\") 开始...");
          var unloadHandle = Addressables.UnloadSceneAsync(handle);
          unloadHandle.Completed += h =>
          {
              _log.Log(h.Status == AsyncOperationStatus.Succeeded
                  ? $"卸载成功 ✓  {address}"
                  : $"卸载失败 ✗ {h.OperationException?.Message}");
              RefreshSceneList();
          };
      }

      private void RefreshSceneList()
      {
          if (_sceneListText == null) return;
          _sceneListText.text = _loaded.Count == 0
              ? "当前已加载子场景: （无）"
              : "当前已加载子场景: " + string.Join(", ", _loaded.Keys);
      }
  }
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/_Chapters/Chapter03_SceneLoad/Chapter03Manager.cs
  git commit -m "feat: Chapter03Manager scene load/unload demo"
  ```

---

### Task 7: Chapter04Manager — Remote Loading and Catalog Update

**Files:**
- Create: `UnityProject/Assets/_Chapters/Chapter04_Remote/Chapter04Manager.cs`

- [ ] **Step 1: Write Chapter04Manager.cs**

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using UnityEngine.UI;

  public class Chapter04Manager : MonoBehaviour
  {
      private DebugLogPanel _log;
      private List<string> _pendingCatalogs;
      private AsyncOperationHandle<GameObject> _remoteHandle;
      private GameObject _remoteInstance;

      private void Start()
      {
          _log = FindObjectOfType<DebugLogPanel>();

          WireButton("检查 Catalog 更新", OnCheckCatalogClick);
          WireButton("更新 Catalog", OnUpdateCatalogClick);
          WireButton("加载 Remote 资源", OnLoadRemoteClick);
          WireButton("清理", OnClearClick);
          WireButton("清空日志", () => _log.Clear());
      }

      private void WireButton(string name, UnityEngine.Events.UnityAction action)
      {
          GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
      }

      private void OnCheckCatalogClick()
      {
          _log.Log("CheckForCatalogUpdates() 开始...");
          Addressables.CheckForCatalogUpdates(false).Completed += handle =>
          {
              if (handle.Status == AsyncOperationStatus.Succeeded)
              {
                  _pendingCatalogs = handle.Result;
                  _log.Log(_pendingCatalogs.Count == 0
                      ? "Catalog 已是最新 ✓"
                      : $"发现 {_pendingCatalogs.Count} 个 Catalog 需要更新 → 点「更新 Catalog」");
              }
              else
              {
                  _log.Log($"检查失败 ✗ {handle.OperationException?.Message}");
              }
              Addressables.Release(handle);
          };
      }

      private void OnUpdateCatalogClick()
      {
          if (_pendingCatalogs == null || _pendingCatalogs.Count == 0)
          {
              _log.Log("无待更新 Catalog，请先点「检查 Catalog 更新」");
              return;
          }
          _log.Log("UpdateCatalogs() 开始...");
          Addressables.UpdateCatalogs(_pendingCatalogs, false).Completed += handle =>
          {
              if (handle.Status == AsyncOperationStatus.Succeeded)
                  _log.Log("Catalog 更新完成 ✓ 现在可以加载最新 Remote 资源");
              else
                  _log.Log($"更新失败 ✗ {handle.OperationException?.Message}");
              Addressables.Release(handle);
              _pendingCatalogs = null;
          };
      }

      private void OnLoadRemoteClick()
      {
          _log.Log("LoadAssetAsync<GameObject>(\"RemoteCube\") 从 OSS 加载...");
          _remoteHandle = Addressables.LoadAssetAsync<GameObject>("RemoteCube");
          _remoteHandle.Completed += h =>
          {
              if (h.Status == AsyncOperationStatus.Succeeded)
              {
                  _remoteInstance = Instantiate(h.Result, Vector3.up * 2f, Quaternion.identity);
                  _log.Log($"Remote 资源加载成功 ✓ {h.Result.name}");
              }
              else
              {
                  _log.Log($"失败 ✗ {h.OperationException?.Message}");
              }
          };
      }

      private void OnClearClick()
      {
          if (_remoteInstance != null) Destroy(_remoteInstance);
          if (_remoteHandle.IsValid()) Addressables.Release(_remoteHandle);
          _log.Log("Remote 资源已清理，Handle 已释放");
      }

      private void OnDestroy()
      {
          if (_remoteHandle.IsValid()) Addressables.Release(_remoteHandle);
      }
  }
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/_Chapters/Chapter04_Remote/Chapter04Manager.cs
  git commit -m "feat: Chapter04Manager remote catalog and load demo"
  ```

---

### Task 8: Editor Setup Script

**Files:**
- Create: `UnityProject/Assets/Editor/StudyProjectSetup.cs`

This script runs once via the menu item `StudyUnity > Setup All Chapters` and:
1. Creates demo assets (cyan cube prefab, green sprite texture, red remote cube prefab)
2. Configures two Addressable groups: `LocalContent` and `RemoteContent`
3. Marks assets with their addresses
4. Creates all 7 scenes with full UI hierarchies — Chapter01, Chapter02, Chapter03 Main, SubSceneA, SubSceneB, Chapter04

- [ ] **Step 1: Write StudyProjectSetup.cs**

  ```csharp
  #if UNITY_EDITOR
  using System.IO;
  using UnityEditor;
  using UnityEditor.AddressableAssets;
  using UnityEditor.AddressableAssets.Settings;
  using UnityEditor.AddressableAssets.Settings.GroupSchemas;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.EventSystems;
  using UnityEngine.UI;
  using TMPro;

  public static class StudyProjectSetup
  {
      private const string LocalGroup = "LocalContent";
      private const string RemoteGroup = "RemoteContent";

      [MenuItem("StudyUnity/Setup All Chapters")]
      public static void SetupAll()
      {
          CreateDemoAssets();
          ConfigureAddressables();
          CreateChapter01Scene();
          CreateChapter02Scene();
          CreateChapter03Scenes();
          CreateChapter04Scene();
          AssetDatabase.SaveAssets();
          AssetDatabase.Refresh();
          Debug.Log("[StudyUnity] Setup complete! Open any chapter scene and press Play.");
      }

      // ── Demo Assets ──────────────────────────────────────────────

      static void CreateDemoAssets()
      {
          EnsureDir("Assets/_Chapters/Chapter01_BasicLoad/DemoAssets");
          EnsureDir("Assets/_Chapters/Chapter04_Remote/DemoAssets");

          MakeCubePrefab(
              "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab",
              "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCubeMat.mat",
              new Color(0.2f, 0.8f, 1f));

          MakeSpriteTex("Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoSprite.png");

          MakeCubePrefab(
              "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCube.prefab",
              "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCubeMat.mat",
              new Color(1f, 0.3f, 0.2f));
      }

      static void MakeCubePrefab(string prefabPath, string matPath, Color color)
      {
          if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return;
          var mat = new Material(Shader.Find("Standard")) { color = color };
          AssetDatabase.CreateAsset(mat, matPath);
          var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
          go.GetComponent<Renderer>().sharedMaterial = mat;
          PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
          Object.DestroyImmediate(go);
      }

      static void MakeSpriteTex(string path)
      {
          if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
          var tex = new Texture2D(128, 128);
          var pixels = new Color[128 * 128];
          for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.2f, 0.9f, 0.3f);
          tex.SetPixels(pixels);
          File.WriteAllBytes(path, tex.EncodeToPNG());
          AssetDatabase.ImportAsset(path);
          var imp = (TextureImporter)AssetImporter.GetAtPath(path);
          imp.textureType = TextureImporterType.Sprite;
          imp.SaveAndReimport();
      }

      // ── Addressable Groups ────────────────────────────────────────

      static void ConfigureAddressables()
      {
          var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
          var local = GetOrCreateGroup(s, LocalGroup, remote: false);
          var remote = GetOrCreateGroup(s, RemoteGroup, remote: true);

          Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoCube.prefab", "DemoCube", local);
          Tag(s, "Assets/_Chapters/Chapter01_BasicLoad/DemoAssets/DemoSprite.png", "DemoSprite", local);
          Tag(s, "Assets/_Chapters/Chapter04_Remote/DemoAssets/RemoteCube.prefab", "RemoteCube", remote);
      }

      static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings s, string name, bool remote)
      {
          var g = s.FindGroup(name);
          if (g != null) return g;
          g = s.CreateGroup(name, false, false, false, null,
              typeof(ContentUpdateGroupSchema),
              typeof(BundledAssetGroupSchema));
          if (remote)
          {
              var schema = g.GetSchema<BundledAssetGroupSchema>();
              schema.BuildPath.SetVariableByName(s, AddressableAssetSettings.kRemoteBuildPath);
              schema.LoadPath.SetVariableByName(s, AddressableAssetSettings.kRemoteLoadPath);
          }
          return g;
      }

      static void Tag(AddressableAssetSettings s, string assetPath, string address, AddressableAssetGroup group)
      {
          var guid = AssetDatabase.AssetPathToGUID(assetPath);
          if (string.IsNullOrEmpty(guid)) { Debug.LogWarning($"[StudyUnity] Asset not found: {assetPath}"); return; }
          var entry = s.CreateOrMoveEntry(guid, group, false, false);
          entry.address = address;
      }

      // ── Scene Helpers ─────────────────────────────────────────────

      static (UnityEngine.SceneManagement.Scene scene, GameObject canvas) NewScene(string savePath)
      {
          EnsureDir(Path.GetDirectoryName(savePath));
          var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

          var camGo = new GameObject("Main Camera");
          camGo.tag = "MainCamera";
          var cam = camGo.AddComponent<Camera>();
          cam.clearFlags = CameraClearFlags.SolidColor;
          cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
          cam.orthographic = false;
          camGo.transform.position = new Vector3(0, 1, -10);

          var lightGo = new GameObject("Directional Light");
          var light = lightGo.AddComponent<Light>();
          light.type = LightType.Directional;
          lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

          var evSys = new GameObject("EventSystem");
          evSys.AddComponent<EventSystem>();
          evSys.AddComponent<StandaloneInputModule>();

          var canvasGo = new GameObject("Canvas");
          var cv = canvasGo.AddComponent<Canvas>();
          cv.renderMode = RenderMode.ScreenSpaceOverlay;
          var scaler = canvasGo.AddComponent<CanvasScaler>();
          scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
          scaler.referenceResolution = new Vector2(1920, 1080);
          canvasGo.AddComponent<GraphicRaycaster>();

          EditorSceneManager.SaveScene(scene, savePath);
          return (scene, canvasGo);
      }

      static GameObject Btn(GameObject parent, string label, Vector2 aMin, Vector2 aMax)
      {
          var go = new GameObject(label);
          go.transform.SetParent(parent.transform, false);
          var rt = go.AddComponent<RectTransform>();
          rt.anchorMin = aMin; rt.anchorMax = aMax;
          rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -4);
          var img = go.AddComponent<Image>();
          img.color = new Color(0.18f, 0.45f, 0.78f);
          go.AddComponent<Button>();
          var txtGo = new GameObject("Text");
          txtGo.transform.SetParent(go.transform, false);
          var trt = txtGo.AddComponent<RectTransform>();
          trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
          trt.offsetMin = trt.offsetMax = Vector2.zero;
          var tmp = txtGo.AddComponent<TextMeshProUGUI>();
          tmp.text = label; tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = 22;
          return go;
      }

      static GameObject Txt(GameObject parent, string name, string text, Vector2 aMin, Vector2 aMax, int fontSize = 26)
      {
          var go = new GameObject(name);
          go.transform.SetParent(parent.transform, false);
          var rt = go.AddComponent<RectTransform>();
          rt.anchorMin = aMin; rt.anchorMax = aMax;
          rt.offsetMin = new Vector2(10, 4); rt.offsetMax = new Vector2(-10, -4);
          var tmp = go.AddComponent<TextMeshProUGUI>();
          tmp.text = text; tmp.fontSize = fontSize; tmp.color = Color.white;
          return go;
      }

      // Adds the log panel UI + DebugLogPanel component
      // Hierarchy: LogPanel > ScrollRect > Content (TMP)
      static void AddLogPanel(GameObject canvas)
      {
          var panel = new GameObject("LogPanel");
          panel.transform.SetParent(canvas.transform, false);
          var panelRt = panel.AddComponent<RectTransform>();
          panelRt.anchorMin = new Vector2(0, 0);
          panelRt.anchorMax = new Vector2(1, 0.32f);
          panelRt.offsetMin = new Vector2(10, 10);
          panelRt.offsetMax = new Vector2(-10, -5);
          var bg = panel.AddComponent<Image>();
          bg.color = new Color(0, 0, 0, 0.75f);

          var scrollGo = new GameObject("ScrollRect");
          scrollGo.transform.SetParent(panel.transform, false);
          var scrollRt = scrollGo.AddComponent<RectTransform>();
          scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
          scrollRt.offsetMin = new Vector2(6, 6); scrollRt.offsetMax = new Vector2(-6, -6);
          var scroll = scrollGo.AddComponent<ScrollRect>();
          scroll.horizontal = false;

          var contentGo = new GameObject("Content");
          contentGo.transform.SetParent(scrollGo.transform, false);
          var contentRt = contentGo.AddComponent<RectTransform>();
          contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
          contentRt.pivot = new Vector2(0.5f, 1f);
          contentRt.sizeDelta = new Vector2(0, 0);
          var logTmp = contentGo.AddComponent<TextMeshProUGUI>();
          logTmp.text = "日志输出...";
          logTmp.fontSize = 18; logTmp.color = new Color(0.3f, 1f, 0.4f);
          var fitter = contentGo.AddComponent<ContentSizeFitter>();
          fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

          scroll.content = contentRt;
          panel.AddComponent<DebugLogPanel>();
      }

      // ── Chapter 01 ────────────────────────────────────────────────

      static void CreateChapter01Scene()
      {
          var (scene, canvas) = NewScene("Assets/_Chapters/Chapter01_BasicLoad/Chapter01Scene.unity");

          Txt(canvas, "Title", "Chapter 01 — 基础加载", new Vector2(0, 0.92f), Vector2.one, 32);
          Btn(canvas, "加载 Cube Prefab", new Vector2(0, 0.78f), new Vector2(0.5f, 0.92f));
          Btn(canvas, "加载 Sprite", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
          Btn(canvas, "清理场景", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
          Btn(canvas, "清空日志", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));

          // Sprite display area
          var imgGo = new GameObject("SpriteDisplay");
          imgGo.transform.SetParent(canvas.transform, false);
          var imgRt = imgGo.AddComponent<RectTransform>();
          imgRt.anchorMin = new Vector2(0.3f, 0.36f); imgRt.anchorMax = new Vector2(0.7f, 0.64f);
          var img = imgGo.AddComponent<Image>();
          img.color = new Color(0.2f, 0.2f, 0.2f);

          AddLogPanel(canvas);

          new GameObject("Chapter01Manager").AddComponent<Chapter01Manager>();
          EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter01_BasicLoad/Chapter01Scene.unity");
      }

      // ── Chapter 02 ────────────────────────────────────────────────

      static void CreateChapter02Scene()
      {
          var (scene, canvas) = NewScene("Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Scene.unity");

          Txt(canvas, "Title", "Chapter 02 — Handle 与内存管理", new Vector2(0, 0.92f), Vector2.one, 32);
          Btn(canvas, "加载 (LoadAssetAsync)", new Vector2(0, 0.78f), new Vector2(0.5f, 0.92f));
          Btn(canvas, "实例化 (Instantiate)", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
          Btn(canvas, "释放 Handle (Release)", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
          Btn(canvas, "销毁实例 (Destroy)", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
          Btn(canvas, "清空日志", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.64f));
          Txt(canvas, "StatusText", "Handle 数: 0    实例数: 0", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 24);

          AddLogPanel(canvas);

          new GameObject("Chapter02Manager").AddComponent<Chapter02Manager>();
          EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Scene.unity");
      }

      // ── Chapter 03 ────────────────────────────────────────────────

      static void CreateChapter03Scenes()
      {
          MakeSubScene("Assets/_Chapters/Chapter03_SceneLoad/SubSceneA.unity", "Sub Scene A", new Color(0.1f, 0.1f, 0.5f));
          MakeSubScene("Assets/_Chapters/Chapter03_SceneLoad/SubSceneB.unity", "Sub Scene B", new Color(0.4f, 0.1f, 0.4f));

          var (scene, canvas) = NewScene("Assets/_Chapters/Chapter03_SceneLoad/Chapter03MainScene.unity");

          Txt(canvas, "Title", "Chapter 03 — 场景加载", new Vector2(0, 0.92f), Vector2.one, 32);
          Btn(canvas, "Additive 加载 SubSceneA", new Vector2(0f, 0.78f), new Vector2(0.5f, 0.92f));
          Btn(canvas, "Additive 加载 SubSceneB", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
          Btn(canvas, "Single 加载 SubSceneA", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
          Btn(canvas, "卸载 SubSceneA", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
          Btn(canvas, "卸载 SubSceneB", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.64f));
          Btn(canvas, "清空日志", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.64f));
          Txt(canvas, "SceneListText", "当前已加载子场景: （无）", new Vector2(0, 0.36f), new Vector2(1f, 0.5f), 22);

          AddLogPanel(canvas);

          new GameObject("Chapter03Manager").AddComponent<Chapter03Manager>();
          EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter03_SceneLoad/Chapter03MainScene.unity");

          // Mark sub scenes as Addressable
          var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
          var local = s.FindGroup(LocalGroup) ?? GetOrCreateGroup(s, LocalGroup, remote: false);
          Tag(s, "Assets/_Chapters/Chapter03_SceneLoad/SubSceneA.unity", "SubSceneA", local);
          Tag(s, "Assets/_Chapters/Chapter03_SceneLoad/SubSceneB.unity", "SubSceneB", local);
      }

      static void MakeSubScene(string path, string label, Color bgColor)
      {
          EnsureDir(Path.GetDirectoryName(path));
          var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

          var floorGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
          floorGo.name = "Floor";
          var matPath = path.Replace(".unity", "_Mat.mat");
          var mat = new Material(Shader.Find("Standard")) { color = bgColor };
          AssetDatabase.CreateAsset(mat, matPath);
          floorGo.GetComponent<Renderer>().sharedMaterial = mat;

          var labelGo = new GameObject("Label");
          labelGo.transform.position = new Vector3(0, 1.5f, 0);
          var tmp = labelGo.AddComponent<TextMeshPro>();
          tmp.text = label; tmp.fontSize = 6; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

          EditorSceneManager.SaveScene(scene, path);
      }

      // ── Chapter 04 ────────────────────────────────────────────────

      static void CreateChapter04Scene()
      {
          var (scene, canvas) = NewScene("Assets/_Chapters/Chapter04_Remote/Chapter04Scene.unity");

          Txt(canvas, "Title", "Chapter 04 — Remote 加载 & Catalog 热更新", new Vector2(0, 0.92f), Vector2.one, 28);
          Btn(canvas, "检查 Catalog 更新", new Vector2(0f, 0.78f), new Vector2(0.5f, 0.92f));
          Btn(canvas, "更新 Catalog", new Vector2(0.5f, 0.78f), new Vector2(1f, 0.92f));
          Btn(canvas, "加载 Remote 资源", new Vector2(0f, 0.64f), new Vector2(0.5f, 0.78f));
          Btn(canvas, "清理", new Vector2(0.5f, 0.64f), new Vector2(1f, 0.78f));
          Btn(canvas, "清空日志", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.64f));

          AddLogPanel(canvas);

          new GameObject("Chapter04Manager").AddComponent<Chapter04Manager>();
          EditorSceneManager.SaveScene(scene, "Assets/_Chapters/Chapter04_Remote/Chapter04Scene.unity");
      }

      // ── Utilities ─────────────────────────────────────────────────

      static void EnsureDir(string path)
      {
          if (string.IsNullOrEmpty(path) || Directory.Exists(path)) return;
          Directory.CreateDirectory(path);
          AssetDatabase.Refresh();
      }
  }
  #endif
  ```

- [ ] **Step 2: Verify in Unity Console — no compile errors**

- [ ] **Step 3: Commit**

  ```bash
  git add UnityProject/Assets/Editor/StudyProjectSetup.cs
  git commit -m "feat: add EditorSetup script for one-click scene bootstrap"
  ```

---

### Task 9: Run Setup and Verify Chapter 01 and 02

- [ ] **Step 1: Run setup**

  In Unity: **StudyUnity > Setup All Chapters** (top menu bar).
  
  Expected console output: `[StudyUnity] Setup complete! Open any chapter scene and press Play.`

- [ ] **Step 2: Verify Chapter 01**

  Open `Assets/_Chapters/Chapter01_BasicLoad/Chapter01Scene.unity`. Press Play.
  - Click **加载 Cube Prefab** → cyan cube appears in the scene, log shows "成功 ✓"
  - Click **加载 Sprite** → green square appears in the UI display area, log shows "成功 ✓"
  - Click **清理场景** → cube and sprite disappear, log shows "Handle 已释放"

- [ ] **Step 3: Verify Chapter 02**

  Open `Assets/_Chapters/Chapter02_MemoryMgmt/Chapter02Scene.unity`. Press Play.
  - Click **加载** 3 times → status shows "Handle 数: 3  实例数: 0"
  - Click **实例化** → cube appears, "实例数: 1"
  - Click **销毁实例** → cube disappears, log shows "注意: Destroy 不会释放 Handle"
  - Click **释放 Handle** → "Handle 数: 2", log shows "引用计数 -1"

- [ ] **Step 4: Commit**

  ```bash
  git add UnityProject/Assets/
  git commit -m "feat: setup complete - Chapter01 and Chapter02 verified"
  ```

---

### Task 10: Verify Chapter 03 (Scene Loading)

- [ ] **Step 1: Open Chapter 03 main scene**

  Open `Assets/_Chapters/Chapter03_SceneLoad/Chapter03MainScene.unity`. Press Play.

- [ ] **Step 2: Verify Additive load**

  - Click **Additive 加载 SubSceneA** → blue floor plane appears in the scene, log shows "加载成功 ✓ 模式: Additive"
  - Click **Additive 加载 SubSceneB** → purple floor plane also appears. Status shows both scenes loaded.
  - Click **卸载 SubSceneA** → blue floor disappears, log shows "卸载成功 ✓"

- [ ] **Step 3: Verify Single load**

  - Click **Additive 加载 SubSceneA** again → blue floor reappears
  - Click **Single 加载 SubSceneA** → scene reloads in Single mode. Note: this replaces the main scene — expected behavior for Single mode, described in docs.

- [ ] **Step 4: Commit**

  ```bash
  git add UnityProject/Assets/
  git commit -m "feat: Chapter03 scene loading verified"
  ```

---

### Task 11: Configure OSS Profile and Verify Chapter 04

- [ ] **Step 1: Set up OSS Bucket**

  In Alibaba Cloud Console:
  1. Create a Bucket (e.g., `unity-addressables-test`), Region: any, Access: **Public Read**
  2. Note the endpoint URL format: `https://<bucket>.<region>.aliyuncs.com`

- [ ] **Step 2: Configure Addressable Profile**

  In Unity: `Window > Asset Management > Addressables > Profiles`.
  
  Select the **Default** profile. Set:
  - `Remote.BuildPath` → `ServerData/[BuildTarget]`
  - `Remote.LoadPath` → `https://unity-addressables-test.oss-cn-hangzhou.aliyuncs.com/[BuildTarget]`
  
  Replace the URL with your actual Bucket endpoint and target region.

- [ ] **Step 3: Build Addressable content**

  `Window > Asset Management > Addressables > Groups` → **Build > New Build > Default Build Script**
  
  After build completes, the `ServerData/` folder appears in the project root (outside `Assets/`).

- [ ] **Step 4: Upload Remote group output to OSS**

  Upload the contents of `ServerData/StandaloneOSX/` (or your target platform folder) to your OSS Bucket root directory. You can use the aliyun CLI or the OSS web console.

  ```bash
  # Using aliyun CLI (install from: https://help.aliyun.com/document_detail/121541.html)
  ossutil cp -r ServerData/StandaloneOSX/ oss://unity-addressables-test/StandaloneOSX/ --acl public-read
  ```

- [ ] **Step 5: Verify Chapter 04**

  Open `Assets/_Chapters/Chapter04_Remote/Chapter04Scene.unity`. Press Play.
  - Click **检查 Catalog 更新** → log shows "Catalog 已是最新 ✓" (or lists updates if you changed remote content)
  - Click **加载 Remote 资源** → red cube appears (loaded from OSS), log shows "Remote 资源加载成功 ✓"
  - Click **清理** → cube disappears, Handle released

- [ ] **Step 6: Commit**

  ```bash
  git add UnityProject/ Docs/
  git commit -m "feat: Chapter04 OSS remote load verified"
  ```

---

### Task 12: Write Docs/00_Overview.md

**Files:**
- Create: `Docs/00_Overview.md`

- [ ] **Step 1: Write 00_Overview.md**

  ```markdown
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
  "com.unity.addressables": "1.21.21"
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
  ```

- [ ] **Step 2: Commit**

  ```bash
  git add Docs/00_Overview.md
  git commit -m "docs: add Addressable overview"
  ```

---

### Task 13: Write Chapter Docs (01–04)

**Files:**
- Create: `Docs/01_BasicLoad.md`, `Docs/02_MemoryMgmt.md`, `Docs/03_SceneLoad.md`, `Docs/04_Remote.md`

- [ ] **Step 1: Write Docs/01_BasicLoad.md**

  ```markdown
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
  ```

- [ ] **Step 2: Write Docs/02_MemoryMgmt.md**

  ```markdown
  # Chapter 02 — Handle 与内存管理

  ## 学习目标

  - 理解 `AsyncOperationHandle` 的生命周期
  - 正确使用 `Addressables.Release(handle)` 释放资源
  - 区分「Destroy 实例」和「Release Handle」的区别

  ## 场景操作

  打开 `Chapter02Scene`，按 Play，按照以下顺序点按钮：

  1. 点「加载」3次 → Handle 数: 3
  2. 点「实例化」→ Cube 出现
  3. 点「销毁实例」→ Cube 消失，但注意日志："Destroy 不会释放 Handle"
  4. 点「释放 Handle」→ Handle 数 -1

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
  ```

- [ ] **Step 3: Write Docs/03_SceneLoad.md**

  ```markdown
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
  ```

- [ ] **Step 4: Write Docs/04_Remote.md**

  ```markdown
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
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add Docs/
  git commit -m "docs: add chapter guides 01-04"
  ```

---

## Self-Review

**Spec coverage check:**
- Chapter 01 (basic load): Task 4 + Task 9 ✓
- Chapter 02 (memory management): Task 5 + Task 9 ✓
- Chapter 03 (scene loading): Task 6 + Task 10 ✓
- Chapter 04 (remote + catalog): Task 7 + Task 11 ✓
- DebugLogPanel shared component: Task 3 ✓
- OSS configuration (not local server): Task 11 + Docs/04_Remote.md ✓
- All docs (00-04): Tasks 12–13 ✓
- Editor one-click setup: Task 8 ✓

**Placeholder scan:** No TBD, no TODO, no "implement later". All code blocks are complete.

**Type consistency:**
- `DebugLogPanel.Log(string)` — defined Task 3, used in Tasks 4–7 ✓
- `DebugLogPanel.Clear()` — defined Task 3, called via `() => _log.Clear()` in Tasks 4–7 ✓
- Button names match exactly between setup script (Task 8) and `WireButton` calls (Tasks 4–7) ✓
- Addressable addresses: `"DemoCube"`, `"DemoSprite"`, `"SubSceneA"`, `"SubSceneB"`, `"RemoteCube"` — consistent across setup script and manager scripts ✓
