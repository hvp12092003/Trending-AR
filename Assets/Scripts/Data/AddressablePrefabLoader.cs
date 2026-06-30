using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Cầu nối nạp Prefab bất đồng bộ thông qua Unity Addressables.
/// Sử dụng trực tiếp API kiểu dữ liệu an toàn (Type-safe) thay vì Reflection để tránh xung đột kiểu.
/// </summary>
public static class AddressablePrefabLoader
{
    private static readonly Dictionary<string, GameObject> LoadedPrefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AsyncOperationHandle<GameObject>> LoadedHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<GameObject>> PendingLoads = new Dictionary<string, Task<GameObject>>(StringComparer.OrdinalIgnoreCase);

    public static bool IsAvailable => true;

    public static Task<GameObject> LoadGameObjectAsync(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return Task.FromResult<GameObject>(null);
        }

        if (LoadedPrefabs.TryGetValue(addressKey, out GameObject cachedPrefab))
        {
            return Task.FromResult(cachedPrefab);
        }

        if (PendingLoads.TryGetValue(addressKey, out Task<GameObject> pending))
        {
            return pending;
        }

        Task<GameObject> loadTask = LoadGameObjectInternalAsync(addressKey);
        PendingLoads[addressKey] = loadTask;
        return loadTask;
    }

    public static void ReleaseAll()
    {
        foreach (var handle in LoadedHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        LoadedPrefabs.Clear();
        LoadedHandles.Clear();
        PendingLoads.Clear();
    }

    private static async Task<GameObject> LoadGameObjectInternalAsync(string addressKey)
    {
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<GameObject>(addressKey);
            GameObject prefab = await handle.Task;
            PendingLoads.Remove(addressKey);

            if (prefab != null)
            {
                LoadedPrefabs[addressKey] = prefab;
                LoadedHandles[addressKey] = handle;
            }
            else
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                Debug.LogWarning("[AddressablePrefabLoader] Addressables returned null for key: " + addressKey);
            }

            return prefab;
        }
        catch (Exception ex)
        {
            PendingLoads.Remove(addressKey);
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            Debug.LogWarning("[AddressablePrefabLoader] Failed to load '" + addressKey + "': " + ex.Message);
            return null;
        }
    }
}
