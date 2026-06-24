using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// Runtime Data Class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Dữ liệu runtime của một bản ghi âm đã lưu.
/// </summary>
public class RecordingData
{
    public string recordingId      { get; set; }
    public string name             { get; set; }
    public string audioBase64      { get; set; }
    public long   createdAtSeconds { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Serializable Wrappers – dùng PlayerPrefs + JsonUtility
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public class SerializableRecordingData
{
    public string recordingId;
    public string name;
    public string audioBase64;
    public long createdAtSeconds;
}

[Serializable]
public class SerializableRecordingDataList
{
    public List<SerializableRecordingData> recordings = new List<SerializableRecordingData>();
}
