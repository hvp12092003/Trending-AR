using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// Runtime Data Class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lưu trữ danh sách thông tin về một ban nhạc (Band) gồm tối đa 4 cast.
/// </summary>
[Serializable]
public class BandData
{
    public string bandId;
    public string name;

    /// <summary>Danh sách tối đa 4 cast thành viên trong ban nhạc.</summary>
    public List<CastData> casts = new List<CastData>();

    public BandData() { }

    public BandData(List<CastData> casts)
    {
        if (casts != null)
            this.casts = new List<CastData>(casts);
    }

    public BandData(string bandId, string name, List<CastData> casts)
    {
        this.bandId = bandId;
        this.name   = name;
        if (casts != null)
            this.casts = new List<CastData>(casts);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Serializable Wrappers – dùng PlayerPrefs + JsonUtility
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public class SerializableBandData
{
    public string bandId;
    public string name;
    public List<SerializableCharacterData> casts = new List<SerializableCharacterData>();
    public long createdAtSeconds;
}

[Serializable]
public class SerializableBandDataList
{
    public List<SerializableBandData> bands = new List<SerializableBandData>();
}
