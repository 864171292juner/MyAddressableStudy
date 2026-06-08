using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter06Manager : MonoBehaviour
{
    [SerializeField] AssetReferenceGameObject _cubeRef;
    // [SerializeField] private AssetReferenceGameObject _enemyPrefab;
    [SerializeField] private AssetReferenceT<AudioClip> _bgm;
    [SerializeField] private AssetReferenceSprite _icon;

    private DebugLogPanel _log;
    private AsyncOperationHandle<GameObject> _stringHandle;
    private AsyncOperationHandle<GameObject> _refHandle;
    private GameObject _stringInstance;
    private GameObject _refInstance;

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        WireButton("字符串加载", OnStringLoadClick);
        WireButton("AssetRef 加载", OnRefLoadClick);
        WireButton("清理", OnClearClick);
        WireButton("清空日志", () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void OnStringLoadClick()
    {
        if (_stringHandle.IsValid()) { _log.Log("字符串方式已加载，请先「清理」"); return; }
        _log.Log("Addressables.LoadAssetAsync<GameObject>(\"DemoCube\") 开始...");
        _stringHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        _stringHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _stringInstance = Instantiate(h.Result, new Vector3(-2f, 0, 0), Quaternion.identity);
                _log.Log($"字符串加载完成 ✓  {h.Result.name}  (左侧)");
            }
            else
            {
                _log.Log($"字符串加载失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private void OnRefLoadClick()
    {
        if (_refHandle.IsValid()) { _log.Log("AssetReference 方式已加载，请先「清理」"); return; }
        if (!_cubeRef.RuntimeKeyIsValid()) { _log.Log("_cubeRef 未赋值，Inspector 检查"); return; }
        _log.Log("_cubeRef.LoadAssetAsync<GameObject>() 开始...");
        _refHandle = _cubeRef.LoadAssetAsync<GameObject>();
        _refHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _refInstance = Instantiate(h.Result, new Vector3(2f, 0, 0), Quaternion.identity);
                _log.Log($"AssetRef 加载完成 ✓  {h.Result.name}  (右侧)");
            }
            else
            {
                _log.Log($"AssetRef 加载失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private void OnClearClick()
    {
        if (_stringInstance != null) { Destroy(_stringInstance); _stringInstance = null; }
        if (_refInstance != null) { Destroy(_refInstance); _refInstance = null; }
        if (_stringHandle.IsValid()) Addressables.Release(_stringHandle);
        if (_refHandle.IsValid()) _cubeRef.ReleaseAsset();
        _stringHandle = default;
        _refHandle = default;
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_stringHandle.IsValid()) Addressables.Release(_stringHandle);
        if (_refHandle.IsValid()) _cubeRef.ReleaseAsset();
    }
}
