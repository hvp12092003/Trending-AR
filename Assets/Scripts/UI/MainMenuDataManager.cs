using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quản lý việc đọc/ghi dữ liệu cục bộ cho màn hình Menu chính – phiên bản Offline.
/// Tất cả dữ liệu được lưu bằng PlayerPrefs + JsonUtility.
/// Data classes được định nghĩa tập trung trong Assets/Scripts/Data/.
/// </summary>
public class MainMenuDataManager : MonoBehaviour
{
    public static MainMenuDataManager Instance { get; private set; }

    [Header("Tap Audio")]
    [Tooltip("AudioSource phat am khi VFX dang bat va nguoi dung cham/click man hinh. Neu de trong se tu lay AudioSource tren GameObject nay.")]
    [SerializeField] private AudioSource tapAudioSource;

    [Header("Prefab Catalog")]
    [Tooltip("Addressables-ready catalog. Legacy lists below are kept as fallback during migration.")]
    [SerializeField] private MainMenuPrefabCatalog prefabCatalog;

    [Header("Character Catalog (Deprecated)")]
    private readonly List<GameObject> characterPrefabs = new List<GameObject>();

    private readonly List<GameObject> _resolvedCharacterPrefabs = new List<GameObject>();

    public MainMenuPrefabCatalog PrefabCatalog => prefabCatalog;
    public List<GameObject> CharacterPrefabs => GetResolvedCharacterPrefabs();
    public IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> CharacterEntries =>
        prefabCatalog != null
            ? prefabCatalog.GetCharacterEntries(null)
            : MainMenuPrefabCatalog.CreateRuntimeEntries(null);

    [Header("Instrument Catalog (Deprecated)")]
    private readonly List<GameObject> instrumentPrefabs = new List<GameObject>();

    private readonly List<GameObject> _resolvedInstrumentPrefabs = new List<GameObject>();

    public List<GameObject> InstrumentPrefabs => GetResolvedInstrumentPrefabs();
    public IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> InstrumentEntries =>
        prefabCatalog != null
            ? prefabCatalog.GetInstrumentEntries(null)
            : MainMenuPrefabCatalog.CreateRuntimeEntries(null);

    /// <summary>
    /// Lưu trữ cấu hình ban nhạc được chọn hiện hành từ Main Menu để truyền sang Scene AR.
    /// </summary>
    [HideInInspector]
    public EditorBandData selectedBandData;

    private const string LAST_SELECTED_CAST_KEY    = "LastSelectedCastJSON";
    private const string SAVED_CASTS_PREFS_KEY      = "SavedCastsDataJSON";
    private const string SAVED_RECORDINGS_PREFS_KEY = "SavedRecordingsDataJSON";
    private const string SAVED_BANDS_PREFS_KEY      = "SavedBandsDataJSON";
    private const string OFFLINE_POINTS_PREFS_KEY   = "OfflineUserPoints";
    private const string CUSTOM_CAST_USE_AWARD_PREFIX = "CustomCastUseAwarded_";

    private CastData _castData;

