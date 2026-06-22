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
        _auth = FirebaseAuth.DefaultInstance;
        Debug.Log("[AuthManager] Firebase Auth đã khởi tạo thành công.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Synchronous helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra người dùng hiện tại đã đăng nhập hay chưa.
    /// </summary>
    public bool IsLoggedIn()
    {
        return _auth?.CurrentUser != null;
    }

    /// <summary>
    /// Lấy email của người dùng hiện đang đăng nhập.
    /// </summary>
    public string GetLoggedInUser()
    {
        if (_auth?.CurrentUser != null)
        {
            return !string.IsNullOrEmpty(_auth.CurrentUser.DisplayName) 
                ? _auth.CurrentUser.DisplayName 
                : _auth.CurrentUser.Email;
        }
        return "";
    }

    /// <summary>
    /// Đăng xuất tài khoản hiện tại.
    /// </summary>
    public void Logout()
    {
        _auth?.SignOut();
        Debug.Log("[AuthManager] Đã đăng xuất tài khoản.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Async Firebase operations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đăng ký tài khoản mới bằng Email và Mật khẩu.
    /// </summary>
    public async Task<(bool success, string errorMessage)> RegisterAsync(string email, string password, string displayName = "")
    {
        email = email?.Trim().ToLower();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return (false, "Email và mật khẩu không được để trống!");

        if (password.Length < 6)
            return (false, "Mật khẩu phải có tối thiểu 6 ký tự!");

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                var authResult = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                
                // Cập nhật tên hiển thị nếu có
                if (authResult.User != null && !string.IsNullOrEmpty(displayName))
                {
                    UserProfile profile = new UserProfile { DisplayName = displayName };
                    await authResult.User.UpdateUserProfileAsync(profile);
                }

                Debug.Log($"[AuthManager] Đăng ký thành công tài khoản: {email}");
                return (true, "");
            }
            catch (FirebaseException ex)
            {
                string msg = MapFirebaseError((AuthError)ex.ErrorCode);
                Debug.LogWarning($"[AuthManager] Lỗi đăng ký: {ex.ErrorCode} – {ex.Message}");
                return (false, msg);
            }
        });
    }

    /// <summary>
    /// Đăng nhập bằng Email và Mật khẩu.
    /// </summary>
    public async Task<(bool success, string errorMessage)> LoginAsync(string email, string password)
    {
        email = email?.Trim().ToLower();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return (false, "Email và mật khẩu không được để trống!");

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                await _auth.SignInWithEmailAndPasswordAsync(email, password);
                Debug.Log($"[AuthManager] Đăng nhập thành công: {email}");
                return (true, "");
            }
            catch (FirebaseException ex)
            {
                string msg = MapFirebaseError((AuthError)ex.ErrorCode);
                Debug.LogWarning($"[AuthManager] Lỗi đăng nhập: {ex.ErrorCode} – {ex.Message}");
                return (false, msg);
            }
        });
    }

    /// <summary>
    /// Đổi mật khẩu cho người dùng hiện tại.
    /// Tự động xác thực lại (ReAuthenticate) trước khi đổi mật khẩu theo chuẩn bảo mật Firebase.
    /// </summary>
    public async Task<(bool success, string errorMessage)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (!IsLoggedIn())
            return (false, "Vui lòng đăng nhập để thực hiện chức năng này!");

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            return (false, "Mật khẩu mới phải từ 6 ký tự trở lên!");

        FirebaseUser user = _auth.CurrentUser;

        return await NetworkGuard.RunAsync(async () =>
        {
            // 1. Xác thực lại để đảm bảo phiên đăng nhập còn hiệu lực
            try
            {
                Credential credential = EmailAuthProvider.GetCredential(user.Email, oldPassword);
                await user.ReauthenticateAsync(credential);
            }
            catch (FirebaseException ex)
            {
                Debug.LogWarning($"[AuthManager] Xác thực lại thất bại: {ex.Message}");
                return (false, "Mật khẩu cũ không chính xác!");
            }

            // 2. Cập nhật mật khẩu mới
            try
            {
                await user.UpdatePasswordAsync(newPassword);
                Debug.Log($"[AuthManager] Đổi mật khẩu thành công cho user: {user.Email}");
                return (true, "");
            }
            catch (FirebaseException ex)
            {
                string msg = MapFirebaseError((AuthError)ex.ErrorCode);
                Debug.LogWarning($"[AuthManager] Lỗi đổi mật khẩu: {ex.ErrorCode} – {ex.Message}");
                return (false, msg);
            }
        });
    }

    /// <summary>
    /// Gửi email đặt lại mật khẩu đến địa chỉ email được cung cấp.
    /// </summary>
    public async Task<(bool success, string errorMessage)> ForgotPasswordAsync(string email)
    {
        email = email?.Trim().ToLower();

        if (string.IsNullOrEmpty(email))
            return (false, "Vui lòng nhập Email để khôi phục mật khẩu!");

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                await _auth.SendPasswordResetEmailAsync(email);
                Debug.Log($"[AuthManager] Đã gửi email đặt lại mật khẩu tới: {email}");
                return (true, "");
            }
            catch (FirebaseException ex)
            {
                string msg = MapFirebaseError((AuthError)ex.ErrorCode);
                Debug.LogWarning($"[AuthManager] Lỗi gửi email reset: {ex.ErrorCode} – {ex.Message}");
                return (false, msg);
            }
        });
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
