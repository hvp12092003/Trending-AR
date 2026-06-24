using UnityEngine;

/// <summary>
/// Quản lý danh tính người dùng – phiên bản Offline.
/// Tất cả dữ liệu được lưu cục bộ bằng PlayerPrefs.
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

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
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Luôn trả về true ở chế độ offline.</summary>
    public bool IsLoggedIn() => true;

    /// <summary>
    /// Lấy tên người dùng offline từ PlayerPrefs. Tự sinh tên nếu chưa có.
    /// </summary>
    public string GetLoggedInUser()
    {
        string username = PlayerPrefs.GetString("OfflineUserName", "");
        if (string.IsNullOrEmpty(username) || username == "Offline User")
        {
            System.DateTime now = System.DateTime.Now;
            username = $"Player{now.Day}{now.Month}{now.Year}{now.Hour:D2}{now.Minute:D2}{now.Second:D2}";
            PlayerPrefs.SetString("OfflineUserName", username);
            PlayerPrefs.Save();
        }
        return username;
    }

    /// <summary>Cập nhật tên hiển thị của người chơi offline.</summary>
    public void UpdateOfflineUserName(string newDisplayName)
    {
        if (!string.IsNullOrEmpty(newDisplayName))
        {
            PlayerPrefs.SetString("OfflineUserName", newDisplayName);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Đăng xuất (no-op ở chế độ offline).</summary>
    public void Logout()
    {
        Debug.Log("[AuthManager] Đã gọi đăng xuất (Offline Mode).");
    }
}
