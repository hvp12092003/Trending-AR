using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý và điều phối âm thanh trong Scene AR/Non-AR cho chế độ chơi Band.
/// Xử lý việc giảm âm lượng của các nhạc cụ riêng lẻ khi chơi chung và 
/// phát bài hát hoàn chỉnh (Full Song) khi có đủ 4 cast được thả ra thế giới.
/// </summary>
public class BandAudioManager : MonoBehaviour
{
    private static BandAudioManager s_Instance;

    public static BandAudioManager Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = FindFirstObjectByType<BandAudioManager>();
                if (s_Instance == null)
                {
                    GameObject obj = new GameObject("BandAudioManager");
                    s_Instance = obj.AddComponent<BandAudioManager>();
                }
            }
            return s_Instance;
        }
    }

    private AudioSource m_FullSongSource;
    private AudioClip m_FullSongClip;
    private bool m_FullSongPlayingState = false;

    private float m_UpdateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.1f; // Kiểm tra trạng thái mỗi 100ms để tối ưu hiệu năng

    private void Awake()
    {
        if (s_Instance == null)
        {
            s_Instance = this;
        }
        else if (s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Khởi tạo AudioSource tổng cho bài hát hoàn chỉnh
        m_FullSongSource = gameObject.GetComponent<AudioSource>();
        if (m_FullSongSource == null)
        {
            m_FullSongSource = gameObject.AddComponent<AudioSource>();
        }
        m_FullSongSource.loop = true;
        m_FullSongSource.playOnAwake = false;
        m_FullSongSource.volume = 1f;
    }

    /// <summary>
    /// Tạo hoặc lấy instance và gán bài hát hoàn chỉnh.
    /// </summary>
    public static BandAudioManager GetOrCreateInstance(AudioClip fullSongClip)
    {
        BandAudioManager manager = Instance;
        if (fullSongClip != null)
        {
            manager.SetFullSongClip(fullSongClip);
        }
        return manager;
    }

    public void SetFullSongClip(AudioClip clip)
    {
        m_FullSongClip = clip;
        if (m_FullSongSource != null)
        {
            m_FullSongSource.clip = clip;
        }
        Debug.Log($"[BandAudioManager] Đã nạp bài hát hoàn chỉnh: {(clip != null ? clip.name : "null")}");
    }

    private void Update()
    {
        m_UpdateTimer += Time.deltaTime;
        if (m_UpdateTimer >= UPDATE_INTERVAL)
        {
            m_UpdateTimer = 0f;
            UpdateAudioStates();
        }
    }

    private void UpdateAudioStates()
    {
        // 1. Tìm tất cả các CastAudioData hiện có trong scene
        CastAudioData[] allCasts = FindObjectsByType<CastAudioData>(FindObjectsSortMode.None);
        if (allCasts == null || allCasts.Length == 0)
        {
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
            }
            return;
        }

        // 2. Phân loại các Cast đã được đặt (placed) ra thế giới AR/Non-AR
        // Nhân vật được xem là placed khi không còn thuộc cha nào (transform.parent == null)
        List<CastAudioData> placedCasts = new List<CastAudioData>();
        List<CastAudioData> unplacedCasts = new List<CastAudioData>();

        foreach (var cast in allCasts)
        {
            if (cast == null || cast.preparedSource == null) continue;

            if (cast.transform.parent == null)
            {
                placedCasts.Add(cast);
            }
            else
            {
                unplacedCasts.Add(cast);
            }
        }

        int placedCount = placedCasts.Count;

        // 3. Thực hiện logic điều phối dựa trên số lượng Cast đã đặt
        if (placedCount == 0)
        {
            // Mute / Dừng tất cả nếu chưa có Cast nào thả ra
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
            }

            foreach (var cast in allCasts)
            {
                if (cast.preparedSource.isPlaying)
                {
                    cast.preparedSource.Pause();
                }
            }
        }
        else
        {
            // Dừng bài hát hoàn chỉnh (vì chúng ta sẽ phát các nhạc cụ đơn lẻ kết hợp lại)
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
                Debug.Log("[BandAudioManager] Đã dừng phát bài hát hoàn chỉnh để phát các nhạc cụ riêng lẻ.");
            }

            // Xử lý các Cast chưa được đặt: đảm bảo chúng KHÔNG phát tiếng
            foreach (var cast in unplacedCasts)
            {
                if (cast.preparedSource.isPlaying)
                {
                    cast.preparedSource.Pause();
                }
            }

            // Tìm thời gian phát tham chiếu (đã phát và ở trạng thái ổn định)
            float referenceTime = -1f;
            foreach (var cast in placedCasts)
            {
                if (cast.preparedSource != null && cast.preparedSource.isPlaying && cast.preparedSource.time > 0.05f)
                {
                    referenceTime = cast.preparedSource.time;
                    break;
                }
            }

            // Xử lý các Cast đã được đặt ra thế giới: Phát và đồng bộ hóa thời gian phát
            foreach (var cast in placedCasts)
            {
                if (cast.preparedSource == null) continue;

                // Cho phát âm thanh nhạc cụ riêng lẻ
                if (!cast.preparedSource.isPlaying)
                {
                    cast.preparedSource.Play();
                    if (referenceTime >= 0f && 
                        cast.preparedSource.clip != null && 
                        cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && 
                        referenceTime < cast.preparedSource.clip.length)
                    {
                        cast.preparedSource.time = referenceTime;
                        Debug.Log($"[BandAudioManager] Đồng bộ phát Cast '{cast.gameObject.name}' theo thời gian tham chiếu: {referenceTime}");
                    }
                }
                else
                {
                    // Đồng bộ lại nếu lệch pha quá nhiều (> 0.15s)
                    if (referenceTime >= 0f && 
                        cast.preparedSource.clip != null && 
                        cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && 
                        referenceTime < cast.preparedSource.clip.length && 
                        Mathf.Abs(cast.preparedSource.time - referenceTime) > 0.15f)
                    {
                        cast.preparedSource.time = referenceTime;
                        Debug.Log($"[BandAudioManager] Đồng bộ lại Cast '{cast.gameObject.name}' bị lệch pha (reference: {referenceTime}, current: {cast.preparedSource.time})");
                    }
                }

                // Đảm bảo không bị mute
                cast.preparedSource.mute = false;

                // Cập nhật âm lượng dựa trên số lượng Cast đặt ra
                if (placedCount == 1)
                {
                    // Chỉ có 1 Cast: Phát với âm lượng gốc đầy đủ
                    cast.preparedSource.volume = cast.originalVolume;
                }
                else
                {
                    // Có nhiều Cast chơi chung: Giảm âm lượng các nhạc cụ được tích chọn để tránh loạn tiếng
                    if (cast.reduceVolumeWhenTogether)
                    {
                        float targetVol = cast.originalVolume * (1f - cast.reduceAmount);
                        cast.preparedSource.volume = Mathf.Clamp01(targetVol);
                    }
                    else
                    {
                        // Giữ nguyên âm lượng (ví dụ: ca sĩ hát)
                        cast.preparedSource.volume = cast.originalVolume;
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }
}
