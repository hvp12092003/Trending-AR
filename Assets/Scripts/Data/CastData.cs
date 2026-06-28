using System;
using System.Collections.Generic;

/// <summary>
/// Lưu trữ các trường dữ liệu của một cast (nhân vật).
/// Nhạc cụ và Audio (kể cả giọng nói tự thu) đều được quy về một trường audioId duy nhất.
/// </summary>
[Serializable]
public class CastData
{
    /// <summary>Stable id for a saved custom cast.</summary>
    public string characterId;

    /// <summary>Name of the cast.</summary>
    public string name;

    /// <summary>Tên Prefab đại diện cho nhân vật (dùng để load avatar và spawn model 3D).</summary>
    public string prefabName;

    /// <summary>
    /// ID nhạc cụ / audio được chọn cho nhân vật này
    /// (ví dụ: "guitar", "drum", "piano" hoặc ID audio tự thu).
    /// </summary>
    public string audioId;

    /// <summary>ID animation nhảy hiện tại đang được chọn hoặc hoạt động.</summary>
    public string danceAnimId;

    /// <summary>Danh sách các ID animation nhảy mà nhân vật này sở hữu.</summary>
    public List<string> danceAnimIds = new List<string>();

    public CastData() { }

    public CastData(string name, string audioId, string danceAnimId, List<string> danceAnimIds = null)
        : this(name, "", audioId, danceAnimId, danceAnimIds)
    {
    }

    public CastData(string name, string prefabName, string audioId, string danceAnimId, List<string> danceAnimIds = null)
    {
        this.name       = name;
        this.prefabName = prefabName;
        this.audioId    = audioId;
        this.danceAnimId = danceAnimId;

        if (danceAnimIds != null)
        {
            this.danceAnimIds = new List<string>(danceAnimIds);
        }
        else
        {
            this.danceAnimIds.Clear();
            if (!string.IsNullOrEmpty(danceAnimId))
                this.danceAnimIds.Add(danceAnimId);
        }
    }
}
