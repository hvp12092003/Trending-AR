using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Optional bridge to Addressables. This keeps the project compiling before the
/// Addressables package is installed, then uses it automatically when available.
/// </summary>
public static class AddressablePrefabLoader
{
    private const string AddressablesTypeName = "UnityEngine.AddressableAssets.Addressables";

    private static readonly Dictionary<string, GameObject> LoadedPrefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, object> LoadedHandles = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<GameObject>> PendingLoads = new Dictionary<string, Task<GameObject>>(StringComparer.OrdinalIgnoreCase);

    private static Type _addressablesType;
    private static MethodInfo _loadAssetMethod;
    private static MethodInfo _releaseMethod;
    private static bool _searchedForAddressables;

    public static bool IsAvailable
    {
        get
        {
            EnsureReflectionCache();
            return _addressablesType != null && _loadAssetMethod != null;
        }
    }

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
        EnsureReflectionCache();
        if (_addressablesType == null || _releaseMethod == null)
        {
            LoadedPrefabs.Clear();
            LoadedHandles.Clear();
            PendingLoads.Clear();
            return;
        }

        foreach (object handle in LoadedHandles.Values)
        {
            try
            {
                _releaseMethod.Invoke(null, new[] { handle });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AddressablePrefabLoader] Release failed: " + ex.Message);
            }
        }

        LoadedPrefabs.Clear();
        LoadedHandles.Clear();
        PendingLoads.Clear();
    }

    private static async Task<GameObject> LoadGameObjectInternalAsync(string addressKey)
    {
        EnsureReflectionCache();

        if (_addressablesType == null || _loadAssetMethod == null)
        {
            PendingLoads.Remove(addressKey);
            return null;
        }

        try
        {
            object handle = _loadAssetMethod.Invoke(null, new object[] { addressKey });
            if (handle == null)
            {
                PendingLoads.Remove(addressKey);
                return null;
            }

            PropertyInfo taskProperty = handle.GetType().GetProperty("Task");
            if (taskProperty == null)
            {
                Debug.LogWarning("[AddressablePrefabLoader] Addressables handle has no Task property.");
                PendingLoads.Remove(addressKey);
                return null;
            }

            Task<GameObject> task = taskProperty.GetValue(handle) as Task<GameObject>;
            if (task == null)
            {
                Debug.LogWarning("[AddressablePrefabLoader] Addressables task could not be read for key: " + addressKey);
                PendingLoads.Remove(addressKey);
                return null;
            }

            GameObject prefab = await task;
            PendingLoads.Remove(addressKey);

            if (prefab != null)
            {
                LoadedPrefabs[addressKey] = prefab;
                LoadedHandles[addressKey] = handle;
            }
            else
            {
                Debug.LogWarning("[AddressablePrefabLoader] Addressables returned null for key: " + addressKey);
            }

            return prefab;
        }
        catch (Exception ex)
        {
            PendingLoads.Remove(addressKey);
            Debug.LogWarning("[AddressablePrefabLoader] Failed to load '" + addressKey + "': " + ex.Message);
            return null;
        }
    }

    private static void EnsureReflectionCache()
    {
        if (_searchedForAddressables)
        {
            return;
        }

        _searchedForAddressables = true;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            _addressablesType = assemblies[i].GetType(AddressablesTypeName);
            if (_addressablesType != null)
            {
                break;
            }
        }

        if (_addressablesType == null)
        {
            return;
        }

        MethodInfo[] methods = _addressablesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != "LoadAssetAsync" || !method.IsGenericMethodDefinition)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1)
            {
                _loadAssetMethod = method.MakeGenericMethod(typeof(GameObject));
                break;
            }
        }

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != "Release" || !method.IsGenericMethodDefinition)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1)
            {
                _releaseMethod = method.MakeGenericMethod(typeof(GameObject));
                break;
            }
        }
    }
}
