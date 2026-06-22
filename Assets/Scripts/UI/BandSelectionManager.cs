using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý việc lưu trữ dữ liệu của ban nhạc đang được chọn để chuyển tiếp giữa các Scene (ví dụ từ Main Menu sang Scene AR).
/// </summary>
public static class BandSelectionManager
{
    /// <summary>
    /// Dữ liệu ban nhạc được chọn.
    /// </summary>
    public static BandData SelectedBand { get; set; }

    /// <summary>
    /// Danh sách các Sprite ảnh đại diện của các thành viên trong ban nhạc được chọn.
    /// </summary>
    public static List<Sprite> SelectedBandAvatars { get; set; } = new List<Sprite>();

    /// <summary>
    /// Tên hiển thị của ban nhạc đang được chọn.
    /// </summary>
    public static string SelectedBandName { get; set; }

    /// <summary>
    /// Xóa thông tin ban nhạc đang được chọn.
    /// </summary>
    public static void ClearSelection()
    {
        SelectedBand = null;
        if (SelectedBandAvatars != null)
        {
            SelectedBandAvatars.Clear();
        }
        SelectedBandName = string.Empty;
    }
}
