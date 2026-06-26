using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainMenuPrefabCatalog", menuName = "Trending AR/Main Menu Prefab Catalog")]
public class MainMenuPrefabCatalog : ScriptableObject
{
    [Header("Character Catalog")]
    [Tooltip("Drag character prefabs here.")]
    [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

    [Header("Instrument Catalog")]
    [Tooltip("Drag instrument prefabs here.")]
    [SerializeField] private List<GameObject> instrumentPrefabs = new List<GameObject>();

    public IReadOnlyList<GameObject> CharacterPrefabs => characterPrefabs;
    public IReadOnlyList<GameObject> InstrumentPrefabs => instrumentPrefabs;

    public GameObject GetCharacterPrefab(string prefabName)
    {
        return GetPrefabByName(characterPrefabs, prefabName);
    }

    public GameObject GetInstrumentPrefab(string prefabName)
    {
        return GetPrefabByName(instrumentPrefabs, prefabName);
    }

    private static GameObject GetPrefabByName(List<GameObject> prefabs, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName) || prefabs == null)
        {
            return null;
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && prefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }
}
