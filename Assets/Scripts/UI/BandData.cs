using System;
using System.Collections.Generic;

/// <summary>
/// Lưu trữ danh sách thông tin về một ban nhạc (Band) gồm tối đa 4 cast.
/// </summary>
[Serializable]
public class BandData
{
    /// <summary>
    /// Danh sách tối đa 4 cast thành viên trong ban nhạc.
    /// </summary>
    public List<CastData> casts = new List<CastData>();

    public BandData()
    {
    }

    public BandData(List<CastData> casts)
    {
        if (casts != null)
        {
            this.casts = new List<CastData>(casts);
        }
    }
}
