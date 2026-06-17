using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if !UNITY_ANDROID || UNITY_EDITOR
using VoxelBusters.ReplayKit;
#endif

/// <summary>
/// Quản lý quay màn hình đa nền tảng:
///   - Android (device): dùng MediaProjection thật qua ScreenRecorderPlugin.java
///   - iOS (device):     dùng VoxelBusters ReplayKit
///   - Editor / khác:    dummy (log only)
///
/// QUAN TRỌNG: GameObject trong Scene PHẢI được đặt tên là "ScreenRecordManager"
/// để UnitySendMessage từ Java hoạt động đúng.
/// </summary>
public class ScreenRecordManager : MonoBehaviour
{
    public static ScreenRecordManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────────────────────

    [Header("Audio Settings")]
    [Tooltip("Ghi âm thanh microphone khi quay (yêu cầu quyền RECORD_AUDIO)")]
    [SerializeField] private bool audioEnable = true;

    [Header("Duration Settings")]
    [Tooltip("Thời lượng quay tối đa (giây). 0 = không giới hạn")]
    [SerializeField] private float maxDuration = 15f;

    [Header("Save Settings")]
    [Tooltip("Tự động lưu video vào Gallery sau khi dừng quay")]
    [SerializeField] private bool autoSaveToGalleryOnStop = true;

    [Tooltip("Tự động chia sẻ video sau khi lưu vào Album (Android: sau khi lưu Gallery; iOS: ReplayKit)")]
    [SerializeField] private bool autoShareOnStop = true;

    [Header("Recording Events")]
    public UnityEvent OnRecordStarted;
    public UnityEvent OnRecordStopped;
    public UnityEvent OnRecordFailed;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────

    private bool  isRecording    = false;
    private float currentDuration = 0f;
    private string lastFilePath   = "";
    private string lastMediaStoreUri = ""; // URI từ MediaStore sau khi lưu (dùng cho chia sẻ Android)

    public bool  IsRecording           => isRecording;
    public float MaxDuration           { get => maxDuration; set => maxDuration = value; }
    public float CurrentDuration       => currentDuration;
    public float RecordingProgress     => maxDuration > 0 ? Mathf.Clamp01(currentDuration / maxDuration) : 0f;
    public string LastFilePath         => lastFilePath;

    // ──────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Warn nếu tên GameObject sai (UnitySendMessage cần đúng tên)
            if (gameObject.name != "ScreenRecordManager")
                Debug.LogWarning("[ScreenRecordManager] GameObject phải được đặt tên là 'ScreenRecordManager' " +
                                 "để callback từ Android hoạt động. Hiện tại tên là: " + gameObject.name);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
#if !UNITY_ANDROID || UNITY_EDITOR
        ReplayKitManager.DidInitialise        += DidInitialise;
        ReplayKitManager.DidRecordingStateChange += DidRecordingStateChange;
#endif
    }

    private void OnDisable()
    {
#if !UNITY_ANDROID || UNITY_EDITOR
        ReplayKitManager.DidInitialise        -= DidInitialise;
        ReplayKitManager.DidRecordingStateChange -= DidRecordingStateChange;
#endif
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroidPlugin();
        RequestAndroidPermissions();
#else
        // iOS / Editor: dùng ReplayKit
        if (ReplayKitManager.IsRecordingAPIAvailable())
            ReplayKitManager.Initialise();
        else
            Debug.LogWarning("[ScreenRecordManager] ReplayKit không khả dụng trên nền tảng này.");
#endif
    }

    private void Update()
    {
        if (!isRecording) return;

        currentDuration += Time.deltaTime;
        if (maxDuration > 0f && currentDuration >= maxDuration)
            StopRecording();
    }

    // ──────────────────────────────────────────────────────────────
    // Android – Plugin init & permissions
    // ──────────────────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject androidPlugin;   // ScreenRecorderPlugin instance

    private void InitAndroidPlugin()
    {
        try
        {
            // Ensure our Fragment is attached to the Unity Activity
            using var pluginClass = new AndroidJavaClass("com.trendingar.screenrecorder.ScreenRecorderPlugin");
            pluginClass.SetStatic("unityObjectName", gameObject.name);
            pluginClass.CallStatic("init");

            // Grab instance for subsequent calls
            androidPlugin = pluginClass.CallStatic<AndroidJavaObject>("getInstance");
            Debug.Log("[ScreenRecordManager] Android plugin initialized.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScreenRecordManager] Failed to init Android plugin: " + ex.Message);
        }
    }

    private void RequestAndroidPermissions()
    {
        var perms = new System.Collections.Generic.List<string>
        {
            UnityEngine.Android.Permission.Microphone
        };

        // Storage permissions (API-level-aware)
        int sdk = 0;
        try
        {
            using var ver = new AndroidJavaClass("android.os.Build$VERSION");
            sdk = ver.GetStatic<int>("SDK_INT");
        }
        catch { /* ignore */ }

        if (sdk >= 33)
        {
            perms.Add("android.permission.READ_MEDIA_VIDEO");
        }
        else if (sdk > 0 && sdk < 33)
        {
            perms.Add(UnityEngine.Android.Permission.ExternalStorageWrite);
            perms.Add(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        // Filter to only un-granted ones
        var missing = perms.FindAll(p =>
            !UnityEngine.Android.Permission.HasUserAuthorizedPermission(p));

        if (missing.Count > 0)
        {
            Debug.Log("[ScreenRecordManager] Requesting permissions: " + string.Join(", ", missing));
            UnityEngine.Android.Permission.RequestUserPermissions(missing.ToArray());
        }
    }
#endif

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────

    /// <summary>Bắt đầu quay màn hình.</summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            AndroidUtils.ShowToast("Đang trong quá trình quay màn hình!");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(EnsurePermissionsThenRecord());
#else
        if (!ReplayKitManager.IsRecordingAPIAvailable())
        {
            AndroidUtils.ShowToast("Thiết bị không hỗ trợ quay màn hình!");
            return;
        }
        ReplayKitManager.SetMicrophoneStatus(audioEnable);
        ReplayKitManager.StartRecording();
#endif
    }

    /// <summary>Dừng quay màn hình.</summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("[ScreenRecordManager] StopRecording gọi nhưng không đang quay.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            androidPlugin?.Call("stopRecording");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScreenRecordManager] stopRecording error: " + ex.Message);
        }
