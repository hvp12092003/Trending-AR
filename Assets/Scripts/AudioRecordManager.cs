using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Manages the recording of a 5-second audio clip from the microphone, 
/// saving it to persistent storage, loading it asynchronously, and playing it in 2D.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioRecordManager : MonoBehaviour
{
    public static AudioRecordManager Instance { get; private set; }

    [Header("Recording Configurations")]
    [Tooltip("Strict duration of the recording in seconds.")]
    [SerializeField] private float recordDuration = 5.0f;

    [Tooltip("The name of the WAV file to save the audio clip to.")]
    [SerializeField] private string saveFileName = "recorded_voice.wav";

    [Header("Testing Overlay UI")]
    [Tooltip("Enable the on-screen modern debug overlay for quick testing.")]
    [SerializeField] private bool enableDebugUI = true;

    private AudioSource globalAudioSource;
    private AudioClip recordingClip;
    private AudioClip loadedClip;

    private bool isRecording = false;
    private bool isLoading = false;
    private bool isPlaying = false;
    private float currentRecordTime = 0.0f;

    // Cache styles for modern OnGUI styling
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private Texture2D bgTexture;
    private Texture2D btnNormalGreenTexture;
    private Texture2D btnNormalCyanTexture;
    private Texture2D btnDisabledTexture;

    public bool IsRecording => isRecording;
    public bool IsLoading => isLoading;
    public float CurrentRecordTime => currentRecordTime;
    public AudioClip LoadedClip => loadedClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize and configure global 2D AudioSource
        globalAudioSource = GetComponent<AudioSource>();
        if (globalAudioSource == null)
        {
            globalAudioSource = gameObject.AddComponent<AudioSource>();
        }
        globalAudioSource.spatialBlend = 0f; // Strictly 2D
        globalAudioSource.playOnAwake = false;
        globalAudioSource.loop = false;
    }

    private void Update()
    {
        if (globalAudioSource != null)
        {
            isPlaying = globalAudioSource.isPlaying;
        }
    }

    /// <summary>
    /// Checks if the application has authorization to use the Microphone.
    /// </summary>
    public bool HasMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        return true;
#endif
    }

    /// <summary>
    /// Requests the Microphone permission from the system.
    /// </summary>
    public void RequestMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[AudioRecordManager] Requesting Microphone Permission...");
        Permission.RequestUserPermission(Permission.Microphone);
#else
        Debug.Log("[AudioRecordManager] Microphone permissions are implicitly granted on this platform.");
