using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "MainMenuPrefabCatalog", menuName = "Trending AR/Main Menu Prefab Catalog")]
public class MainMenuPrefabCatalog : ScriptableObject
{
    private static readonly IReadOnlyList<string> EmptyAnimationIds = Array.Empty<string>();

    [Serializable]
    public class PrefabAssetEntry
    {
        [Tooltip("Stable id saved in PlayerPrefs. Keep this unchanged after release.")]
        [SerializeField] private string id;

        [Tooltip("Addressables address/key. When set, this is preferred over the direct prefab.")]
        [SerializeField] private string addressKey;

        [Header("UI Metadata")]
        [SerializeField] private string displayName;
        [SerializeField] private Sprite avatar;

        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id;
                if (!string.IsNullOrWhiteSpace(addressKey)) return addressKey;
                return displayName;
            }
        }

        public string AddressKey => addressKey;
        public GameObject DirectPrefab => null;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                return !string.IsNullOrWhiteSpace(Id) ? Id : "Prefab";
            }
        }

        public string Category => null;

        public Sprite Avatar
        {
            get
            {
                return avatar;
            }
        }

        public IReadOnlyList<string> AnimationIds
        {
            get
            {
                return EmptyAnimationIds;
            }
        }

        public string DefaultAnimationId
        {
            get
            {
                return "";
            }
        }

        public bool Matches(string lookupId)
        {
            if (string.IsNullOrWhiteSpace(lookupId)) return false;

            return EqualsIgnoreCase(Id, lookupId) ||
                   EqualsIgnoreCase(addressKey, lookupId) ||
                   EqualsIgnoreCase(displayName, lookupId);
        }

        public async Task<GameObject> LoadPrefabAsync()
        {
            if (!string.IsNullOrWhiteSpace(addressKey))
            {
                GameObject addressablePrefab = await AddressablePrefabLoader.LoadGameObjectAsync(addressKey);
                if (addressablePrefab != null)
                {
                    return addressablePrefab;
                }
            }

            return null;
        }

        internal static PrefabAssetEntry FromPrefab(GameObject sourcePrefab)
        {
            return new PrefabAssetEntry { id = sourcePrefab != null ? sourcePrefab.name : "" };
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) &&
                   !string.IsNullOrWhiteSpace(b) &&
                   a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public class CountryFlagEntry
    {
        [Tooltip("Stable id saved in PlayerPrefs, for example VN, US, JP. Keep this unchanged after release.")]
        [SerializeField] private string id;

        [Tooltip("Addressables address/key for the flag sprite. This keeps country flags out of scene memory until needed.")]
        [SerializeField] private string addressKey;

        [SerializeField] private string displayName;
        [SerializeField] private string isoCode;

        [Tooltip("Optional editor fallback. Leave empty in production when using Addressables for the smallest memory footprint.")]
        [SerializeField] private Sprite previewSprite;

        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id)) return id;
                if (!string.IsNullOrWhiteSpace(isoCode)) return isoCode;
                if (!string.IsNullOrWhiteSpace(addressKey)) return addressKey;
                return displayName;
            }
        }

        public string AddressKey => addressKey;
        public string Key => !string.IsNullOrWhiteSpace(addressKey) ? addressKey : Id;
        public string IsoCode => isoCode;
        public Sprite PreviewSprite => previewSprite;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                if (!string.IsNullOrWhiteSpace(isoCode)) return isoCode.ToUpperInvariant();
                return !string.IsNullOrWhiteSpace(Id) ? Id : "Country";
            }
        }

        public bool Matches(string lookupId)
        {
            if (string.IsNullOrWhiteSpace(lookupId)) return false;

            return EqualsIgnoreCase(Id, lookupId) ||
                   EqualsIgnoreCase(addressKey, lookupId) ||
                   EqualsIgnoreCase(displayName, lookupId) ||
                   EqualsIgnoreCase(isoCode, lookupId);
        }

        public async Task<Sprite> LoadSpriteAsync()
        {
            if (!string.IsNullOrWhiteSpace(addressKey))
            {
                Sprite addressableSprite = await AddressablePrefabLoader.LoadSpriteAsync(addressKey);
                if (addressableSprite != null)
                {
                    return addressableSprite;
                }
            }

            return previewSprite;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) &&
                   !string.IsNullOrWhiteSpace(b) &&
                   a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Header("Character Catalog")]
    [Tooltip("Addressables-ready character entries.")]
    [SerializeField] private List<PrefabAssetEntry> characterEntries = new List<PrefabAssetEntry>();

    [Header("Instrument Catalog")]
    [Tooltip("Addressables-ready instrument entries.")]
    [SerializeField] private List<PrefabAssetEntry> instrumentEntries = new List<PrefabAssetEntry>();

    [Header("Country Flag Catalog")]
    [Tooltip("Addressables-ready flag sprites. Use small sprites and address keys like flag/0 to avoid loading the whole flag set into RAM.")]
    [SerializeField] private List<CountryFlagEntry> countryFlagEntries = new List<CountryFlagEntry>();

    private readonly List<PrefabAssetEntry> _mergedCharacterEntries = new List<PrefabAssetEntry>();
    private readonly List<PrefabAssetEntry> _mergedInstrumentEntries = new List<PrefabAssetEntry>();
    private readonly List<GameObject> _directCharacterPrefabs = new List<GameObject>();
    private readonly List<GameObject> _directInstrumentPrefabs = new List<GameObject>();

    public IReadOnlyList<PrefabAssetEntry> GetCharacterEntries(IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return BuildMergedEntries(_mergedCharacterEntries, characterEntries, fallbackPrefabs);
    }

    public IReadOnlyList<PrefabAssetEntry> GetInstrumentEntries(IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return BuildMergedEntries(_mergedInstrumentEntries, instrumentEntries, fallbackPrefabs);
    }

    public IReadOnlyList<CountryFlagEntry> GetCountryFlagEntries()
    {
        if (countryFlagEntries == null)
        {
            return Array.Empty<CountryFlagEntry>();
        }

        return countryFlagEntries;
    }

    public List<GameObject> GetDirectCharacterPrefabs(IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return BuildDirectPrefabs(_directCharacterPrefabs, characterEntries, fallbackPrefabs);
    }

    public List<GameObject> GetDirectInstrumentPrefabs(IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return BuildDirectPrefabs(_directInstrumentPrefabs, instrumentEntries, fallbackPrefabs);
    }

    public PrefabAssetEntry GetCharacterEntry(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return GetEntryById(GetCharacterEntries(fallbackPrefabs), prefabName);
    }

    public PrefabAssetEntry GetInstrumentEntry(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        return GetEntryById(GetInstrumentEntries(fallbackPrefabs), prefabName);
    }

    public CountryFlagEntry GetCountryFlagEntry(string flagIdOrKey)
    {
        if (countryFlagEntries == null || string.IsNullOrWhiteSpace(flagIdOrKey))
        {
            return null;
        }

        for (int i = 0; i < countryFlagEntries.Count; i++)
        {
            CountryFlagEntry entry = countryFlagEntries[i];
            if (entry != null && entry.Matches(flagIdOrKey))
            {
                return entry;
            }
        }

        return null;
    }

    public CountryFlagEntry GetCountryFlagEntryByKey(string flagKey)
    {
        if (countryFlagEntries == null || string.IsNullOrWhiteSpace(flagKey))
        {
            return null;
        }

        for (int i = 0; i < countryFlagEntries.Count; i++)
        {
            CountryFlagEntry entry = countryFlagEntries[i];
            if (entry != null && string.Equals(entry.AddressKey, flagKey, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return GetCountryFlagEntry(flagKey);
    }

    public GameObject GetCharacterPrefab(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        PrefabAssetEntry entry = GetCharacterEntry(prefabName, fallbackPrefabs);
        return entry != null ? entry.DirectPrefab : null;
    }

    public GameObject GetInstrumentPrefab(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        PrefabAssetEntry entry = GetInstrumentEntry(prefabName, fallbackPrefabs);
        return entry != null ? entry.DirectPrefab : null;
    }

    public async Task<GameObject> LoadCharacterPrefabAsync(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        PrefabAssetEntry entry = GetCharacterEntry(prefabName, fallbackPrefabs);
        return entry != null ? await entry.LoadPrefabAsync() : null;
    }

    public async Task<GameObject> LoadInstrumentPrefabAsync(string prefabName, IEnumerable<GameObject> fallbackPrefabs = null)
    {
        PrefabAssetEntry entry = GetInstrumentEntry(prefabName, fallbackPrefabs);
        return entry != null ? await entry.LoadPrefabAsync() : null;
    }

    public async Task<Sprite> LoadCountryFlagSpriteAsync(string flagId)
    {
        CountryFlagEntry entry = GetCountryFlagEntry(flagId);
        return entry != null ? await entry.LoadSpriteAsync() : null;
    }

    public async Task<Sprite> LoadCountryFlagSpriteByKeyAsync(string flagKey)
    {
        CountryFlagEntry entry = GetCountryFlagEntryByKey(flagKey);
        return entry != null ? await entry.LoadSpriteAsync() : null;
    }

    public Sprite GetCountryFlagPreviewSprite(string flagId)
    {
        CountryFlagEntry entry = GetCountryFlagEntry(flagId);
        return entry != null ? entry.PreviewSprite : null;
    }

    public Sprite GetCountryFlagPreviewSpriteByKey(string flagKey)
    {
        CountryFlagEntry entry = GetCountryFlagEntryByKey(flagKey);
        return entry != null ? entry.PreviewSprite : null;
    }

    public static List<PrefabAssetEntry> CreateRuntimeEntries(IEnumerable<GameObject> prefabs)
    {
        List<PrefabAssetEntry> entries = new List<PrefabAssetEntry>();
        AddLegacyPrefabs(entries, prefabs);
        return entries;
    }

    public static List<string> GetAnimationIdsFromCastConfig(CastPrefab config)
    {
        List<string> ids = new List<string>();
        if (config == null || config.animations == null)
        {
            return ids;
        }

        for (int i = 0; i < config.animations.Count; i++)
        {
            CastAnimation anim = config.animations[i];
            if (anim.animation == null || string.IsNullOrWhiteSpace(anim.animation.name))
            {
                continue;
            }

            if (!ids.Contains(anim.animation.name))
            {
                ids.Add(anim.animation.name);
            }
        }

        return ids;
    }

    private static IReadOnlyList<PrefabAssetEntry> BuildMergedEntries(
        List<PrefabAssetEntry> output,
        List<PrefabAssetEntry> configuredEntries,
        IEnumerable<GameObject> fallbackPrefabs)
    {
        output.Clear();

        if (configuredEntries != null)
        {
            for (int i = 0; i < configuredEntries.Count; i++)
            {
                PrefabAssetEntry entry = configuredEntries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id) && !ContainsEntry(output, entry.Id))
                {
                    output.Add(entry);
                }
            }
        }

        AddLegacyPrefabs(output, fallbackPrefabs);
        return output;
    }

    private static List<GameObject> BuildDirectPrefabs(
        List<GameObject> output,
        List<PrefabAssetEntry> configuredEntries,
        IEnumerable<GameObject> fallbackPrefabs)
    {
        output.Clear();

        if (configuredEntries != null)
        {
            for (int i = 0; i < configuredEntries.Count; i++)
            {
                GameObject prefab = configuredEntries[i] != null ? configuredEntries[i].DirectPrefab : null;
                AddPrefab(output, prefab);
            }
        }

        AddPrefabs(output, fallbackPrefabs);
        return output;
    }

    private static PrefabAssetEntry GetEntryById(IReadOnlyList<PrefabAssetEntry> entries, string prefabName)
    {
        if (entries == null || string.IsNullOrWhiteSpace(prefabName))
        {
            return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PrefabAssetEntry entry = entries[i];
            if (entry != null && entry.Matches(prefabName))
            {
                return entry;
            }
        }

        return null;
    }

    private static void AddLegacyPrefabs(List<PrefabAssetEntry> output, IEnumerable<GameObject> prefabs)
    {
        if (prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null || ContainsEntry(output, prefab.name))
            {
                continue;
            }

            output.Add(PrefabAssetEntry.FromPrefab(prefab));
        }
    }

    private static bool ContainsEntry(List<PrefabAssetEntry> entries, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Matches(id))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPrefabs(List<GameObject> output, IEnumerable<GameObject> prefabs)
    {
        if (prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            AddPrefab(output, prefab);
        }
    }

    private static void AddPrefab(List<GameObject> output, GameObject prefab)
    {
        if (prefab == null || output.Contains(prefab))
        {
            return;
        }

        output.Add(prefab);
    }
}
