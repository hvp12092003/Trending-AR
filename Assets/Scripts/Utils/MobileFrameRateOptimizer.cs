using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class MobileFrameRateOptimizer : MonoBehaviour
{
    private const int TargetFrameRate = 60;
    private const int PreferredMinCameraWidth = 1280;
    private const int PreferredMinCameraHeight = 720;
    private const float RetuneInterval = 1f;

    private static MobileFrameRateOptimizer s_Instance;

    private float m_NextRetuneTime;
    private string m_LastCameraConfigLogKey;
    private string m_LastUnavailableLogKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (s_Instance != null)
        {
            return;
        }

        GameObject optimizerObject = new GameObject(nameof(MobileFrameRateOptimizer));
        DontDestroyOnLoad(optimizerObject);
        s_Instance = optimizerObject.AddComponent<MobileFrameRateOptimizer>();
    }

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyFrameBudget();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ARSession.stateChanged += OnARSessionStateChanged;
    }

    private void Update()
    {
        if (Time.unscaledTime < m_NextRetuneTime)
        {
            return;
        }

        m_NextRetuneTime = Time.unscaledTime + RetuneInterval;
        Retune();
    }

    private void OnDestroy()
    {
        if (s_Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        ARSession.stateChanged -= OnARSessionStateChanged;
        s_Instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Retune();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            Retune();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Retune();
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        Retune();
    }

    private void Retune()
    {
        ApplyFrameBudget();

        // Disable AR's automatic frame-rate matching until a 60 Hz camera
        // configuration is selected. Otherwise many Android devices clamp to 30.
        ApplyARSessionFrameRatePolicy(false);
        bool has60HzARCamera = TryApplyBestARCameraConfiguration();
        ApplyARSessionFrameRatePolicy(has60HzARCamera);

        ApplyFrameBudget();
    }

    private static void ApplyFrameBudget()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
        Time.fixedDeltaTime = 1f / TargetFrameRate;
    }

    private static void ApplyARSessionFrameRatePolicy(bool matchARCameraFrameRate)
    {
        ARSession[] sessions = FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARSession session in sessions)
        {
            if (session == null)
            {
                continue;
            }

            session.matchFrameRateRequested = matchARCameraFrameRate;
        }
    }

    private bool TryApplyBestARCameraConfiguration()
    {
        bool found60HzConfiguration = false;
        ARCameraManager[] cameraManagers = FindObjectsByType<ARCameraManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ARCameraManager cameraManager in cameraManagers)
        {
            if (cameraManager == null || !cameraManager.isActiveAndEnabled || cameraManager.subsystem == null)
            {
                continue;
            }

            NativeArray<XRCameraConfiguration> configurations = cameraManager.GetConfigurations(Allocator.Temp);
            try
            {
                if (!TryChooseBestConfiguration(configurations, out XRCameraConfiguration bestConfiguration))
                {
                    LogUnavailableOnce(cameraManager, "no-fps-config");
                    continue;
                }

                bool is60Hz = bestConfiguration.framerate.GetValueOrDefault() >= TargetFrameRate;
                found60HzConfiguration |= is60Hz;

                XRCameraConfiguration? currentConfiguration = cameraManager.currentConfiguration;
                if (!currentConfiguration.HasValue || currentConfiguration.Value != bestConfiguration)
                {
                    cameraManager.currentConfiguration = bestConfiguration;
                }

                LogConfigurationOnce(cameraManager, bestConfiguration, is60Hz);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MobileFrameRateOptimizer] Could not apply AR camera configuration: {ex.Message}");
            }
            finally
            {
                if (configurations.IsCreated)
                {
                    configurations.Dispose();
                }
            }
        }

        return found60HzConfiguration;
    }

    private static bool TryChooseBestConfiguration(NativeArray<XRCameraConfiguration> configurations, out XRCameraConfiguration bestConfiguration)
    {
        bestConfiguration = default;
        bool found = false;
        int bestTier = -1;
        int bestQualityTier = -1;
        int bestFrameRate = 0;
        long bestArea = long.MaxValue;

        for (int i = 0; i < configurations.Length; i++)
        {
            XRCameraConfiguration configuration = configurations[i];
            int frameRate = configuration.framerate.GetValueOrDefault();
            if (frameRate <= 0)
            {
                continue;
            }

            long area = (long)configuration.width * configuration.height;
            int tier = frameRate >= TargetFrameRate ? 2 : 1;
            int qualityTier = configuration.width >= PreferredMinCameraWidth &&
                              configuration.height >= PreferredMinCameraHeight ? 1 : 0;

            bool shouldUse =
                !found ||
                tier > bestTier ||
                (tier == bestTier && qualityTier > bestQualityTier) ||
                (tier == bestTier && qualityTier == bestQualityTier && IsBetterWithinTier(tier, frameRate, area, bestFrameRate, bestArea));

            if (!shouldUse)
            {
                continue;
            }

            bestConfiguration = configuration;
            found = true;
            bestTier = tier;
            bestQualityTier = qualityTier;
            bestFrameRate = frameRate;
            bestArea = area;
        }

        return found;
    }

    private static bool IsBetterWithinTier(int tier, int frameRate, long area, int bestFrameRate, long bestArea)
    {
        if (tier == 2)
        {
            return area < bestArea;
        }

        return frameRate > bestFrameRate || (frameRate == bestFrameRate && area < bestArea);
    }

    private void LogConfigurationOnce(ARCameraManager cameraManager, XRCameraConfiguration configuration, bool is60Hz)
    {
        string key = $"{cameraManager.GetInstanceID()}:{configuration.width}x{configuration.height}:{configuration.framerate.GetValueOrDefault()}";
        if (m_LastCameraConfigLogKey == key)
        {
            return;
        }

        m_LastCameraConfigLogKey = key;
        string status = is60Hz ? "using" : "device has no 60 Hz config, using best available";
        Debug.Log($"[MobileFrameRateOptimizer] {status}: {configuration}");
    }

    private void LogUnavailableOnce(ARCameraManager cameraManager, string reason)
    {
        string key = $"{cameraManager.GetInstanceID()}:{reason}";
        if (m_LastUnavailableLogKey == key)
        {
            return;
        }

        m_LastUnavailableLogKey = key;
        Debug.LogWarning("[MobileFrameRateOptimizer] AR camera configurations did not expose frame-rate data on this device/provider.");
    }
}
