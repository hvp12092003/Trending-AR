using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Quản lý xác thực người dùng – phiên bản Offline 100%.
/// Tất cả dữ liệu được lưu cục bộ bằng PlayerPrefs.
/// (Firebase Authentication sẽ được tích hợp lại sau.)
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
    // Synchronous helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra người dùng hiện tại đã đăng nhập hay chưa (luôn true ở chế độ offline).
    /// </summary>
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

    /// <summary>
    /// Cập nhật tên hiển thị của người chơi offline.
    /// </summary>
    public void UpdateOfflineUserName(string newDisplayName)
    {
        if (!string.IsNullOrEmpty(newDisplayName))
        {
            PlayerPrefs.SetString("OfflineUserName", newDisplayName);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Đăng xuất (No-op ở chế độ offline).
    /// </summary>
    public void Logout()
    {
        Debug.Log("[AuthManager] Đã gọi đăng xuất (Offline Mode – No-op).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Async stubs – sẽ kết nối Firebase Authentication sau
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đăng ký tài khoản mới (offline stub – luôn thành công).
    /// </summary>
    public async Task<(bool success, string errorMessage)> RegisterAsync(string email, string password, string displayName = "")
    {
        if (!string.IsNullOrEmpty(displayName))
        {
            PlayerPrefs.SetString("OfflineUserName", displayName);
            PlayerPrefs.Save();
        }
        return await Task.FromResult((true, ""));
    }

    /// <summary>
    /// Đăng nhập bằng Email và Mật khẩu (offline stub – luôn thành công).
    /// </summary>
    public async Task<(bool success, string errorMessage)> LoginAsync(string email, string password)
        => await Task.FromResult((true, ""));

    /// <summary>
    /// Đổi mật khẩu (offline stub – luôn thành công).
    /// </summary>
    public async Task<(bool success, string errorMessage)> ChangePasswordAsync(string oldPassword, string newPassword)
        => await Task.FromResult((true, ""));

    /// <summary>
    /// Gửi email đặt lại mật khẩu (offline stub – luôn thành công).
    /// </summary>
    public async Task<(bool success, string errorMessage)> ForgotPasswordAsync(string email)
        => await Task.FromResult((true, ""));
}
