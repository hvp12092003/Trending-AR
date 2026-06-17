using System;
using UnityEngine;
using UnityEngine.Events;
using VoxelBusters.ReplayKit;

public class ScreenRecordManager : MonoBehaviour
{
    public static ScreenRecordManager Instance { get; private set; }

    [Header("Save Folder Settings (Legacy - ignored by ReplayKit)")]
    [SerializeField] private string saveFolderName = "TrendingAR";

    [Header("Video Configurations (Legacy - ignored by ReplayKit)")]
    [SerializeField] private int videoWidth = 720;
    [SerializeField] private int videoHeight = 0;
    [SerializeField] private int fps = 30;

    [Header("Audio Settings")]
    [Tooltip("Có cho phép ghi âm thanh không (yêu cầu quyền Microphone)")]
    [SerializeField] private bool audioEnable = true;

    [Header("Duration Settings")]
    [Tooltip("Thời lượng quay video tối đa (giây)")]
    [SerializeField] private float maxDuration = 15f;

    [Header("Recording Events")]
    public UnityEvent OnRecordStarted;
    public UnityEvent OnRecordStopped;
    public UnityEvent OnRecordFailed;

    [Header("Share Settings")]
    [Tooltip("Tự động hiển thị popup chia sẻ của hệ điều hành sau khi dừng quay")]
    [SerializeField] private bool autoShareOnStop = true;

    private bool isRecording = false;
    private float currentDuration = 0f;

    public bool IsRecording => isRecording;
    public float MaxDuration
    {
        get => maxDuration;
        set => maxDuration = value;
    }
    public float CurrentDuration => currentDuration;
    public float RecordingProgress => maxDuration > 0 ? Mathf.Clamp01(currentDuration / maxDuration) : 0f;

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
    }

    private void OnEnable()
    {
        ReplayKitManager.DidInitialise += DidInitialise;
        ReplayKitManager.DidRecordingStateChange += DidRecordingStateChange;
    }

    private void OnDisable()
    {
        ReplayKitManager.DidInitialise -= DidInitialise;
        ReplayKitManager.DidRecordingStateChange -= DidRecordingStateChange;
    }

    private void Start()
    {
        // Khởi tạo ReplayKit khi bắt đầu
        if (ReplayKitManager.IsRecordingAPIAvailable())
        {
            ReplayKitManager.Initialise();
        }
        else
        {
            Debug.LogWarning("Replay Kit recording API is not available on this platform.");
        }
    }

    private void Update()
    {
        if (isRecording)
        {
            currentDuration += Time.deltaTime;
            if (currentDuration >= maxDuration)
            {
                StopRecording();
            }
        }
    }

    /// <summary>
    /// Bắt đầu quay màn hình
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            AndroidUtils.ShowToast("Đang trong quá trình quay màn hình!");
            return;
        }

        if (!ReplayKitManager.IsRecordingAPIAvailable())
        {
            AndroidUtils.ShowToast("Thiết bị không hỗ trợ quay màn hình!");
            return;
        }

        ReplayKitManager.SetMicrophoneStatus(audioEnable);
        ReplayKitManager.StartRecording();
    }

    /// <summary>
    /// Dừng quay màn hình
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Không có phiên quay màn hình nào đang hoạt động.");
            return;
        }

        ReplayKitManager.StopRecording();
    }

    /// <summary>
    /// Chia sẻ video vừa quay gần đây nhất
    /// </summary>
    public void ShareLastRecording(string bodyText = "Xem video gameplay của tôi!", string subjectText = "Trending AR Gameplay")
    {
        if (ReplayKitManager.IsPreviewAvailable())
        {
            ReplayKitManager.SharePreview();
            Debug.Log("Shared video preview");
        }
        else
        {
            AndroidUtils.ShowToast("Không tìm thấy video nào để chia sẻ!");
        }
    }

    private void DidInitialise(ReplayKitInitialisationState state, string message)
    {
        Debug.Log($"ReplayKit Initialised: {state}. Message: {message}");
    }

    private void DidRecordingStateChange(ReplayKitRecordingState state, string message)
    {
        Debug.Log($"DidRecordingStateChange: {state}. Message: {message}");

        switch (state)
        {
            case ReplayKitRecordingState.Started:
                isRecording = true;
                currentDuration = 0f;
                OnRecordStarted?.Invoke();
                break;

            case ReplayKitRecordingState.Stopped:
                isRecording = false;
                OnRecordStopped?.Invoke();
                break;

            case ReplayKitRecordingState.Failed:
                isRecording = false;
                OnRecordFailed?.Invoke();
                AndroidUtils.ShowToast($"Quay màn hình thất bại: {message}");
                break;

            case ReplayKitRecordingState.Available:
                isRecording = false;
                if (autoShareOnStop)
                {
                    ShareLastRecording();
                }
                break;
        }
    }
}
