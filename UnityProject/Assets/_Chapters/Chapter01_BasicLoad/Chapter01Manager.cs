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
        if (_cubeHandle.IsValid()) { _log.Log("Cube 已加载，请先「清理场景」再重新加载"); return; }
        _log.Log("LoadAssetAsync<GameObject>(\"DemoCube\") 开始...");
        _cubeHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        _cubeHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _cubeInstance = Instantiate(h.Result, new Vector3(-5f, 1f, 0f), Quaternion.identity);
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
        if (_spriteHandle.IsValid()) { _log.Log("Sprite 已加载，请先「清理场景」再重新加载"); return; }
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