#else
        ReplayKitManager.StopRecording();
#endif
    }

    /// <summary>Lưu video gần nhất vào Gallery (Android: tự động sau stop).</summary>
    public void SaveLastRecordingToGallery()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            androidPlugin?.Call("saveToGallery");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScreenRecordManager] saveToGallery error: " + ex.Message);
        }
#else
        if (ReplayKitManager.IsPreviewAvailable())
        {
            ReplayKitManager.SavePreview(error =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    AndroidUtils.ShowToast("Đã lưu video vào Gallery!");
                }
                else
                {
                    AndroidUtils.ShowToast($"Lưu video thất bại: {error}");
                    Debug.LogError("[ScreenRecordManager] SavePreview error: " + error);
                }
            });
        }
        else
        {
            Debug.LogWarning("[ScreenRecordManager] Không có recording để lưu.");
        }
#endif
    }

    /// <summary>Chia sẻ video (đa nền tảng).</summary>
    public void ShareLastRecording(string body = "Xem video của tôi!", string subject = "Trending AR")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Dùng URI từ MediaStore được trả về sau khi lưu vào Gallery
        if (string.IsNullOrEmpty(lastMediaStoreUri))
        {
            AndroidUtils.ShowToast("Chưa có video nào để chia sẻ. Hãy quay và lưu video trước!");
            return;
        }

        try
        {
            using var uriClass      = new AndroidJavaClass("android.net.Uri");
            using var uri           = uriClass.CallStatic<AndroidJavaObject>("parse", lastMediaStoreUri);
            using var intent        = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
            using var unityPlayer   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity      = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            intent.Call<AndroidJavaObject>("setType", "video/mp4");
            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", subject);
            intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION (1)

            using var intentClass   = new AndroidJavaClass("android.content.Intent");
            using var chooser       = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, body);
            activity.Call("startActivity", chooser);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[ScreenRecordManager] ShareLastRecording error: " + ex.Message);
            AndroidUtils.ShowToast("Không thể mở popup chia sẻ: " + ex.Message);
        }
#else
        if (ReplayKitManager.IsPreviewAvailable())
            ReplayKitManager.SharePreview();
        else
            AndroidUtils.ShowToast("Không tìm thấy video để chia sẻ!");
