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
    private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> LoadedSpriteHandles = new Dictionary<string, AsyncOperationHandle<Sprite>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<Sprite>> PendingSpriteLoads = new Dictionary<string, Task<Sprite>>(StringComparer.OrdinalIgnoreCase);

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

    public static Task<Sprite> LoadSpriteAsync(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return Task.FromResult<Sprite>(null);
        }

        if (LoadedSprites.TryGetValue(addressKey, out Sprite cachedSprite))
        {
            return Task.FromResult(cachedSprite);
        }

        if (PendingSpriteLoads.TryGetValue(addressKey, out Task<Sprite> pending))
        {
            return pending;
        }

        Task<Sprite> loadTask = LoadSpriteInternalAsync(addressKey);
        PendingSpriteLoads[addressKey] = loadTask;
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

        foreach (var handle in LoadedSpriteHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        LoadedSprites.Clear();
        LoadedSpriteHandles.Clear();
        PendingSpriteLoads.Clear();
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

    private static async Task<Sprite> LoadSpriteInternalAsync(string addressKey)
    {
        AsyncOperationHandle<Sprite> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<Sprite>(addressKey);
            Sprite sprite = await handle.Task;
            PendingSpriteLoads.Remove(addressKey);

            if (sprite != null)
            {
                LoadedSprites[addressKey] = sprite;
                LoadedSpriteHandles[addressKey] = handle;
            }
            else
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                Debug.LogWarning("[AddressablePrefabLoader] Addressables returned null sprite for key: " + addressKey);
            }

            return sprite;
        }
        catch (Exception ex)
        {
            PendingSpriteLoads.Remove(addressKey);
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            Debug.LogWarning("[AddressablePrefabLoader] Failed to load sprite '" + addressKey + "': " + ex.Message);
            return null;
        }
    }
}
