using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;

public class Chapter05Manager : MonoBehaviour
{
    private DebugLogPanel _log;
    private TextMeshProUGUI _progressText;
    private AsyncOperationHandle _downloadHandle;
    private AsyncOperationHandle<IList<GameObject>> _loadHandle;
    private readonly List<GameObject> _instances = new List<GameObject>();

    private void Start()
    {
        _log = FindObjectOfType<DebugLogPanel>();
        _progressText = GameObject.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();

        WireButton("检查下载大小",     OnCheckSizeClick);
        WireButton("预下载",          OnPreDownloadClick);
        WireButton("加载资产",         OnLoadAssetsClick);
        WireButton("清理",            OnClearClick);
        WireButton("清除 Bundle 缓存", OnClearBundleCacheClick);
        WireButton("清空日志",         () => _log.Clear());
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject.Find(name)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    private void OnCheckSizeClick()
    {
        _log.Log("GetDownloadSizeAsync(\"Chapter05\") 开始...");
        Addressables.GetDownloadSizeAsync("Chapter05").Completed += handle =>
        {
            long size = handle.Result;
            _log.Log(size == 0
                ? "所有资产已缓存，无需下载 ✓"
                : $"需要下载 {size} bytes，点「预下载」开始");
            Addressables.Release(handle);
        };
    }

    private void OnPreDownloadClick()
    {
        if (_downloadHandle.IsValid()) { _log.Log("正在下载中，请稍候..."); return; }
        _log.Log("DownloadDependenciesAsync(\"Chapter05\") 开始...");
        _downloadHandle = Addressables.DownloadDependenciesAsync("Chapter05");
        StartCoroutine(TrackProgress(_downloadHandle));
        _downloadHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
                _log.Log("预下载完成 ✓ 可以点「加载资产」");
            else
                _log.Log($"下载失败 ✗ {h.OperationException?.Message}");
            Addressables.Release(_downloadHandle);
            _downloadHandle = default;
        };
    }

    private IEnumerator TrackProgress(AsyncOperationHandle handle)
    {
        while (!handle.IsDone)
        {
            if (_progressText != null)
                _progressText.text = $"下载进度: {handle.PercentComplete * 100:F0}%";
            yield return null;
        }
        if (_progressText != null)
            _progressText.text = "下载进度: 100%";
    }

    private void OnLoadAssetsClick()
    {
        if (_loadHandle.IsValid()) { _log.Log("资产已加载，请先「清理」再重新加载"); return; }
        _log.Log("LoadAssetsAsync<GameObject>(\"Chapter05\") 开始...");
        _loadHandle = Addressables.LoadAssetsAsync<GameObject>("Chapter05", null);
        StartCoroutine(TrackLoadProgress(_loadHandle));
        _loadHandle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                for (int i = 0; i < h.Result.Count; i++)
                    _instances.Add(Instantiate(h.Result[i],
                        new Vector3((i - 1) * 2.5f, 0, 0), Quaternion.identity));
                _log.Log($"加载完成 ✓ 共 {h.Result.Count} 个资产");
            }
            else
            {
                _log.Log($"加载失败 ✗ {h.OperationException?.Message}");
                Addressables.Release(_loadHandle);
                _loadHandle = default;
            }
        };
    }

    private IEnumerator TrackLoadProgress(AsyncOperationHandle<IList<GameObject>> handle)
    {
        while (!handle.IsDone)
        {
            if (_progressText != null)
                _progressText.text = $"加载进度: {handle.PercentComplete * 100:F0}%";
            yield return null;
        }
        if (_progressText != null)
            _progressText.text = "加载进度: 100%";
    }

    private void OnClearClick()
    {
        foreach (var inst in _instances) Destroy(inst);
        _instances.Clear();
        if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
        _loadHandle = default;
        _log.Log("实例已销毁，Handle 已释放");
    }

    private void OnClearBundleCacheClick()
    {
        bool cleared = Caching.ClearCache();
        _log.Log(cleared
            ? "Bundle 磁盘缓存已清除 ✓ 下次加载将从 CDN 重新下载"
            : "清除失败 — 可能有 Bundle 正在被使用，先「清理」再试");
    }

    private void OnDestroy()
    {
        if (_downloadHandle.IsValid()) Addressables.Release(_downloadHandle);
        if (_loadHandle.IsValid()) Addressables.Release(_loadHandle);
    }
}
