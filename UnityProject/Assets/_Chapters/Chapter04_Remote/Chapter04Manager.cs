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
        _log.Log($"Cache 路径: {Caching.defaultCache.path}");

        Addressables.ResourceManager.WebRequestOverride = req =>
            _log.Log($"[HTTP] {req.url}");

        WireButton("检查 Catalog 更新", OnCheckCatalogClick);
        WireButton("更新 Catalog", OnUpdateCatalogClick);
        WireButton("加载 Remote 资源", OnLoadRemoteClick);
        WireButton("清理", OnClearClick);
        WireButton("清除 Bundle 缓存", OnClearBundleCacheClick);
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
        if (_remoteHandle.IsValid()) { _log.Log("Remote 资源已加载，请先「清理」再重新加载"); return; }

        Addressables.GetDownloadSizeAsync("RemoteCube").Completed += sizeHandle =>
        {
            long size = sizeHandle.Result;
            _log.Log(size == 0
                ? "Bundle 已在本地磁盘缓存，从本地加载"
                : $"Bundle 未缓存，将从 CDN 下载 ({size} bytes)");
            Addressables.Release(sizeHandle);

            _remoteHandle = Addressables.LoadAssetAsync<GameObject>("RemoteCube");
            _remoteHandle.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded)
                {
                    _remoteInstance = Instantiate(h.Result, Vector3.zero, Quaternion.identity);
                    _log.Log($"加载成功 ✓ {h.Result.name}");
                }
                else
                {
                    _log.Log($"失败 ✗ {h.OperationException?.Message}");
                    var inner = h.OperationException?.InnerException;
                    while (inner != null) { _log.Log($"  内部错误: {inner.Message}"); inner = inner.InnerException; }
                }
            };
        };
    }

    private void OnClearClick()
    {
        if (_remoteInstance != null) Destroy(_remoteInstance);
        if (_remoteHandle.IsValid()) Addressables.Release(_remoteHandle);
        _log.Log("实例已销毁，Handle 已释放（Bundle 磁盘缓存保留）");
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
        if (_remoteHandle.IsValid()) Addressables.Release(_remoteHandle);
    }
}
