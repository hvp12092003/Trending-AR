using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// Runtime Data Class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Dữ liệu runtime của một nhân vật đã được tạo và lưu trữ.
/// </summary>
public class CharacterData
{
    public string       characterId      { get; set; }
    public string       name             { get; set; }
    public string       prefabName       { get; set; }
    public long         createdAtSeconds { get; set; }
    public string       instrumentId     { get; set; }
    public string       danceAnimId      { get; set; }
    public List<string> danceAnimIds     { get; set; } = new List<string>();
    public bool         usePrefabAvatar  { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Serializable Wrappers – dùng PlayerPrefs + JsonUtility
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public class SerializableCharacterData
{
    public string characterId;
    public string name;
    public string prefabName;
    public string instrumentId;
    public string danceAnimId;
    public List<string> danceAnimIds = new List<string>();
    public long createdAtSeconds;
    public bool usePrefabAvatar;
}

[Serializable]
public class SerializableCharacterDataList
{
    public List<SerializableCharacterData> characters = new List<SerializableCharacterData>();
}
