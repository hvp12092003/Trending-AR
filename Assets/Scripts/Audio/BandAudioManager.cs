using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý và điều phối âm thanh trong Scene AR/Non-AR cho chế độ chơi Band.
/// Xử lý việc giảm âm lượng của các nhạc cụ riêng lẻ khi chơi chung và 
/// phát bài hát hoàn chỉnh (Full Song) khi có đủ 4 cast được thả ra thế giới.
/// Dùng pattern Register/Unregister thay vì FindObjectsByType để tối ưu hiệu năng.
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
    private const float UPDATE_INTERVAL = 0.1f; // Kiểm tra trạng thái mỗi 100ms

    // Số lượng Cast cần đặt đủ để chuyển sang phát bài hát hoàn chỉnh
    private const int FULL_BAND_COUNT = 4;

    // Cache trạng thái thông báo để tránh gọi lặp
    private bool m_WasFullBandActive = false;

    // ── Registered Lists (thay thế FindObjectsByType) ──
    private readonly List<CastAudioData> m_RegisteredCasts = new List<CastAudioData>();
    private readonly List<ModelLoopEffects> m_RegisteredEffects = new List<ModelLoopEffects>();

    // Buffer tái sử dụng để tránh allocate mỗi frame
    private readonly List<CastAudioData> m_PlacedCasts = new List<CastAudioData>();
    private readonly List<CastAudioData> m_UnplacedCasts = new List<CastAudioData>();

    // ── Public API ──

    /// <summary>
    /// Trả về AudioSource đang phát bài hát hoàn chỉnh để các ModelLoopEffects có thể đồng bộ.
    /// </summary>
    public AudioSource GetFullSongSource() => m_FullSongSource;

    /// <summary>
    /// Trả về thời gian phát hiện tại của Cast đang phát ổn định nhất.
    /// Dùng để đồng bộ tức thì khi một Cast mới được thả ra.
    /// Trả về -1f nếu chưa có Cast nào đang phát.
    /// </summary>
    public float GetCurrentReferenceTime()
    {
        foreach (var cast in m_RegisteredCasts)
        {
            if (cast == null || cast.preparedSource == null) continue;
            if (cast.transform.parent == null &&
                cast.preparedSource.isPlaying &&
                cast.preparedSource.time > 0.05f)
            {
                return cast.preparedSource.time;
            }
        }
        return -1f;
    }

    /// <summary>
    /// Đăng ký một CastAudioData mới vào hệ thống quản lý âm thanh.
    /// Gọi từ CastAudioData.Start().
    /// </summary>
    public void RegisterCast(CastAudioData cast)
    {
        if (cast != null && !m_RegisteredCasts.Contains(cast))
        {
            m_RegisteredCasts.Add(cast);
        }
    }

    /// <summary>
    /// Hủy đăng ký CastAudioData khỏi hệ thống quản lý âm thanh.
    /// Gọi từ CastAudioData.OnDestroy().
    /// </summary>
    public void UnregisterCast(CastAudioData cast)
    {
        m_RegisteredCasts.Remove(cast);
    }

    /// <summary>
    /// Đăng ký một ModelLoopEffects vào danh sách nhận thông báo chuyển AudioSource.
    /// Gọi từ ModelLoopEffects.Start().
    /// </summary>
    public void RegisterModelLoopEffect(ModelLoopEffects effect)
    {
        if (effect != null && !m_RegisteredEffects.Contains(effect))
        {
            m_RegisteredEffects.Add(effect);
        }
    }

    /// <summary>
    /// Hủy đăng ký ModelLoopEffects khỏi danh sách thông báo.
    /// Gọi từ ModelLoopEffects.OnDestroy().
    /// </summary>
    public void UnregisterModelLoopEffect(ModelLoopEffects effect)
    {
        m_RegisteredEffects.Remove(effect);
    }

    // ── Unity Lifecycle ──

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
        // Dọn null khỏi danh sách đã đăng ký (trường hợp object bị Destroy mà không kịp Unregister)
        m_RegisteredCasts.RemoveAll(c => c == null);

        if (m_RegisteredCasts.Count == 0)
        {
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
            }
            if (m_WasFullBandActive)
            {
                NotifyAllModelLoopEffects(false);
                m_WasFullBandActive = false;
            }
            return;
        }

        // Phân loại Cast đã đặt / chưa đặt — tái sử dụng buffer để tránh GC
        m_PlacedCasts.Clear();
        m_UnplacedCasts.Clear();

        foreach (var cast in m_RegisteredCasts)
        {
            if (cast.preparedSource == null) continue;

            if (cast.transform.parent == null)
                m_PlacedCasts.Add(cast);
            else
                m_UnplacedCasts.Add(cast);
        }

        int placedCount = m_PlacedCasts.Count;

        // ── Logic điều phối âm thanh theo số lượng Cast đã đặt ──

        if (placedCount == 0)
        {
            // Chưa có Cast nào: dừng tất cả
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
            }

            foreach (var cast in m_RegisteredCasts)
            {
                if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                    cast.preparedSource.Pause();
            }

            if (m_WasFullBandActive)
            {
                NotifyAllModelLoopEffects(false);
                m_WasFullBandActive = false;
            }
        }
        else if (placedCount >= FULL_BAND_COUNT)
        {
            // ── ĐỦ 4 CAST: Phát FullSong ──

            // Dừng tất cả nhạc cụ riêng lẻ
            foreach (var cast in m_RegisteredCasts)
            {
                if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                {
                    cast.preparedSource.Stop();
                    Debug.Log($"[BandAudioManager] Dừng nhạc cụ riêng lẻ: {cast.gameObject.name}");
                }
            }

            // Phát FullSong nếu chưa đang phát
            if (m_FullSongSource != null)
            {
                if (m_FullSongClip == null)
                {
                    Debug.LogWarning("[BandAudioManager] Đủ 4 Cast nhưng FullSongClip chưa được gán! Kiểm tra EditorBandData.fullSongAudio.");
                }
                else if (!m_FullSongSource.isPlaying)
                {
                    m_FullSongSource.clip = m_FullSongClip;
                    m_FullSongSource.Play();
                    m_FullSongPlayingState = true;
                    Debug.Log($"[BandAudioManager] Đã đặt đủ {FULL_BAND_COUNT} Cast → Bắt đầu phát FullSong: {m_FullSongClip.name}");
                }
            }

            // Thông báo ModelLoopEffects trỏ về AudioSource FullSong (chỉ 1 lần khi chuyển trạng thái)
            if (!m_WasFullBandActive)
            {
                NotifyAllModelLoopEffects(true);
                m_WasFullBandActive = true;
            }
        }
        else
        {
            // ── 1-3 CAST: Phát nhạc cụ riêng lẻ ──

            // Dừng FullSong nếu đang phát
            if (m_FullSongSource != null && m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Stop();
                m_FullSongPlayingState = false;
                Debug.Log("[BandAudioManager] Chưa đủ 4 Cast → Dừng FullSong, chuyển về phát nhạc cụ riêng lẻ.");
            }

            // Reset ModelLoopEffects nếu trước đó đã là FullBand
            if (m_WasFullBandActive)
            {
                NotifyAllModelLoopEffects(false);
                m_WasFullBandActive = false;
            }

            // Dừng tiếng Cast chưa đặt
            foreach (var cast in m_UnplacedCasts)
            {
                if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                    cast.preparedSource.Pause();
            }

            // Tìm thời gian phát tham chiếu từ Cast đang phát ổn định nhất
            float referenceTime = -1f;
            foreach (var cast in m_PlacedCasts)
            {
                if (cast.preparedSource != null &&
                    cast.preparedSource.isPlaying &&
                    cast.preparedSource.time > 0.05f)
                {
                    referenceTime = cast.preparedSource.time;
                    break;
                }
            }

            // Xử lý từng Cast đã đặt: phát + đồng bộ thời gian
            foreach (var cast in m_PlacedCasts)
            {
                if (cast.preparedSource == null) continue;

                if (!cast.preparedSource.isPlaying)
                {
                    // Cast chưa phát (hiếm sau khi có sync tức thì trong PlayAudio) → phát và sync
                    cast.preparedSource.Play();
                    if (referenceTime >= 0f &&
                        cast.preparedSource.clip != null &&
                        cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded &&
                        referenceTime < cast.preparedSource.clip.length)
                    {
                        cast.preparedSource.time = referenceTime;
                        Debug.Log($"[BandAudioManager] Đồng bộ phát Cast '{cast.gameObject.name}' → t={referenceTime:F3}s");
                    }
                }
                else if (referenceTime >= 0f &&
                         cast.preparedSource.clip != null &&
                         cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded &&
                         referenceTime < cast.preparedSource.clip.length &&
                         Mathf.Abs(cast.preparedSource.time - referenceTime) > 0.15f)
                {
                    // Drift correction: đồng bộ lại nếu lệch hơn 0.15s
                    cast.preparedSource.time = referenceTime;
                    Debug.Log($"[BandAudioManager] Drift correction '{cast.gameObject.name}': {cast.preparedSource.time:F3}s → {referenceTime:F3}s");
                }

                // Đảm bảo không bị mute
                cast.preparedSource.mute = false;

                // Điều chỉnh âm lượng theo số Cast đang chơi chung
                if (placedCount == 1)
                {
                    cast.preparedSource.volume = cast.originalVolume;
                }
                else if (cast.reduceVolumeWhenTogether)
                {
                    cast.preparedSource.volume = Mathf.Clamp01(cast.originalVolume * (1f - cast.reduceAmount));
                }
                else
                {
                    cast.preparedSource.volume = cast.originalVolume;
                }
            }
        }
    }

    /// <summary>
    /// Broadcast sang tất cả ModelLoopEffects đã đăng ký để chuyển/khôi phục AudioSource.
    /// isFullBand = true  → trỏ tới FullSong AudioSource (nháy đồng bộ nhạc full).
    /// isFullBand = false → khôi phục về AudioSource nhạc cụ riêng lẻ gốc.
    /// </summary>
    /// <summary>
    /// Dừng toàn bộ âm thanh: FullSong và tất cả Cast đã đăng ký.
    /// Gọi khi người dùng thoát khỏi chế độ AR hoặc reset scene.
    /// </summary>
    public void StopAll()
    {
        // Dừng FullSong
        if (m_FullSongSource != null && m_FullSongSource.isPlaying)
        {
            m_FullSongSource.Stop();
            m_FullSongPlayingState = false;
        }

        // Dừng tất cả Cast
        foreach (var cast in m_RegisteredCasts)
        {
            if (cast == null) continue;
            if (cast.preparedSource != null && cast.preparedSource.isPlaying)
            {
                cast.preparedSource.Stop();
            }
        }

        // Reset trạng thái
        if (m_WasFullBandActive)
        {
            NotifyAllModelLoopEffects(false);
            m_WasFullBandActive = false;
        }

        Debug.Log("[BandAudioManager] StopAll: Đã dừng toàn bộ âm thanh.");
    }

    private void NotifyAllModelLoopEffects(bool isFullBand)
    {
        // Dọn null
        m_RegisteredEffects.RemoveAll(e => e == null);

        int count = m_RegisteredEffects.Count;
        if (count == 0) return;

        foreach (var effect in m_RegisteredEffects)
        {
            if (isFullBand)
                effect.SetAudioSourceForBandFull(m_FullSongSource);
            else
                effect.ResetAudioSourceToInstrument();
        }

        Debug.Log($"[BandAudioManager] Đã thông báo {count} ModelLoopEffects → isFullBand={isFullBand}");
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }
}