#endif
    }

    // ──────────────────────────────────────────────────────────────
    // Android – coroutine: xin quyền mic rồi bắt đầu quay
    // ──────────────────────────────────────────────────────────────

    private IEnumerator EnsurePermissionsThenRecord()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(
                UnityEngine.Android.Permission.Microphone);

            // Poll until granted or 5 s timeout
            float t = 0f;
            while (t < 5f &&
                   !UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                       UnityEngine.Android.Permission.Microphone))
            {
                yield return new WaitForSeconds(0.3f);
                t += 0.3f;
            }
        }
#else
        yield return null;
#endif
        // Gọi Java plugin; plugin sẽ mở dialog "Cho phép quay màn hình?"
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Re-fetch plugin instance in case it was null earlier
            if (androidPlugin == null) InitAndroidPlugin();
            androidPlugin?.Call("startRecording");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScreenRecordManager] startRecording error: " + ex.Message);
            OnRecordFailed?.Invoke();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Android callbacks – called by Java via UnitySendMessage
    // ──────────────────────────────────────────────────────────────

    /// <summary>Java → quay bắt đầu thành công.</summary>
    private void OnAndroidRecordingStarted(string _)
    {
        Debug.Log("[ScreenRecordManager] OnAndroidRecordingStarted");
        isRecording    = true;
        currentDuration = 0f;
        OnRecordStarted?.Invoke();
    }

    /// <summary>Java → quay dừng lại; filePath là đường dẫn file tạm.</summary>
    private void OnAndroidRecordingStopped(string filePath)
    {
        Debug.Log("[ScreenRecordManager] OnAndroidRecordingStopped → " + filePath);
        isRecording = false;
        lastFilePath = filePath;
        OnRecordStopped?.Invoke();

        // Tự động lưu vào Gallery nếu được bật
        if (autoSaveToGalleryOnStop)
            SaveLastRecordingToGallery();
    }

    /// <summary>Java → quay thất bại (user từ chối hoặc lỗi).</summary>
    private void OnAndroidRecordingFailed(string error)
    {
        Debug.LogError("[ScreenRecordManager] OnAndroidRecordingFailed: " + error);
        isRecording = false;

        string msg = error.Contains("User denied")
            ? "Bạn đã từ chối quyền quay màn hình."
            : $"Quay thất bại: {error}";

        AndroidUtils.ShowToast(msg);
        OnRecordFailed?.Invoke();
    }

    /// <summary>Java → lưu Gallery thành công. uriString là MediaStore URI của video.</summary>
    private void OnAndroidSaveSuccess(string uriString)
    {
        Debug.Log("[ScreenRecordManager] Video đã được lưu vào Gallery (Movies/TrendingAR). URI: " + uriString);
        lastMediaStoreUri = uriString; // Lưu lại để dùng khi chia sẻ
        AndroidUtils.ShowToast("Đã lưu video vào Gallery!");

        // Tự động mở popup chia sẻ nếu cài đặt được bật
        if (autoShareOnStop)
            ShareLastRecording();
    }

    /// <summary>Java → lưu Gallery thất bại.</summary>
    private void OnAndroidSaveFailed(string error)
    {
        Debug.LogError("[ScreenRecordManager] OnAndroidSaveFailed: " + error);
        AndroidUtils.ShowToast($"Lưu video thất bại: {error}");
    }

    // ──────────────────────────────────────────────────────────────
    // iOS / Editor – ReplayKit callbacks
    // ──────────────────────────────────────────────────────────────

#if !UNITY_ANDROID || UNITY_EDITOR
    private void DidInitialise(ReplayKitInitialisationState state, string message)
    {
        Debug.Log($"[ScreenRecordManager] ReplayKit Init: {state} – {message}");
    }

    private void DidRecordingStateChange(ReplayKitRecordingState state, string message)
    {
        Debug.Log($"[ScreenRecordManager] ReplayKit State: {state} – {message}");

        switch (state)
        {
            case ReplayKitRecordingState.Started:
                isRecording     = true;
                currentDuration  = 0f;
                OnRecordStarted?.Invoke();
                break;

            case ReplayKitRecordingState.Stopped:
                isRecording = false;
                OnRecordStopped?.Invoke();
                break;

            case ReplayKitRecordingState.Failed:
                isRecording = false;
                OnRecordFailed?.Invoke();
                AndroidUtils.ShowToast($"Quay thất bại: {message}");
                break;

            case ReplayKitRecordingState.Available:
                isRecording = false;
                if (autoSaveToGalleryOnStop) SaveLastRecordingToGallery();
                if (autoShareOnStop)         ShareLastRecording();
                break;
        }
    }
#endif
}
