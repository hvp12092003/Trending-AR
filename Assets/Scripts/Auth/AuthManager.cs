using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Quản lý xác thực (Authentication) sử dụng Firebase Authentication SDK.
/// Tất cả các phương thức bất đồng bộ đều trả về (bool success, string errorMessage).
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    private FirebaseAuth _auth;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFirebase()
    {
        try
        {
            _auth = FirebaseAuth.DefaultInstance;
            Debug.Log("[AuthManager] Firebase Auth đã khởi tạo (chế độ offline).");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[AuthManager] Bỏ qua lỗi khởi tạo Firebase Auth ở chế độ offline: " + ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Synchronous helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra người dùng hiện tại đã đăng nhập hay chưa.
    /// </summary>
    public bool IsLoggedIn()
    {
        return true;
    }

    /// <summary>
    /// Lấy email của người dùng hiện đang đăng nhập.
    /// </summary>
    public string GetLoggedInUser()
    {
        string username = PlayerPrefs.GetString("OfflineUserName", "");
        if (string.IsNullOrEmpty(username) || username == "Offline User")
        {
            System.DateTime now = System.DateTime.Now;
            string day = now.Day.ToString();
            string month = now.Month.ToString();
            string year = now.Year.ToString();
            string hour = now.Hour.ToString("D2");
            string minute = now.Minute.ToString("D2");
            string second = now.Second.ToString("D2");
            username = $"Player{day}{month}{year}{hour}{minute}{second}";
            
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
    /// Đăng xuất tài khoản hiện tại.
    /// </summary>
    public void Logout()
    {
        Debug.Log("[AuthManager] Đã gọi đăng xuất (Offline Mode - No-op).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Async Firebase operations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đăng ký tài khoản mới bằng Email và Mật khẩu.
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
    /// Đăng nhập bằng Email và Mật khẩu.
    /// </summary>
    public async Task<(bool success, string errorMessage)> LoginAsync(string email, string password)
    {
        return await Task.FromResult((true, ""));
    }

    /// <summary>
    /// Đổi mật khẩu cho người dùng hiện tại.
    /// </summary>
    public async Task<(bool success, string errorMessage)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        return await Task.FromResult((true, ""));
    }

    /// <summary>
    /// Gửi email đặt lại mật khẩu đến địa chỉ email được cung cấp.
    /// </summary>
    public async Task<(bool success, string errorMessage)> ForgotPasswordAsync(string email)
    {
        return await Task.FromResult((true, ""));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Error mapping helper
    // ─────────────────────────────────────────────────────────────────────────

    private static string MapFirebaseError(AuthError error)
    {
        return error switch
        {
            AuthError.EmailAlreadyInUse      => "Địa chỉ email này đã được đăng ký trước đó!",
            AuthError.InvalidEmail           => "Địa chỉ email không hợp lệ!",
            AuthError.WeakPassword           => "Mật khẩu quá yếu. Vui lòng chọn mật khẩu mạnh hơn!",
            AuthError.UserNotFound           => "Không tìm thấy tài khoản với email này!",
            AuthError.WrongPassword          => "Mật khẩu đăng nhập không chính xác!",
            AuthError.NetworkRequestFailed   => "Lỗi kết nối mạng. Vui lòng kiểm tra Internet!",
            AuthError.TooManyRequests        => "Quá nhiều yêu cầu. Vui lòng thử lại sau!",
            AuthError.UserDisabled           => "Tài khoản này đã bị vô hiệu hóa!",
            AuthError.InvalidCredential      => "Thông tin xác thực không hợp lệ!",
            _                                => "Đã xảy ra lỗi. Vui lòng thử lại!"
        };
    }
}
