using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện hiển thị cho một dòng trên bảng xếp hạng (Leaderboard).
/// </summary>
public class LeaderboardItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private Image bgImage;
    
    [Header("Màu sắc làm nổi bật")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0f, 0.2f); // Màu vàng nhạt

    private Color _defaultColor;
    private bool _hasDefaultColor = false;

    private void EnsureDefaultColor()
    {
        if (!_hasDefaultColor && bgImage != null)
        {
            _defaultColor = bgImage.color;
            _hasDefaultColor = true;
        }
    }

    /// <summary>
    /// Gán thông tin người dùng và thứ hạng lên dòng bảng xếp hạng.
    /// </summary>
    public void Setup(UserData data, int rank, bool isCurrentUser)
    {
        EnsureDefaultColor();

        if (rankText != null) 
            rankText.text = $"#{rank}";

        if (nameText != null) 
            nameText.text = data.displayName;

        if (pointsText != null) 
            pointsText.text = $"{data.points} Điểm";

        if (bgImage != null)
        {
            // Làm nổi bật nếu là tài khoản của chính người chơi hiện tại
            bgImage.color = isCurrentUser ? highlightColor : _defaultColor;
        }
    }
}
