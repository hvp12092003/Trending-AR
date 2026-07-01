using UnityEngine;
using System;

/// <summary>
/// Quản lý nhịp nhạc (BPM) toàn cục để đồng bộ chuyển động nhảy của các nhân vật.
/// Thay thế cho cơ chế timeline của ScreenRecordManager cũ.
/// </summary>
public class MusicSyncManager : MonoBehaviour
{
    public static MusicSyncManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("AudioSource để phát nhạc. Nếu để trống, hệ thống sẽ tự động tìm kiếm trong Scene.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Nhịp nhạc mỗi phút (Beats Per Minute) để đồng bộ điệu nhảy.")]
    [SerializeField] private float bpm = 120f;

    [Tooltip("Tự động phát nhạc khi bắt đầu.")]
    [SerializeField] private bool playOnStart = true;

    /// <summary>
    /// Sự kiện kích hoạt tại mỗi nhịp nhạc (phách nhạc).
    /// </summary>
    public event Action OnBeat;

    /// <summary>
    /// Cho phép các script khác truy cập AudioSource phát nhạc nền chính.
    /// </summary>
    public AudioSource AudioSource => audioSource;

    /// <summary>
    /// Thời gian đã trôi qua kể từ beat cuối cùng.
    /// </summary>
    public float BeatTimer => beatTimer;

    /// <summary>
    /// Khoảng thời gian giữa các beat nhạc (giây).
    /// </summary>
    public float BeatInterval => beatInterval;

    /// <summary>
    /// Nhịp nhạc BPM hiện tại.
    /// </summary>
    public float Bpm => bpm;

    private float beatInterval;
    private float beatTimer;

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

        // Tự động tìm AudioSource nếu chưa được cấu hình
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = FindFirstObjectByType<AudioSource>();
            }
        }
    }

    private void Start()
    {
        UpdateBeatInterval();

        if (playOnStart && audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        // Sử dụng thời gian trôi qua thực tế
        if (audioSource == null || !audioSource.isPlaying) return;

        beatTimer += Time.deltaTime;
        if (beatTimer >= beatInterval)
        {
            beatTimer -= beatInterval;
            OnBeat?.Invoke();
        }
    }

    /// <summary>
    /// Thiết lập BPM mới và cập nhật khoảng thời gian beat.
    /// </summary>
    public void SetBPM(float newBpm)
    {
        bpm = newBpm;
        UpdateBeatInterval();
    }

    /// <summary>
    /// Cho phép set lại AudioSource mà MusicSyncManager theo dõi để fire OnBeat.
    /// Dùng khi AudioPanelController phát preview nhạc qua AudioSource riêng của nó.
    /// Truyền null để reset về AudioSource gốc (nếu có).
    /// </summary>
    public void SetAudioSource(AudioSource source)
    {
        audioSource = source;
        beatTimer = 0f;
        Debug.Log($"[MusicSyncManager] AudioSource đã được cập nhật: {(source != null ? source.gameObject.name : "null")}");
    }

    private void UpdateBeatInterval()
    {
        beatInterval = 60f / bpm;
        beatTimer = 0f;
    }

    /// <summary>
    /// Lấy thời điểm phát nhạc hiện tại.
    /// </summary>
    public float GetPlaybackTime()
    {
        return audioSource != null ? audioSource.time : 0f;
    }

    /// <summary>
    /// Reset phát nhạc về 0 và đồng bộ lại beat đầu tiên (dùng khi chạm đúp reset điệu nhảy).
    /// </summary>
    public void ResetPlayback()
    {
        if (audioSource != null)
        {
            audioSource.time = 0f;
            audioSource.Play();
        }
        beatTimer = 0f;
        OnBeat?.Invoke(); // Kích hoạt beat ngay khi reset để khớp nhịp
    }
}
