using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class Chapter09Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private AsyncOperationHandle<IList<GameObject>> _activeHandle;
    private readonly List<GameObject> _instances = new List<GameObject>();

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        WireButton("加载 SetA",               () => LoadByKey("SetA"));
        WireButton("加载 SetB",               () => LoadByKey("SetB"));
        WireButton("Union(SetA|SetB)",        OnUnionClick);
        WireButton("Intersection(SetA∩SetB)", OnIntersectionClick);
        WireButton("清理",                    OnClearClick);
        WireButton("清空日志",                 () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void LoadByKey(string key)
    {
        ClearInstances();
        _log.Log($"LoadAssetsAsync(\"{key}\") 开始...");
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(key, null);
        _activeHandle.Completed += h => OnLoaded(h, key);
    }

    private void OnUnionClick()
    {
        ClearInstances();
        _log.Log("LoadAssetsAsync(Union: SetA | SetB) 开始...");
        var keys = new List<object> { "SetA", "SetB" };
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(
            keys, null, Addressables.MergeMode.Union);
        _activeHandle.Completed += h => OnLoaded(h, "Union");
    }

    private void OnIntersectionClick()
    {
        ClearInstances();
        _log.Log("LoadAssetsAsync(Intersection: SetA ∩ SetB) 开始...");
        var keys = new List<object> { "SetA", "SetB" };
        _activeHandle = Addressables.LoadAssetsAsync<GameObject>(
            keys, null, Addressables.MergeMode.Intersection);
        _activeHandle.Completed += h => OnLoaded(h, "Intersection");
    }

    private void OnLoaded(AsyncOperationHandle<IList<GameObject>> h, string label)
    {
        if (h.Status != AsyncOperationStatus.Succeeded)
        {
            _log.Log($"{label} 加载失败 ✗ {h.OperationException?.Message}");
            Addressables.Release(_activeHandle);
            _activeHandle = default;
            return;
        }
        float spacing = 2.5f;
        float startX  = -(h.Result.Count - 1) * spacing / 2f;
        for (int i = 0; i < h.Result.Count; i++)
            _instances.Add(Instantiate(h.Result[i],
                new Vector3(startX + i * spacing, 0, 0), Quaternion.identity));
        _log.Log($"{label} 加载完成 ✓  共 {h.Result.Count} 个资产");
    }

    private void ClearInstances()
    {
        foreach (var inst in _instances) Destroy(inst);
        _instances.Clear();
        if (_activeHandle.IsValid()) Addressables.Release(_activeHandle);
        _activeHandle = default;
    }

    private void OnClearClick()
    {
        ClearInstances();
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_activeHandle.IsValid()) Addressables.Release(_activeHandle);
    }
}