#endif
    }

    /// <summary>
    /// Starts the recording process.
    /// </summary>
    public void StartRecording()
    {
        if (!HasMicrophonePermission())
        {
            RequestMicrophonePermission();
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[AudioRecordManager] No microphone devices found!");
            return;
        }

        if (isRecording || isLoading)
        {
            return;
        }

        StartCoroutine(RecordRoutine());
    }

    /// <summary>
    /// Recording sequence running for exactly recordDuration seconds.
    /// </summary>
    private IEnumerator RecordRoutine()
    {
        isRecording = true;
        currentRecordTime = 0.0f;

        // Auto-detect sample rates supported by the default microphone device
        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(null, out minFreq, out maxFreq);
        int targetFreq = 44100;
        if (maxFreq > 0)
        {
            targetFreq = Mathf.Clamp(targetFreq, minFreq, maxFreq);
        }

        Debug.Log($"[AudioRecordManager] Starting microphone recording. Frequency: {targetFreq}Hz, Duration: {recordDuration}s");
        
        // Start recording (null uses default microphone device)
        recordingClip = Microphone.Start(null, false, Mathf.CeilToInt(recordDuration), targetFreq);

        // Record for exactly the duration
        while (currentRecordTime < recordDuration)
        {
            currentRecordTime += Time.deltaTime;
            // Cap visual countdown at the limit
            if (currentRecordTime > recordDuration)
            {
                currentRecordTime = recordDuration;
            }
            yield return null;
        }

        // Finish recording
        Microphone.End(null);
        isRecording = false;
        Debug.Log("[AudioRecordManager] Recording finished.");

        // Save recorded audio data to storage
        SaveRecordingToDisk();

        // Load the recorded audio clip back asynchronously from disk
        StartCoroutine(LoadRecordingFromDiskAsync());
    }

    /// <summary>
    /// Encodes the recorded AudioClip into WAV format and saves it to local disk.
    /// </summary>
    private void SaveRecordingToDisk()
    {
        if (recordingClip == null)
        {
            Debug.LogError("[AudioRecordManager] No recorded audio clip to save.");
            return;
        }

        try
        {
            byte[] wavBytes = WavUtility.FromAudioClip(recordingClip);
            string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
            File.WriteAllBytes(filePath, wavBytes);
            Debug.Log($"[AudioRecordManager] Audio saved to disk at: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioRecordManager] Error saving audio to WAV: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the saved WAV audio clip asynchronously from local disk storage.
    /// </summary>
    private IEnumerator LoadRecordingFromDiskAsync()
    {
        isLoading = true;
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        string url = "file://" + filePath;

        Debug.Log($"[AudioRecordManager] Loading audio asynchronously from: {url}");

        using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[AudioRecordManager] Error loading audio: {webRequest.error}");
            }
            else
            {
                loadedClip = DownloadHandlerAudioClip.GetContent(webRequest);
                if (loadedClip != null)
                {
                    Debug.Log("[AudioRecordManager] Audio loaded successfully and loaded into game clip.");
                }
                else
                {
                    Debug.LogError("[AudioRecordManager] Loaded AudioClip is null.");
                }
            }
        }

        isLoading = false;
    }

    /// <summary>
    /// Plays the loaded audio clip in 2D mode through the global AudioSource.
    /// </summary>
    public void PlayRecordedAudio()
    {
        if (loadedClip == null)
        {
            Debug.LogWarning("[AudioRecordManager] No loaded audio clip to play.");
            return;
        }

        if (globalAudioSource != null)
        {
            globalAudioSource.spatialBlend = 0f; // Enforce 2D mode
            globalAudioSource.clip = loadedClip;
            globalAudioSource.Play();
            Debug.Log("[AudioRecordManager] Playing loaded audio clip in 2D mode.");
        }
    }

    #region Modern OnGUI Debug UI
    private void OnGUI()
    {
        if (!enableDebugUI) return;

        // Custom design system styles for modern look
        InitGUIStyles();

        // Overlay Card window
        Rect overlayWindow = new Rect(20, 20, 320, 290);
        GUI.Box(overlayWindow, "GHI ÂM GIỌNG NÓI (5s)", boxStyle);

        // Status description block
        Rect statusArea = new Rect(35, 60, 290, 40);
        string statusText = GetStatusMessage();
        GUI.Label(statusArea, statusText, labelStyle);

        // Button 1: Ghi Âm Button
        Rect recBtnRect = new Rect(35, 110, 290, 50);
        bool canRecord = !isRecording && !isLoading;
        
        GUIStyle recStyle = GetButtonStyle(canRecord ? btnNormalGreenTexture : btnDisabledTexture, canRecord ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        GUI.enabled = canRecord;
        if (GUI.Button(recBtnRect, isRecording ? "ĐANG GHI ÂM..." : "BẮT ĐẦU GHI ÂM", recStyle))
        {
            StartRecording();
        }
        GUI.enabled = true;

        // Button 2: Phát Ghi Âm Button
        Rect playBtnRect = new Rect(35, 175, 290, 50);
        bool canPlay = loadedClip != null && !isRecording && !isLoading;
        
        GUIStyle playStyle = GetButtonStyle(canPlay ? btnNormalCyanTexture : btnDisabledTexture, canPlay ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        GUI.enabled = canPlay;
        if (GUI.Button(playBtnRect, isPlaying ? "ĐANG PHÁT GIỌNG NÓI..." : "PHÁT GHI ÂM (2D)", playStyle))
        {
            PlayRecordedAudio();
        }
        GUI.enabled = true;

        // Visual Progress Countdown Bar for strict 5s recording
        if (isRecording)
        {
            Rect progressBgRect = new Rect(35, 240, 290, 20);
            GUI.Box(progressBgRect, "", boxStyle);

            float fillPercent = Mathf.Clamp01(currentRecordTime / recordDuration);
            Rect progressFillRect = new Rect(35, 240, fillPercent * 290, 20);
            
            // Render red progress bar block
            Texture2D redTex = MakeColorTexture(Color.red);
            GUIStyle fillStyle = new GUIStyle();
            fillStyle.normal.background = redTex;
            GUI.Box(progressFillRect, "", fillStyle);

            // Centered progress text
            GUIStyle countdownStyle = new GUIStyle(labelStyle);
            countdownStyle.normal.textColor = Color.white;
            countdownStyle.fontStyle = FontStyle.Bold;
            GUI.Label(progressBgRect, $"Đang ghi: {(recordDuration - currentRecordTime):F1}s", countdownStyle);
        }
    }

    private string GetStatusMessage()
    {
        if (isRecording)
            return "Đang thu âm giọng nói từ Microphone...\nĐang tắt các nút bấm UI.";
        if (isLoading)
            return "Đang tải file âm thanh lên game...\nVui lòng đợi.";
        if (isPlaying)
            return "Đang phát lại bản thu âm (Chế độ 2D).\nÂm lượng phát đều hai bên loa.";
        if (loadedClip != null)
            return "Đã lưu bản thu vào bộ nhớ & tải thành công!\nSẵn sàng phát lại.";
        
        if (!HasMicrophonePermission())
            return "Yêu cầu quyền truy cập Microphone.\nNhấn Bắt Đầu Ghi Âm để cấp quyền.";

        return "Microphone sẵn sàng.\nNhấn Bắt Đầu Ghi Âm (tự động thu 5s).";
    }

    private void InitGUIStyles()
    {
        if (boxStyle == null)
        {
            // Main Container Card Style
            boxStyle = new GUIStyle(GUI.skin.box);
            bgTexture = MakeColorTexture(new Color(0.12f, 0.14f, 0.18f, 0.95f));
            boxStyle.normal.background = bgTexture;
            boxStyle.alignment = TextAnchor.UpperCenter;
            boxStyle.fontSize = 14;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = new Color(0.9f, 0.95f, 1.0f);
            boxStyle.padding = new RectOffset(10, 10, 12, 10);

            // Center status labels style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            labelStyle.fontStyle = FontStyle.Normal;

            // Generate textures for buttons
            btnNormalGreenTexture = MakeColorTexture(new Color(0.18f, 0.65f, 0.35f, 1.0f));
            btnNormalCyanTexture = MakeColorTexture(new Color(0.15f, 0.55f, 0.75f, 1.0f));
            btnDisabledTexture = MakeColorTexture(new Color(0.25f, 0.28f, 0.32f, 0.8f));
        }
    }

    private GUIStyle GetButtonStyle(Texture2D background, Color textColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.background = background;
        style.fontSize = 13;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;
        return style;
    }

    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(2, 2);
        Color[] pixels = new Color[4];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        // Cleanup dynamically generated textures to avoid memory leaks
        if (bgTexture != null) Destroy(bgTexture);
        if (btnNormalGreenTexture != null) Destroy(btnNormalGreenTexture);
        if (btnNormalCyanTexture != null) Destroy(btnNormalCyanTexture);
        if (btnDisabledTexture != null) Destroy(btnDisabledTexture);
    }
    #endregion
}