    /// <summary>
    /// Lưu trữ CastData của nhân vật tự thiết kế (Custom Character) được chọn để chuyển tiếp sang scene AR.
    /// </summary>
    public CastData castData
    {
        get
        {
            if (_castData == null)
            {
                string json = PlayerPrefs.GetString(LAST_SELECTED_CAST_KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try { _castData = JsonUtility.FromJson<CastData>(json); }
                    catch (Exception ex) { Debug.LogError("[MainMenuDataManager] Lỗi load LastSelectedCastJSON: " + ex.Message); }
                }
            }
            return _castData;
        }
        set
        {
            _castData = value;
            if (_castData != null)
            {
                try
                {
                    PlayerPrefs.SetString(LAST_SELECTED_CAST_KEY, JsonUtility.ToJson(_castData));
                    PlayerPrefs.Save();
                }
                catch (Exception ex) { Debug.LogError("[MainMenuDataManager] Lỗi save LastSelectedCastJSON: " + ex.Message); }
            }
            else
            {
                PlayerPrefs.DeleteKey(LAST_SELECTED_CAST_KEY);
                PlayerPrefs.Save();
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (tapAudioSource == null)
            {
                tapAudioSource = GetComponent<AudioSource>();
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            AddressablePrefabLoader.ReleaseAll();
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsTapAudioEnabled())
        {
            return;
        }

        if (Touchscreen.current != null)
        {
            bool playedTouchAudio = false;
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.wasPressedThisFrame)
                {
                    PlayTapAudio();
                    playedTouchAudio = true;
                }
            }

            if (playedTouchAudio)
            {
                return;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PlayTapAudio();
        }
    }

    private bool IsTapAudioEnabled()
    {
        return PlayerPrefs.GetInt(PopupSetting.VFX_KEY, 1) == 1 &&
               tapAudioSource != null &&
               tapAudioSource.clip != null;
    }

    private void PlayTapAudio()
    {
        tapAudioSource.PlayOneShot(tapAudioSource.clip);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Đọc CastPrefab từ danh sách Prefab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Truyền vào danh sách các Cast (Prefab) và đọc ra danh sách Component CastPrefab của từng prefab.
    /// </summary>
    private List<GameObject> GetResolvedCharacterPrefabs()
    {
        _resolvedCharacterPrefabs.Clear();

        if (prefabCatalog != null)
        {
            AddUniquePrefabs(_resolvedCharacterPrefabs, prefabCatalog.GetDirectCharacterPrefabs(null));
        }

        return _resolvedCharacterPrefabs;
    }

    private List<GameObject> GetResolvedInstrumentPrefabs()
    {
        _resolvedInstrumentPrefabs.Clear();

        if (prefabCatalog != null)
        {
            AddUniquePrefabs(_resolvedInstrumentPrefabs, prefabCatalog.GetDirectInstrumentPrefabs(null));
        }

        return _resolvedInstrumentPrefabs;
    }

    private static void AddUniquePrefabs(List<GameObject> output, IEnumerable<GameObject> prefabs)
    {
        if (output == null || prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && !output.Contains(prefab))
            {
                output.Add(prefab);
            }
        }
    }

    public List<CastPrefab> GetCastPrefabList(List<GameObject> prefabs)
    {
        List<CastPrefab> castPrefabList = new List<CastPrefab>();
        if (prefabs == null) return castPrefabList;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            CastPrefab castPrefab = prefab.GetComponent<CastPrefab>();
            if (castPrefab != null)
                castPrefabList.Add(castPrefab);
        }
        return castPrefabList;
    }

    /// <summary>
    /// Truyền vào danh sách các Cast (Prefab) và đọc ra danh sách CastData tương ứng.
    /// </summary>
    public List<CastData> GetCastDataList(List<GameObject> prefabs)
    {
        List<CastData> castDataList = new List<CastData>();
        if (prefabs == null) return castDataList;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            CastData data = CreateCastDataFromPrefab(prefab);
            if (data != null)
                castDataList.Add(data);
        }
        return castDataList;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quản lý Prefab Nhân vật
    // ─────────────────────────────────────────────────────────────────────────

    public void SetCharacterPrefabs(IEnumerable<GameObject> prefabs)
    {
        characterPrefabs.Clear();
        AddCharacterPrefabs(prefabs);
    }

