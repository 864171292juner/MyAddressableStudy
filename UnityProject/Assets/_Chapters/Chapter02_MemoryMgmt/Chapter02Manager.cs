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
    private readonly List<(GameObject go, AsyncOperationHandle<GameObject> source)> _instances = new();

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
        var handle = _handles[_handles.Count - 1];
        var go = Instantiate(handle.Result);
        go.transform.position = Random.insideUnitSphere * 3f;
        _instances.Add((go, handle));
        _log.Log($"Instantiate 完成  实例数: {_instances.Count}");
        RefreshStatus();
    }

    private void OnReleaseHandleClick()
    {
        if (_handles.Count == 0) { _log.Log("没有 Handle 可释放"); return; }
        int released = 0, skipped = 0;
        for (int i = _handles.Count - 1; i >= 0; i--)
        {
            int dependent = 0;
            foreach (var t in _instances) if (t.source.Equals(_handles[i])) dependent++;
            if (dependent > 0) { skipped++; continue; }
            Addressables.Release(_handles[i]);
            _handles.RemoveAt(i);
            released++;
        }
        if (released > 0) _log.Log($"Release ✓  释放了 {released} 个 Handle，剩余: {_handles.Count}");
        if (skipped > 0) _log.Log($"⚠ 跳过 {skipped} 个 Handle（仍有实例依赖，请先销毁）");
        RefreshStatus();
    }

    private void OnDestroyInstanceClick()
    {
        if (_instances.Count == 0) { _log.Log("没有实例可销毁"); return; }
        var (go, _) = _instances[_instances.Count - 1];
        _instances.RemoveAt(_instances.Count - 1);
        Destroy(go);
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
        foreach (var (go, _) in _instances) if (go != null) Destroy(go);
        foreach (var h in _handles) if (h.IsValid()) Addressables.Release(h);
    }
}
