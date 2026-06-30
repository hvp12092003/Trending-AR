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
    private int m_LastPlacedCount = 0;

    // ── Registered Lists (thay thế FindObjectsByType) ──
    private readonly List<CastAudioData> m_RegisteredCasts = new List<CastAudioData>();
    private readonly List<ModelLoopEffects> m_RegisteredEffects = new List<ModelLoopEffects>();
    private readonly Dictionary<CastAudioData, CastPlacementState> m_PlacementStateCache = new Dictionary<CastAudioData, CastPlacementState>();
    private readonly Dictionary<CastAudioData, Move> m_MoveCache = new Dictionary<CastAudioData, Move>();
    private readonly Dictionary<GameObject, Renderer[]> m_RendererCache = new Dictionary<GameObject, Renderer[]>();
    private MaterialPropertyBlock m_PropertyBlock;

    // Buffer tái sử dụng để tránh allocate mỗi frame
    private readonly List<CastAudioData> m_PlacedCasts = new List<CastAudioData>();
    private readonly List<CastAudioData> m_UnplacedCasts = new List<CastAudioData>();

    // Trạng thái cho tính năng Highlight/Solo khi ẩn UI để quay phim
    private bool m_WasUiHidden = false;
    private GameObject m_LastHighlightedCast = null;
    private BandARSpawner m_CachedSpawner;
    private BandPanelController m_CachedBandPanel;
    private CustomCharacterPanelController m_CachedCustomPanel;

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
            if (IsCastPlaced(cast) &&
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
            m_PlacementStateCache[cast] = cast.GetComponent<CastPlacementState>();
            m_MoveCache[cast] = cast.GetComponent<Move>();
        }
    }

    /// <summary>
    /// Hủy đăng ký CastAudioData khỏi hệ thống quản lý âm thanh.
    /// Gọi từ CastAudioData.OnDestroy().
    /// </summary>
    public void UnregisterCast(CastAudioData cast)
    {
        m_RegisteredCasts.Remove(cast);
        if (cast != null)
        {
            m_PlacementStateCache.Remove(cast);
            m_MoveCache.Remove(cast);
            m_RendererCache.Remove(cast.gameObject);
        }
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
        m_PropertyBlock = new MaterialPropertyBlock();
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
        CleanupDestroyedCasts();

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

            if (IsCastPlaced(cast))
                m_PlacedCasts.Add(cast);
            else
                m_UnplacedCasts.Add(cast);
        }

        int placedCount = m_PlacedCasts.Count;

        // Kiểm tra xem có đang ở chế độ Custom hay không
        bool isCustomMode = IsCustomMode();

        // Tự động đồng bộ âm thanh và hoạt ảnh về 0 khi số lượng Cast đã thả thay đổi (chỉ cho chế độ chơi Band)
        if (!isCustomMode && placedCount != m_LastPlacedCount)
        {
            m_LastPlacedCount = placedCount;
            ResetAllAudioAndAnimations();
        }
        else
        {
            m_LastPlacedCount = placedCount;
        }

        // ── Logic điều phối âm thanh & Highlight ──
        bool isUiHidden = IsAnyUiHidden();
        GameObject currentSelected = (CharacterManager.Instance != null) ? CharacterManager.Instance.SelectedCharacter : null;

        // Nếu chuyển đổi trạng thái ẩn/hiện UI, tự động xóa highlight/lựa chọn hiện tại
        if (m_WasUiHidden != isUiHidden)
        {
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.DeselectCharacter();
            }
            currentSelected = null;
        }
        m_WasUiHidden = isUiHidden;

        GameObject highlightedCast = null;
        if (isUiHidden && currentSelected != null && currentSelected.GetComponent<CastAudioData>() != null)
        {
            highlightedCast = currentSelected;
        }

        // Kiểm tra thay đổi trạng thái Highlight
        if (highlightedCast != m_LastHighlightedCast)
        {
            m_LastHighlightedCast = highlightedCast;
            ApplyHighlightVisuals(highlightedCast);
        }

        if (highlightedCast != null)
        {
            // ── CÓ CAST ĐƯỢC HIGHLIGHT (SOLO) ──
            
            // Dừng các Cast chưa đặt
            foreach (var cast in m_UnplacedCasts)
            {
                if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                    cast.preparedSource.Pause();
            }

            float refTime = -1f;
            if (!isCustomMode && placedCount >= FULL_BAND_COUNT)
            {
                // Ở chế độ Band và đủ 4 Cast: nhạc tổng FullSong vẫn chạy nhưng âm lượng bằng 0
                if (m_FullSongSource != null)
                {
                    if (m_FullSongClip != null && !m_FullSongSource.isPlaying)
                    {
                        m_FullSongSource.clip = m_FullSongClip;
                        m_FullSongSource.Play();
                    }
                    m_FullSongSource.volume = 0f; // Mute bài hát tổng
                    m_FullSongPlayingState = true;
                    refTime = m_FullSongSource.time;
                }

                if (!m_WasFullBandActive)
                {
                    NotifyAllModelLoopEffects(true);
                    m_WasFullBandActive = true;
                }
            }
            else
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

                refTime = GetCurrentReferenceTime();
            }

            // Phát các Cast đã đặt: Cast được highlight phát tối đa, các Cast khác âm lượng 0
            foreach (var cast in m_PlacedCasts)
            {
                if (cast.preparedSource == null) continue;

                if (!cast.preparedSource.isPlaying)
                {
                    cast.preparedSource.Play();
                    if (refTime >= 0f && cast.preparedSource.clip != null && cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && refTime < cast.preparedSource.clip.length)
                    {
                        cast.preparedSource.time = refTime;
                    }
                }
                else if (refTime >= 0f && cast.preparedSource.clip != null && cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && refTime < cast.preparedSource.clip.length && Mathf.Abs(cast.preparedSource.time - refTime) > 0.15f)
                {
                    cast.preparedSource.time = refTime;
                }

                cast.preparedSource.mute = false;

                if (cast.gameObject == highlightedCast)
                {
                    cast.preparedSource.volume = 1f; // Âm lượng tối đa cho Cast được highlight
                }
                else
                {
                    cast.preparedSource.volume = 0f; // Tắt tiếng các Cast khác (nhưng vẫn phát để đồng bộ)
                }
            }
        }
        else
        {
            // ── KHÔNG CÓ HIGHLIGHT (TRẠNG THÁI BÌNH THƯỜNG) ──
            if (placedCount == 0)
            {
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
            else if (!isCustomMode && placedCount >= FULL_BAND_COUNT)
            {
                // ── Chế độ Band & Đủ 4 Cast: Phát FullSong ──

                foreach (var cast in m_RegisteredCasts)
                {
                    if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                    {
                        cast.preparedSource.Stop();
                        Debug.Log($"[BandAudioManager] Dừng nhạc cụ riêng lẻ: {cast.gameObject.name}");
                    }
                }

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
                    }
                    m_FullSongSource.volume = 1f; // Khôi phục âm lượng nhạc tổng
                }

                if (!m_WasFullBandActive)
                {
                    NotifyAllModelLoopEffects(true);
                    m_WasFullBandActive = true;
                }
            }
            else
            {
                // ── Chế độ Custom hoặc 1-3 Cast ở chế độ Band: Phát nhạc cụ riêng lẻ ──
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

                foreach (var cast in m_UnplacedCasts)
                {
                    if (cast.preparedSource != null && cast.preparedSource.isPlaying)
                        cast.preparedSource.Pause();
                }

                float referenceTime = GetCurrentReferenceTime();

                foreach (var cast in m_PlacedCasts)
                {
                    if (cast.preparedSource == null) continue;

                    if (!cast.preparedSource.isPlaying)
                    {
                        cast.preparedSource.Play();
                        if (referenceTime >= 0f && cast.preparedSource.clip != null && cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && referenceTime < cast.preparedSource.clip.length)
                        {
                            cast.preparedSource.time = referenceTime;
                        }
                    }
                    else if (referenceTime >= 0f && cast.preparedSource.clip != null && cast.preparedSource.clip.loadState == AudioDataLoadState.Loaded && referenceTime < cast.preparedSource.clip.length && Mathf.Abs(cast.preparedSource.time - referenceTime) > 0.15f)
                    {
                        cast.preparedSource.time = referenceTime;
                    }

                    cast.preparedSource.mute = false;

                    if (isCustomMode)
                    {
                        // Ở chế độ Custom, phát bình thường ở âm lượng gốc của Cast
                        cast.preparedSource.volume = cast.originalVolume;
                    }
                    else
                    {
                        // Ở chế độ Band, điều phối âm lượng khi chơi chung
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
        }
    }

    private void CleanupDestroyedCasts()
    {
        for (int i = m_RegisteredCasts.Count - 1; i >= 0; i--)
        {
            CastAudioData cast = m_RegisteredCasts[i];
            if (cast != null) continue;

            m_RegisteredCasts.RemoveAt(i);
            if (!object.ReferenceEquals(cast, null))
            {
                m_PlacementStateCache.Remove(cast);
                m_MoveCache.Remove(cast);
            }
        }
    }

    private bool IsCustomMode()
    {
        BandARSpawner spawner = GetSpawner();
        return spawner != null && spawner.spawnerMode == BandARSpawner.SpawnerMode.Custom;
    }

    private BandARSpawner GetSpawner()
    {
        if (m_CachedSpawner == null)
        {
            m_CachedSpawner = FindFirstObjectByType<BandARSpawner>();
        }
        return m_CachedSpawner;
    }

    private BandPanelController GetBandPanel()
    {
        if (m_CachedBandPanel == null)
        {
            m_CachedBandPanel = FindFirstObjectByType<BandPanelController>();
        }
        return m_CachedBandPanel;
    }

    private CustomCharacterPanelController GetCustomPanel()
    {
        if (m_CachedCustomPanel == null)
        {
            m_CachedCustomPanel = FindFirstObjectByType<CustomCharacterPanelController>();
        }
        return m_CachedCustomPanel;
    }

    private Move GetCachedMove(CastAudioData cast)
    {
        if (cast == null)
        {
            return null;
        }

        if (!m_MoveCache.TryGetValue(cast, out Move move) || move == null)
        {
            move = cast.GetComponent<Move>();
            m_MoveCache[cast] = move;
        }

        return move;
    }

    private Renderer[] GetCachedRenderers(GameObject character)
    {
        if (character == null)
        {
            return System.Array.Empty<Renderer>();
        }

        if (!m_RendererCache.TryGetValue(character, out Renderer[] renderers) || renderers == null)
        {
            renderers = character.GetComponentsInChildren<Renderer>(true);
            m_RendererCache[character] = renderers;
        }

        return renderers;
    }

    private bool IsAnyUiHidden()
    {
        BandPanelController bandPanel = GetBandPanel();
        if (bandPanel != null && bandPanel.IsUiHidden) return true;

        CustomCharacterPanelController customPanel = GetCustomPanel();
        if (customPanel != null && customPanel.IsUiHidden) return true;

        return false;
    }

    private void ApplyHighlightVisuals(GameObject highlightedCast)
    {
        foreach (var cast in m_RegisteredCasts)
        {
            if (cast == null) continue;

            Move move = GetCachedMove(cast);
            if (move == null) continue;

            if (highlightedCast == null)
            {
                // Sáng bình thường và nhảy trở lại
                SetCharacterDimmed(cast.gameObject, false);
                move.PlayDance();
            }
            else if (cast.gameObject == highlightedCast)
            {
                // Cast được highlight: sáng bình thường, nhảy trở lại
                SetCharacterDimmed(cast.gameObject, false);
                move.PlayDance();
            }
            else
            {
                // Các Cast khác: làm tối đi và đưa về hoạt ảnh Idle
                SetCharacterDimmed(cast.gameObject, true);
                move.PlayIdle();
            }
        }
    }

    private void SetCharacterDimmed(GameObject character, bool dimmed)
    {
        if (character == null) return;

        Renderer[] renderers = GetCachedRenderers(character);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            if (m_PropertyBlock == null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }

            m_PropertyBlock.Clear();
            renderer.GetPropertyBlock(m_PropertyBlock);

            Color color = dimmed ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
            m_PropertyBlock.SetColor("_Color", color);
            m_PropertyBlock.SetColor("_BaseColor", color);

            renderer.SetPropertyBlock(m_PropertyBlock);
        }
    }


    /// <summary>
    /// Reset toàn bộ âm thanh (FullSong và các Cast riêng lẻ) cùng với hoạt ảnh nhảy về 0
    /// để bắt đầu chạy đồng bộ cùng nhau.
    /// </summary>
    public void ResetAllAudioAndAnimations()
    {
        Debug.Log("[BandAudioManager] Bắt đầu Reset toàn bộ âm thanh và hoạt ảnh về 0.");

        // 1. Reset FullSong AudioSource
        if (m_FullSongSource != null)
        {
            m_FullSongSource.time = 0f;
            if (m_FullSongPlayingState && !m_FullSongSource.isPlaying)
            {
                m_FullSongSource.Play();
            }
        }

        // 2. Reset tất cả AudioSource của từng Cast đã đăng ký
        foreach (var cast in m_RegisteredCasts)
        {
            if (cast == null || cast.preparedSource == null) continue;

            cast.preparedSource.time = 0f;
            
            // Nếu Cast đã được đặt và đang phát thì đảm bảo nó vẫn phát
            if (IsCastPlaced(cast) && !cast.preparedSource.isPlaying)
            {
                cast.preparedSource.Play();
            }
        }

        // 3. Reset MusicSyncManager để đồng bộ lại beat timer toàn cục
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.ResetPlayback();
        }

        // 4. Reset animation cho các Cast đã đăng ký, tránh quét toàn scene.
        foreach (var cast in m_RegisteredCasts)
        {
            if (cast == null || !IsCastPlaced(cast)) continue;

            Move move = GetCachedMove(cast);
            if (move != null)
            {
                move.ResetLocalDanceState();
            }
        }

        Debug.Log("[BandAudioManager] Hoàn tất Reset toàn bộ âm thanh và hoạt ảnh.");
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

    private bool IsCastPlaced(CastAudioData cast)
    {
        if (cast == null) return false;

        if (!m_PlacementStateCache.TryGetValue(cast, out CastPlacementState placementState))
        {
            placementState = cast.GetComponent<CastPlacementState>();
            m_PlacementStateCache[cast] = placementState;
        }

        if (placementState != null)
        {
            return placementState.IsPlaced;
        }

        return cast.transform.parent == null;
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }
}
