using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Quản lý việc đọc/ghi dữ liệu cục bộ cho màn hình Menu chính – phiên bản Offline.
/// Tất cả dữ liệu được lưu bằng PlayerPrefs + JsonUtility.
/// Data classes được định nghĩa tập trung trong Assets/Scripts/Data/.
/// </summary>
public class MainMenuDataManager : MonoBehaviour
{
    public static MainMenuDataManager Instance { get; private set; }

    [Header("Character Catalog")]
    [Tooltip("Danh sach prefab nhan vat. Keo cac prefab Cast vao day de MainMenuDataManager tu doc CastPrefab.")]
    [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

    public List<GameObject> CharacterPrefabs => characterPrefabs;

    [Header("Instrument Catalog")]
    [Tooltip("Danh sách prefab nhạc cụ. Kéo các prefab nhạc cụ vào đây để quản lý tập trung.")]
    [SerializeField] private List<GameObject> instrumentPrefabs = new List<GameObject>();

    public List<GameObject> InstrumentPrefabs => instrumentPrefabs;

    private const string LAST_SELECTED_CAST_KEY    = "LastSelectedCastJSON";
    private const string SAVED_CASTS_PREFS_KEY      = "SavedCastsDataJSON";
    private const string SAVED_RECORDINGS_PREFS_KEY = "SavedRecordingsDataJSON";
    private const string SAVED_BANDS_PREFS_KEY      = "SavedBandsDataJSON";

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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Đọc CastPrefab từ danh sách Prefab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Truyền vào danh sách các Cast (Prefab) và đọc ra danh sách Component CastPrefab của từng prefab.
    /// </summary>
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

        if (characterPrefabs != null)
        {
            foreach (GameObject prefab in characterPrefabs)
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

    public Sprite GetCharacterAvatarSprite(string prefabName)
    {
        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab == null) return null;
        CastPrefab config = prefab.GetComponent<CastPrefab>();
        return config != null ? config.characterAvatar : null;
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

        List<string> danceAnimIds = new List<string>();
        if (!string.IsNullOrEmpty(selectedDanceAnimId))
            danceAnimIds.Add(selectedDanceAnimId);

        string displayName = string.IsNullOrEmpty(prefabName) ? "Custom Character" : prefabName;
        return new CastData(displayName, prefabName, audioId ?? "", selectedDanceAnimId, danceAnimIds);
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
        if (list.Count >= 7)
        {
            Debug.LogWarning("[MainMenuDataManager] Đã đạt giới hạn tối đa 7 nhân vật.");
            return Task.FromResult(false);
        }

        bool usePrefab = Resources.Load<Sprite>("Avatars/" + prefabName) == null;
        list.Add(new SerializableCharacterData
        {
            characterId      = "char_" + DateTime.UtcNow.Ticks,
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
        if (list.Count >= 7)
        {
            Debug.LogWarning("[MainMenuDataManager] Đã đạt giới hạn tối đa 7 nhân vật.");
            return Task.FromResult(false);
        }

        bool usePrefab = Resources.Load<Sprite>("Avatars/" + cast.prefabName) == null;
        list.Add(new SerializableCharacterData
        {
            characterId      = "char_" + DateTime.UtcNow.Ticks,
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

        list.Add(new UserData { userId = "offline_user_id", displayName = currentUserName, points = 1200 });
        list.Add(new UserData { userId = "bot_1", displayName = "Sunny Beats",  points = 950 });
        list.Add(new UserData { userId = "bot_2", displayName = "Neon Dancer",  points = 720 });
        list.Add(new UserData { userId = "bot_3", displayName = "AR Groover",   points = 510 });
        return Task.FromResult(list);
    }
}
