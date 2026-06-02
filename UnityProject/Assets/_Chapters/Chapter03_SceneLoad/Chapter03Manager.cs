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
