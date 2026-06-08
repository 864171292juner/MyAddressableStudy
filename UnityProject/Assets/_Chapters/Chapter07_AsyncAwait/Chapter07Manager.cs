using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter07Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private TextMeshProUGUI _statusText;

    private AsyncOperationHandle<GameObject> _callbackHandle;
    private AsyncOperationHandle<GameObject> _awaitHandle;
    private GameObject _callbackInstance;
    private GameObject _awaitInstance;

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        _statusText = GameObject.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

        WireButton("回调方式加载", OnCallbackLoadClick);
        WireButton("async/await 加载", OnAwaitLoadClick);
        WireButton("清理", OnClearClick);
        WireButton("清空日志", () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void SetStatus(string text)
    {
        if (_statusText != null) _statusText.text = $"状态: {text}";
    }

    private void OnCallbackLoadClick()
    {
        if (_callbackHandle.IsValid()) { _log.Log("回调方式已加载，请先「清理」"); return; }
        _log.Log("【回调方式】LoadAssetAsync 开始...");
        SetStatus("加载中(回调)...");
        _callbackHandle = Addressables.LoadAssetAsync<GameObject>("DemoCube");
        _callbackHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                _callbackInstance = Instantiate(h.Result, new Vector3(-2f, 0, 0), Quaternion.identity);
                SetStatus("完成 ✓");
                _log.Log("【回调方式】加载完成 ✓ 实例在左侧");
            }
            else
            {
                SetStatus("失败 ✗");
                _callbackHandle.Release();
                _log.Log($"【回调方式】失败 ✗ {h.OperationException?.Message}");
            }
        };
    }

    private async void OnAwaitLoadClick()
    {
        {
            if (_awaitHandle.IsValid()) { _log.Log("async/await 方式已加载，请先「清理」"); return; }
            _log.Log("【async/await】LoadAssetAsync 开始...");
            SetStatus("加载中(await)...");
            _awaitHandle = Addressables.LoadAssetAsync<GameObject>("Ch05CubeC");//从cdn加载，测试【async/await】LoadAssetAsync 结束日志是否提前输出
            try
            {
                var prefab = await _awaitHandle.Task;
                _awaitInstance = Instantiate(prefab, new Vector3(2f, 0, 0), Quaternion.identity);
                SetStatus("完成 ✓");
                _log.Log("【async/await】加载完成 ✓ 实例在右侧");
            }
            catch (Exception e)
            {
                SetStatus("失败 ✗");
                _log.Log($"【async/await】失败 ✗ {e.Message}");
                Addressables.Release(_awaitHandle);
                _awaitHandle = default;
            }
        }
        {
            _log.Log("【async/await】LoadAssetAsync 结束");
        }
    }

    private void OnClearClick()
    {
        if (_callbackInstance != null) { Destroy(_callbackInstance); _callbackInstance = null; }
        if (_awaitInstance != null) { Destroy(_awaitInstance); _awaitInstance = null; }
        if (_callbackHandle.IsValid()) Addressables.Release(_callbackHandle);
        if (_awaitHandle.IsValid()) Addressables.Release(_awaitHandle);
        _callbackHandle = default;
        _awaitHandle = default;
        SetStatus("就绪");
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnDestroy()
    {
        if (_callbackHandle.IsValid()) Addressables.Release(_callbackHandle);
        if (_awaitHandle.IsValid()) Addressables.Release(_awaitHandle);
    }
}