    public void AddCharacterPrefabs(IEnumerable<GameObject> prefabs)
    {
        if (prefabs == null) return;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && !characterPrefabs.Contains(prefab))
                characterPrefabs.Add(prefab);
        }
    }

    public GameObject GetCharacterPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        if (prefabCatalog != null)
        {
            GameObject catalogPrefab = prefabCatalog.GetCharacterPrefab(prefabName, null);
            if (catalogPrefab != null)
            {
                return catalogPrefab;
            }
        }

        List<GameObject> resolvedPrefabs = CharacterPrefabs;
        if (resolvedPrefabs != null)
        {
            foreach (GameObject prefab in resolvedPrefabs)
            {
                if (prefab != null && prefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                    return prefab;
            }
        }

        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/Cast/" + prefabName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(prefabName);
        return resPrefab;
    }

    public async Task<GameObject> LoadCharacterPrefabAsync(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        if (prefabCatalog != null)
        {
            GameObject prefab = await prefabCatalog.LoadCharacterPrefabAsync(prefabName, null);
            if (prefab != null)
            {
                return prefab;
            }
        }

        return GetCharacterPrefab(prefabName);
    }

    public async Task<GameObject> LoadInstrumentPrefabAsync(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return null;

        if (prefabCatalog != null)
        {
            GameObject prefab = await prefabCatalog.LoadInstrumentPrefabAsync(instrumentId, null);
            if (prefab != null)
            {
                return prefab;
            }
        }

        return GetInstrumentPrefab(instrumentId);
    }

    public GameObject GetInstrumentPrefab(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return null;

        if (prefabCatalog != null)
        {
            GameObject catalogPrefab = prefabCatalog.GetInstrumentPrefab(instrumentId, null);
            if (catalogPrefab != null)
            {
                return catalogPrefab;
            }
        }

        List<GameObject> resolvedPrefabs = InstrumentPrefabs;
        if (resolvedPrefabs != null)
        {
            foreach (GameObject prefab in resolvedPrefabs)
            {
                if (prefab == null) continue;

                if (prefab.name.Equals(instrumentId, StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }

                AudioConfig config = prefab.GetComponent<AudioConfig>();
                if (config != null &&
                    !string.IsNullOrEmpty(config.Name) &&
                    config.Name.Equals(instrumentId, StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/Intrument/" + instrumentId);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>("Prefabs/" + instrumentId);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(instrumentId);
        return resPrefab;
    }

    public Sprite GetCharacterAvatarSprite(string prefabName)
    {
        MainMenuPrefabCatalog.PrefabAssetEntry entry =
            prefabCatalog != null ? prefabCatalog.GetCharacterEntry(prefabName, null) : null;
        if (entry != null && entry.Avatar != null)
        {
            return entry.Avatar;
        }

        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab == null) return null;
        CastPrefab config = prefab.GetComponent<CastPrefab>();
        return config != null ? config.characterAvatar : null;
    }

    /// <summary>
    /// Lấy Sprite ảnh đại diện của nhạc cụ dựa vào ID (Name hoặc tên prefab).
    /// </summary>
    public Sprite GetInstrumentAvatarSprite(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return null;

        // Fallback cho bản ghi âm tự thu
        if (instrumentId.StartsWith("rec_"))
        {
            Sprite micSprite = Resources.Load<Sprite>("Avatars/Mic");
            if (micSprite == null) micSprite = Resources.Load<Sprite>("Mic");
            return micSprite;
        }

        MainMenuPrefabCatalog.PrefabAssetEntry entry =
            prefabCatalog != null ? prefabCatalog.GetInstrumentEntry(instrumentId, null) : null;
        if (entry != null && entry.Avatar != null)
        {
            return entry.Avatar;
        }

        List<GameObject> resolvedPrefabs = InstrumentPrefabs;
        if (resolvedPrefabs != null)
        {
            foreach (GameObject prefab in resolvedPrefabs)
            {
                if (prefab == null) continue;
                AudioConfig config = prefab.GetComponent<AudioConfig>();
                if (config != null)
                {
                    string name = !string.IsNullOrEmpty(config.Name) ? config.Name : prefab.name;
                    if (name.Equals(instrumentId, StringComparison.OrdinalIgnoreCase))
                    {
                        return config.avatar;
                    }
                }
                else
                {
                    if (prefab.name.Equals(instrumentId, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
            }
        }
        return null;
    }

    public CastData CreateCastDataFromPrefab(GameObject prefab, string audioId = "", string selectedDanceAnimId = "")
    {
        if (prefab == null) return null;

        CastPrefab config = prefab.GetComponent<CastPrefab>();
        string displayName = (config != null && !string.IsNullOrEmpty(config.Name)) ? config.Name : prefab.name;
        List<string> danceAnimIds = GetDanceAnimationIds(config);
        string danceAnimId = !string.IsNullOrEmpty(selectedDanceAnimId)
            ? selectedDanceAnimId
            : (danceAnimIds.Count > 0 ? danceAnimIds[0] : "");

        if (!string.IsNullOrEmpty(danceAnimId) && !danceAnimIds.Contains(danceAnimId))
            danceAnimIds.Add(danceAnimId);

        return new CastData(displayName, prefab.name, audioId ?? "", danceAnimId, danceAnimIds);
    }

    public CastData CreateCastDataFromPrefabName(string prefabName, string audioId = "", string selectedDanceAnimId = "")
    {
        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab != null)
            return CreateCastDataFromPrefab(prefab, audioId, selectedDanceAnimId);

        MainMenuPrefabCatalog.PrefabAssetEntry entry =
            prefabCatalog != null ? prefabCatalog.GetCharacterEntry(prefabName, null) : null;

        List<string> danceAnimIds = new List<string>();
        if (entry != null && entry.AnimationIds != null)
        {
            foreach (string animationId in entry.AnimationIds)
            {
                if (!string.IsNullOrEmpty(animationId) && !danceAnimIds.Contains(animationId))
                {
                    danceAnimIds.Add(animationId);
                }
            }
        }

        if (!string.IsNullOrEmpty(selectedDanceAnimId) && !danceAnimIds.Contains(selectedDanceAnimId))
            danceAnimIds.Add(selectedDanceAnimId);

        string danceAnimId = !string.IsNullOrEmpty(selectedDanceAnimId)
            ? selectedDanceAnimId
            : (entry != null ? entry.DefaultAnimationId : "");
        string displayName = entry != null ? entry.DisplayName : (string.IsNullOrEmpty(prefabName) ? "Custom Character" : prefabName);
        string stablePrefabName = entry != null ? entry.Id : prefabName;
        return new CastData(displayName, stablePrefabName, audioId ?? "", danceAnimId, danceAnimIds);
    }

    private List<string> GetDanceAnimationIds(CastPrefab config)
    {
        List<string> danceAnimIds = new List<string>();
        if (config == null || config.animations == null) return danceAnimIds;

        foreach (CastAnimation anim in config.animations)
        {
            if (anim.animation == null || string.IsNullOrEmpty(anim.animation.name)) continue;
            if (!danceAnimIds.Contains(anim.animation.name))
                danceAnimIds.Add(anim.animation.name);
        }
        return danceAnimIds;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quản lý dữ liệu Nhân Vật (Characters) – Local Storage
    // ─────────────────────────────────────────────────────────────────────────

    private List<SerializableCharacterData> LoadSavedCastsFromPrefs()
    {
        string json = PlayerPrefs.GetString(SAVED_CASTS_PREFS_KEY, "");
        if (string.IsNullOrEmpty(json)) return new List<SerializableCharacterData>();

        try
        {
            SerializableCharacterDataList wrapper = JsonUtility.FromJson<SerializableCharacterDataList>(json);
            return wrapper?.characters ?? new List<SerializableCharacterData>();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi load JSON casts: " + ex.Message);
            return new List<SerializableCharacterData>();
        }
    }

    private void SaveCastsToPrefs(List<SerializableCharacterData> list)
    {
        try
        {
            string json = JsonUtility.ToJson(new SerializableCharacterDataList { characters = list });
            PlayerPrefs.SetString(SAVED_CASTS_PREFS_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi save JSON casts: " + ex.Message);
        }
    }

    public int GetSavedCastsCount()
    {
        List<SerializableCharacterData> list = LoadSavedCastsFromPrefs();
        return list != null ? list.Count : 0;
    }

    public int GetUnlockedCastSlotCount()

    {

        return CastSlotUnlockManager.GetUnlockedSlotCount(GetSavedCastsCount());

    }



    public int GetMaxCastSlotCount()

    {

        return CastSlotUnlockManager.MaxSlotCount;

    }



    public int GetNextUnlockableCastSlotNumber()

    {

        return CastSlotUnlockManager.GetNextUnlockableSlotNumber(GetSavedCastsCount());

    }



    public bool HasFreeCastSlot()

    {

        return GetSavedCastsCount() < GetUnlockedCastSlotCount();

    }



    public bool CanUnlockMoreCastSlots()

    {

        return CastSlotUnlockManager.CanUnlockMore(GetSavedCastsCount());

    }



    public bool TryUnlockNextCastSlot()

    {

        bool unlocked = CastSlotUnlockManager.TryUnlockNextSlot(GetSavedCastsCount());

        if (unlocked)

        {

            Debug.Log($"[MainMenuDataManager] Unlocked Cast slot {GetUnlockedCastSlotCount()} / {GetMaxCastSlotCount()}.");

        }

        return unlocked;

    }



    public Task<List<CharacterData>> GetCreatedCharactersAsync()
    {
        List<CharacterData> result = new List<CharacterData>();
        foreach (var saved in LoadSavedCastsFromPrefs())
        {
            result.Add(new CharacterData
            {
                characterId      = saved.characterId,
                name             = saved.name,
                prefabName       = saved.prefabName,
                instrumentId     = saved.instrumentId,
                danceAnimId      = saved.danceAnimId,
                danceAnimIds     = saved.danceAnimIds != null ? new List<string>(saved.danceAnimIds) : new List<string>(),
                createdAtSeconds = saved.createdAtSeconds,
                usePrefabAvatar  = saved.usePrefabAvatar
            });
        }
        return Task.FromResult(result);
    }

    public Task<bool> CreateCharacterAsync(string name, string prefabName, string instrumentId = "", string danceAnimId = "", List<string> danceAnimIds = null)
    {
        List<SerializableCharacterData> list = LoadSavedCastsFromPrefs();
        int slotLimit = GetUnlockedCastSlotCount();

        if (list.Count >= slotLimit)
        {
            Debug.LogWarning($"[MainMenuDataManager] Reached unlocked Cast slot limit: {slotLimit}/{GetMaxCastSlotCount()}.");
            return Task.FromResult(false);
        }

        bool usePrefab = Resources.Load<Sprite>("Avatars/" + prefabName) == null;
        string characterId = "char_" + DateTime.UtcNow.Ticks;
        list.Add(new SerializableCharacterData
        {
            characterId      = characterId,
            name             = name,
            prefabName       = prefabName,
            instrumentId     = instrumentId,
            danceAnimId      = danceAnimId,
            danceAnimIds     = danceAnimIds != null ? new List<string>(danceAnimIds) : new List<string>(),
            createdAtSeconds = DateTime.UtcNow.Ticks / 10000000,
            usePrefabAvatar  = usePrefab
        });
        SaveCastsToPrefs(list);
        return Task.FromResult(true);
    }

    public Task<bool> CreateCharacterAsync(CastData cast)
    {
        if (cast == null) return Task.FromResult(false);
        List<SerializableCharacterData> list = LoadSavedCastsFromPrefs();
        int slotLimit = GetUnlockedCastSlotCount();

        if (list.Count >= slotLimit)
        {
            Debug.LogWarning($"[MainMenuDataManager] Reached unlocked Cast slot limit: {slotLimit}/{GetMaxCastSlotCount()}.");
            return Task.FromResult(false);
        }

        bool usePrefab = Resources.Load<Sprite>("Avatars/" + cast.prefabName) == null;
        string characterId = "char_" + DateTime.UtcNow.Ticks;
        cast.characterId = characterId;
        list.Add(new SerializableCharacterData
        {
            characterId      = characterId,
            name             = cast.name,
            prefabName       = cast.prefabName,
            instrumentId     = cast.audioId,
            danceAnimId      = cast.danceAnimId,
            danceAnimIds     = cast.danceAnimIds != null ? new List<string>(cast.danceAnimIds) : new List<string>(),
            createdAtSeconds = DateTime.UtcNow.Ticks / 10000000,
            usePrefabAvatar  = usePrefab
        });
        SaveCastsToPrefs(list);
        return Task.FromResult(true);
    }

    public Task<bool> CreateCharacterAsync(GameObject characterPrefab, string instrumentId = "", string danceAnimId = "", string customName = "")
    {
        if (characterPrefab == null) return Task.FromResult(false);

        CastPrefab config = characterPrefab.GetComponent<CastPrefab>();
        string displayName = !string.IsNullOrEmpty(customName) ? customName
            : ((config != null && !string.IsNullOrEmpty(config.Name)) ? config.Name : characterPrefab.name);

        List<string> danceAnimIds = GetDanceAnimationIds(config);
        if (!string.IsNullOrEmpty(danceAnimId) && !danceAnimIds.Contains(danceAnimId))
            danceAnimIds.Add(danceAnimId);

        return CreateCharacterAsync(displayName, characterPrefab.name, instrumentId, danceAnimId, danceAnimIds);
    }

    public Task<bool> DeleteCharacterAsync(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return Task.FromResult(false);
        List<SerializableCharacterData> list = LoadSavedCastsFromPrefs();
        int index = list.FindIndex(c => c.characterId == characterId);
        if (index >= 0)
        {
            list.RemoveAt(index);
            SaveCastsToPrefs(list);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quản lý dữ liệu Ban Nhạc (Bands) – Local Storage
    // ─────────────────────────────────────────────────────────────────────────

    private List<SerializableBandData> LoadSavedBandsFromPrefs()
    {
        string json = PlayerPrefs.GetString(SAVED_BANDS_PREFS_KEY, "");
        if (string.IsNullOrEmpty(json)) return new List<SerializableBandData>();

        try
        {
            SerializableBandDataList wrapper = JsonUtility.FromJson<SerializableBandDataList>(json);
            List<SerializableBandData> bands = wrapper?.bands ?? new List<SerializableBandData>();

            // Loại bỏ ban nhạc mặc định nếu tồn tại
            bool hasDefault = false;
            for (int i = bands.Count - 1; i >= 0; i--)
            {
                if (bands[i].name == "Ban nhạc mặc định" || bands[i].name == "Default Band" || bands[i].bandId == "default_band")
                {
                    bands.RemoveAt(i);
                    hasDefault = true;
                }
            }
            if (hasDefault)
            {
                Debug.Log("[MainMenuDataManager] Đã loại bỏ Ban nhạc mặc định khỏi dữ liệu cục bộ.");
                SaveBandsToPrefs(bands);
            }
            return bands;
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi load JSON bands: " + ex.Message);
            return new List<SerializableBandData>();
        }
    }

    private void SaveBandsToPrefs(List<SerializableBandData> list)
    {
        try
        {
            string json = JsonUtility.ToJson(new SerializableBandDataList { bands = list });
            PlayerPrefs.SetString(SAVED_BANDS_PREFS_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi save JSON bands: " + ex.Message);
        }
    }

    public Task<List<BandData>> GetCreatedBandsAsync()
    {
        List<BandData> result = new List<BandData>();
        foreach (var saved in LoadSavedBandsFromPrefs())
        {
            List<CastData> casts = new List<CastData>();
            foreach (var c in saved.casts)
                casts.Add(new CastData(c.name, c.prefabName, c.instrumentId, c.danceAnimId, c.danceAnimIds));

            BandData band = new BandData(casts)
            {
                bandId = saved.bandId,
                name   = saved.name
            };
            result.Add(band);
        }
        return Task.FromResult(result);
    }

    public Task<bool> CreateBandAsync(string name, List<CastData> casts)
    {
        List<SerializableBandData> list = LoadSavedBandsFromPrefs();

        List<SerializableCharacterData> serializableCasts = new List<SerializableCharacterData>();
        if (casts != null)
        {
            foreach (var cast in casts)
            {
                serializableCasts.Add(new SerializableCharacterData
                {
                    characterId      = "char_" + DateTime.UtcNow.Ticks + "_" + UnityEngine.Random.Range(0, 1000),
                    name             = cast.name,
                    prefabName       = cast.prefabName,
                    instrumentId     = cast.audioId,
                    danceAnimId      = cast.danceAnimId,
                    danceAnimIds     = cast.danceAnimIds != null ? new List<string>(cast.danceAnimIds) : new List<string>(),
                    createdAtSeconds = DateTime.UtcNow.Ticks / 10000000,
                    usePrefabAvatar  = true
                });
            }
        }

        list.Add(new SerializableBandData
        {
            bandId           = "band_" + DateTime.UtcNow.Ticks,
            name             = name,
            casts            = serializableCasts,
            createdAtSeconds = DateTime.UtcNow.Ticks / 10000000
        });
        SaveBandsToPrefs(list);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteBandAsync(string bandId)
    {
        if (string.IsNullOrEmpty(bandId)) return Task.FromResult(false);
        List<SerializableBandData> list = LoadSavedBandsFromPrefs();
        int index = list.FindIndex(b => b.bandId == bandId);
        if (index >= 0)
        {
            list.RemoveAt(index);
            SaveBandsToPrefs(list);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quản lý Ghi Âm (Recordings) – Local Storage
    // ─────────────────────────────────────────────────────────────────────────

    private List<SerializableRecordingData> LoadSavedRecordingsFromPrefs()
    {
        string json = PlayerPrefs.GetString(SAVED_RECORDINGS_PREFS_KEY, "");
        if (string.IsNullOrEmpty(json)) return new List<SerializableRecordingData>();

        try
        {
            SerializableRecordingDataList wrapper = JsonUtility.FromJson<SerializableRecordingDataList>(json);
            return wrapper?.recordings ?? new List<SerializableRecordingData>();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi load JSON recordings: " + ex.Message);
            return new List<SerializableRecordingData>();
        }
    }

    private void SaveRecordingsToPrefs(List<SerializableRecordingData> list)
    {
        try
        {
            string json = JsonUtility.ToJson(new SerializableRecordingDataList { recordings = list });
            PlayerPrefs.SetString(SAVED_RECORDINGS_PREFS_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] Lỗi save JSON recordings: " + ex.Message);
        }
    }

    public Task<bool> SaveRecordingAsync(string recordingId, string name, string audioBase64)
    {
        try
        {
            List<SerializableRecordingData> list = LoadSavedRecordingsFromPrefs();
            list.Add(new SerializableRecordingData
            {
                recordingId      = recordingId,
                name             = name,
                audioBase64      = audioBase64,
                createdAtSeconds = DateTime.UtcNow.Ticks / 10000000
            });
            SaveRecordingsToPrefs(list);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuDataManager] SaveRecordingAsync failed: " + ex.Message);
            return Task.FromResult(false);
        }
    }

    public Task<List<RecordingData>> GetRecordingsAsync()
    {
        List<RecordingData> result = new List<RecordingData>();
        foreach (var saved in LoadSavedRecordingsFromPrefs())
        {
            result.Add(new RecordingData
            {
                recordingId      = saved.recordingId,
                name             = saved.name,
                audioBase64      = saved.audioBase64,
                createdAtSeconds = saved.createdAtSeconds
            });
        }
        return Task.FromResult(result);
    }

    public Task<bool> DeleteRecordingAsync(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId)) return Task.FromResult(false);
        List<SerializableRecordingData> list = LoadSavedRecordingsFromPrefs();
        int index = list.FindIndex(r => r.recordingId == recordingId);
        if (index >= 0)
        {
            list.RemoveAt(index);
            SaveRecordingsToPrefs(list);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Leaderboard – Offline Data
    // ─────────────────────────────────────────────────────────────────────────

    public Task<List<UserData>> GetLeaderboardAsync()
    {
        List<UserData> list = new List<UserData>();
        string currentUserName = PlayerPrefs.GetString("OfflineUserName", "");
        if (string.IsNullOrEmpty(currentUserName) || currentUserName == "Offline User")
            currentUserName = "Offline Player";

        int currentUserPoints = PlayerPrefs.GetInt(OFFLINE_POINTS_PREFS_KEY, 0);

        list.Add(new UserData { userId = "offline_user_id", displayName = currentUserName, points = currentUserPoints });
        list.Add(new UserData { userId = "bot_1", displayName = "Sunny Beats",  points = 950 });
        list.Add(new UserData { userId = "bot_2", displayName = "Neon Dancer",  points = 720 });
        list.Add(new UserData { userId = "bot_3", displayName = "AR Groover",   points = 510 });
        list.Sort((a, b) => b.points.CompareTo(a.points));

        return Task.FromResult(list);
    }

    public int AddOfflineUserPoints(int pointsToAdd)

    {

        if (pointsToAdd <= 0)

            return PlayerPrefs.GetInt(OFFLINE_POINTS_PREFS_KEY, 0);



        int totalPoints = PlayerPrefs.GetInt(OFFLINE_POINTS_PREFS_KEY, 0) + pointsToAdd;

        PlayerPrefs.SetInt(OFFLINE_POINTS_PREFS_KEY, totalPoints);

        PlayerPrefs.Save();

        return totalPoints;

    }

    public bool TryAwardCustomCastUsePoints(CastData cast, int pointsToAdd, out int totalPoints)

    {

        totalPoints = PlayerPrefs.GetInt(OFFLINE_POINTS_PREFS_KEY, 0);

        if (cast == null || pointsToAdd <= 0)

            return false;



        if (!IsSavedCustomCast(cast))

            return false;



        string awardKey = GetCustomCastUseAwardKey(cast);

        if (PlayerPrefs.GetInt(awardKey, 0) == 1)

            return false;



        totalPoints = AddOfflineUserPoints(pointsToAdd);

        PlayerPrefs.SetInt(awardKey, 1);

        PlayerPrefs.Save();

        return true;

    }

    private bool IsSavedCustomCast(CastData cast)

    {

        List<SerializableCharacterData> savedCasts = LoadSavedCastsFromPrefs();

        foreach (var saved in savedCasts)

        {

            if (saved == null) continue;



            if (!string.IsNullOrEmpty(cast.characterId) &&

                string.Equals(saved.characterId, cast.characterId, StringComparison.Ordinal))

            {

                return true;

            }



            if (DoesSavedCastMatch(cast, saved))

            {

                if (string.IsNullOrEmpty(cast.characterId))

                    cast.characterId = saved.characterId;

                return true;

            }

        }



        return false;

    }

    private static bool DoesSavedCastMatch(CastData cast, SerializableCharacterData saved)

    {

        return string.Equals(NormalizeCastValue(saved.name), NormalizeCastValue(cast.name), StringComparison.Ordinal) &&

               string.Equals(NormalizeCastValue(saved.prefabName), NormalizeCastValue(cast.prefabName), StringComparison.Ordinal) &&

               string.Equals(NormalizeCastValue(saved.instrumentId), NormalizeCastValue(cast.audioId), StringComparison.Ordinal) &&

               string.Equals(NormalizeCastValue(saved.danceAnimId), NormalizeCastValue(cast.danceAnimId), StringComparison.Ordinal);

    }

    private static string GetCustomCastUseAwardKey(CastData cast)

    {

        string stableId = !string.IsNullOrEmpty(cast.characterId)

            ? cast.characterId

            : StableHash(BuildCustomCastSignature(cast));

        return CUSTOM_CAST_USE_AWARD_PREFIX + stableId;

    }

    private static string BuildCustomCastSignature(CastData cast)

    {

        return $"{NormalizeCastValue(cast.name)}|{NormalizeCastValue(cast.prefabName)}|{NormalizeCastValue(cast.audioId)}|{NormalizeCastValue(cast.danceAnimId)}";

    }

    private static string NormalizeCastValue(string value)

    {

        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToLowerInvariant();

    }

    private static string StableHash(string value)

    {

        unchecked

        {

            uint hash = 2166136261;

            string input = value ?? string.Empty;

            for (int i = 0; i < input.Length; i++)

            {

                hash ^= input[i];

                hash *= 16777619;

            }

            return hash.ToString("x8");

        }

    }

}

// ─────────────────────────────────────────────────────────────────────────────
// Editor Band Configurations for Offline Custom Bands
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class EditorBandMember
{
    [Tooltip("Tên hiển thị của thành viên")]
    [HideInInspector]
    public string castName;

    [Tooltip("Chỉ số (Index) của Prefab nhân vật trong Character Catalog của MainMenuDataManager")]
    public int castPrefabIndex;

    [Tooltip("Tên State hoạt ảnh nhảy của nhân vật này trong Animator (ví dụ: Dance1, Dance2, Dance3)")]
    public string danceAnimId;

    [Tooltip("Chỉ số (Index) của Prefab nhạc cụ trong Instrument Catalog của MainMenuDataManager")]
    public int instrumentPrefabIndex;

    [Tooltip("File âm thanh nhạc cụ hoặc giọng hát riêng của nhân vật này")]
    public AudioClip audioClip;

    [Tooltip("Nếu tích chọn, âm lượng của nhân vật này sẽ nhỏ lại khi có nhân vật khác cùng được thả ra AR")]
    [HideInInspector]
    public bool reduceVolumeWhenTogether = true;

    [Tooltip("Mức độ giảm âm lượng khi chơi chung (Ví dụ: 0.3 có nghĩa là giảm đi 30%, âm lượng còn 70%)")]
    [Range(0f, 1f)]
    [HideInInspector]
    public float reduceAmount = 0.3f;
}

[System.Serializable]
public class EditorBandData
{
    [Tooltip("Tên ban nhạc")]
    [HideInInspector]
    public string bandName;



    [Tooltip("Số điểm cộng cho người chơi khi đặt đủ 4 Cast của band này")]

    [Min(0)]

    public int point;

    [Tooltip("Danh sách các thành viên trong ban nhạc (Tối đa 4 cast)")]
    public List<EditorBandMember> members = new List<EditorBandMember>();

    [Tooltip("File âm thanh bài hát hoàn chỉnh (Được phát khi thả đủ 4 cast ra AR)")]
    public AudioClip fullSongAudio;
}
