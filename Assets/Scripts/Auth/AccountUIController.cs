using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Quản lý việc hiển thị, ẩn các Panel UI trong Account Scene và kết nối trực tiếp với AuthManager.
/// Các click handler sử dụng async void để await tác vụ Firebase bất đồng bộ.
/// </summary>
public class AccountUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signupPanel;
    [SerializeField] private GameObject changePassPanel;
    [SerializeField] private GameObject forgotPassPanel;
    [SerializeField] private GameObject welcomePanel;

    [Header("DOTween Settings")]
    [SerializeField] private float panelTransitionDuration = 0.25f;
    private GameObject currentActivePanel;

    [Header("Login Panel UI")]
    [SerializeField] private TMP_InputField loginEmail;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private TextMeshProUGUI loginNofiText;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button goToForgotPassButton;
    [SerializeField] private Button googleButton;
    [SerializeField] private Button facebookButton;

    [Header("Signup Panel UI")]
    [SerializeField] private TMP_InputField signupName;
    [SerializeField] private TMP_InputField signupEmail;
    [SerializeField] private TMP_InputField signupPassword;
    [SerializeField] private TMP_InputField signupConfirmPassword;
    [SerializeField] private TextMeshProUGUI signupNofiText;
    [SerializeField] private Button signupButton;
    [SerializeField] private Button goToLoginButton;

    [Header("Change Password Panel UI")]
    [SerializeField] private TMP_InputField changeCurrentPassword;
    [SerializeField] private TMP_InputField changeNewPassword;
    [SerializeField] private TMP_InputField changeConfirmNewPassword;
    [SerializeField] private TextMeshProUGUI changePassNofiText;
    [SerializeField] private Button changePasswordButton;
    [SerializeField] private Button changePassBackButton;

    [Header("Forgot Password Panel UI")]
    [SerializeField] private TMP_InputField forgotEmail;
    [SerializeField] private TextMeshProUGUI forgotNofiText;
    [SerializeField] private Button forgotSendButton;
    [SerializeField] private Button backToLoginFromForgotPassButton;

    [Header("Welcome Panel UI")]
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button goToChangePassButton;

    [Header("Navigation Settings")]
    [SerializeField] private string mainMenuSceneName = "Main Menu Scene";

    // ─────────────────────────────────────────────────────────────────────────
    // Interactive elements grouped per panel for easy lock/unlock
    // ─────────────────────────────────────────────────────────────────────────
    private List<Selectable> loginSelectables;
    private List<Selectable> signupSelectables;
    private List<Selectable> changePassSelectables;
    private List<Selectable> forgotSelectables;

    private void Start()
    {
        // Cache interactable element lists per panel
        loginSelectables = new List<Selectable>
            { loginEmail, loginPassword, loginButton, signUpButton, goToForgotPassButton };
        if (googleButton != null) loginSelectables.Add(googleButton);
        if (facebookButton != null) loginSelectables.Add(facebookButton);

        signupSelectables = new List<Selectable>
            { signupEmail, signupPassword, signupConfirmPassword, signupButton, goToLoginButton };
        if (signupName != null) signupSelectables.Add(signupName);

        changePassSelectables = new List<Selectable>
            { changeCurrentPassword, changeNewPassword, changePasswordButton, changePassBackButton };
        if (changeConfirmNewPassword != null) changePassSelectables.Add(changeConfirmNewPassword);

        forgotSelectables = new List<Selectable>
            { forgotEmail, forgotSendButton, backToLoginFromForgotPassButton };

        // Gán sự kiện cho các Button
        if (loginButton != null)            loginButton.onClick.AddListener(OnLoginClicked);
        if (signUpButton != null)           signUpButton.onClick.AddListener(() => ShowPanel(signupPanel));
        if (goToForgotPassButton != null)   goToForgotPassButton.onClick.AddListener(() => ShowPanel(forgotPassPanel));
        if (googleButton != null)           googleButton.onClick.AddListener(OnGoogleLoginClicked);
        if (facebookButton != null)         facebookButton.onClick.AddListener(OnFacebookLoginClicked);

        if (signupButton != null)           signupButton.onClick.AddListener(OnRegisterClicked);
        if (goToLoginButton != null)        goToLoginButton.onClick.AddListener(() => ShowPanel(loginPanel));

        if (changePasswordButton != null)   changePasswordButton.onClick.AddListener(OnChangePassClicked);
        if (changePassBackButton != null)   changePassBackButton.onClick.AddListener(ShowWelcomePanel);

        if (forgotSendButton != null)                  forgotSendButton.onClick.AddListener(OnForgotClicked);
        if (backToLoginFromForgotPassButton != null)   backToLoginFromForgotPassButton.onClick.AddListener(() => ShowPanel(loginPanel));

        if (startButton != null)            startButton.onClick.AddListener(OnStartGameClicked);
        if (logoutButton != null)           logoutButton.onClick.AddListener(OnLogoutClicked);
        if (goToChangePassButton != null)   goToChangePassButton.onClick.AddListener(() => ShowPanel(changePassPanel));

        // Kiểm tra trạng thái đăng nhập
        if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn())
        {
            ShowWelcomePanel();
        }
        else
        {
            ShowPanel(loginPanel);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Panel transitions (DOTween)
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null) return;
        if (targetPanel == currentActivePanel) return;

        GameObject previousPanel = currentActivePanel;
        currentActivePanel = targetPanel;

        // Xóa log lỗi cũ
        ClearErrors();

        // Ẩn panel cũ (nếu có)
        if (previousPanel != null)
        {
            // Tắt tương tác ngay lập tức
            CanvasGroup prevCg = previousPanel.GetComponent<CanvasGroup>();
            if (prevCg == null) prevCg = previousPanel.AddComponent<CanvasGroup>();
            prevCg.interactable = false;
            prevCg.blocksRaycasts = false;

            previousPanel.transform.DOKill();
            prevCg.DOKill();

            previousPanel.transform.DOScale(0.95f, panelTransitionDuration).SetEase(Ease.InQuad);
            prevCg.DOFade(0f, panelTransitionDuration).SetEase(Ease.InQuad).OnComplete(() =>
            {
                previousPanel.SetActive(false);
            });
        }
        else
        {
            // Nếu không có panel cũ, đảm bảo các panel khác được ẩn ngay lập tức
            if (loginPanel != null      && loginPanel != targetPanel)       loginPanel.SetActive(false);
            if (signupPanel != null     && signupPanel != targetPanel)      signupPanel.SetActive(false);
            if (changePassPanel != null && changePassPanel != targetPanel)  changePassPanel.SetActive(false);
            if (forgotPassPanel != null && forgotPassPanel != targetPanel)  forgotPassPanel.SetActive(false);
            if (welcomePanel != null    && welcomePanel != targetPanel)     welcomePanel.SetActive(false);
        }

        // Hiện panel mới
        targetPanel.SetActive(true);
        CanvasGroup targetCg = targetPanel.GetComponent<CanvasGroup>();
        if (targetCg == null) targetCg = targetPanel.AddComponent<CanvasGroup>();

        targetCg.interactable = true;
        targetCg.blocksRaycasts = true;

        // Reset state trước khi animate in
        targetCg.alpha = 0f;
        targetPanel.transform.localScale = Vector3.one * 0.95f;

        targetPanel.transform.DOKill();
        targetCg.DOKill();

        // Animate in
        targetPanel.transform.DOScale(1f, panelTransitionDuration).SetEase(Ease.OutBack);
        targetCg.DOFade(1f, panelTransitionDuration).SetEase(Ease.OutQuad);
    }

    private void ClearErrors()
    {
        if (loginNofiText != null)      loginNofiText.text = "";
        if (signupNofiText != null)     signupNofiText.text = "";
        if (changePassNofiText != null) changePassNofiText.text = "";
        if (forgotNofiText != null)     forgotNofiText.text = "";
    }

    private void ShowWelcomePanel()
    {
        ShowPanel(welcomePanel);
        if (welcomeText != null && AuthManager.Instance != null)
        {
            welcomeText.text = $"Xin chào,\n<color=#FFD700>{AuthManager.Instance.GetLoggedInUser()}</color>";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lock / Unlock helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SetSelectables(List<Selectable> selectables, bool interactable)
    {
        if (selectables == null) return;
        foreach (var s in selectables)
        {
            if (s != null) s.interactable = interactable;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Async click handlers
    // ─────────────────────────────────────────────────────────────────────────

    private async void OnLoginClicked()
    {
        if (AuthManager.Instance == null) return;

        string email    = loginEmail    != null ? loginEmail.text    : "";
        string password = loginPassword != null ? loginPassword.text : "";

        SetSelectables(loginSelectables, false);

        var (success, error) = await AuthManager.Instance.LoginAsync(email, password);

        SetSelectables(loginSelectables, true);

        if (success)
        {
            ShowWelcomePanel();
        }
        else
        {
            if (loginNofiText != null)
            {
                loginNofiText.color = Color.red;
                loginNofiText.text  = error;
            }
        }
    }

    private async void OnRegisterClicked()
    {
        if (AuthManager.Instance == null) return;

        string email           = signupEmail           != null ? signupEmail.text           : "";
        string password        = signupPassword        != null ? signupPassword.text        : "";
        string confirmPassword = signupConfirmPassword != null ? signupConfirmPassword.text : "";
        string displayName     = signupName            != null ? signupName.text            : "";

        if (password != confirmPassword)
        {
            if (signupNofiText != null)
            {
                signupNofiText.color = Color.red;
                signupNofiText.text  = "Mật khẩu nhập lại không trùng khớp!";
            }
            return;
        }

        SetSelectables(signupSelectables, false);

        var (success, error) = await AuthManager.Instance.RegisterAsync(email, password, displayName);

        SetSelectables(signupSelectables, true);

        if (success)
        {
            if (signupNofiText != null)
            {
                signupNofiText.color = Color.green;
                signupNofiText.text  = "Đăng ký tài khoản thành công!";
            }

            // Điền sẵn email đăng nhập
            if (loginEmail    != null) loginEmail.text    = email;
            if (loginPassword != null) loginPassword.text = "";

            // Chuyển sang màn hình đăng nhập sau 1.5 giây
            Invoke(nameof(GoToLogin), 1.5f);
        }
        else
        {
            if (signupNofiText != null)
            {
                signupNofiText.color = Color.red;
                signupNofiText.text  = error;
            }
        }
    }

    private void GoToLogin()
    {
        ShowPanel(loginPanel);
    }

    private async void OnChangePassClicked()
    {
        if (AuthManager.Instance == null) return;

        string oldPass = changeCurrentPassword != null ? changeCurrentPassword.text : "";
        string newPass = changeNewPassword     != null ? changeNewPassword.text     : "";
        string confirmNewPass = changeConfirmNewPassword != null ? changeConfirmNewPassword.text : "";

        if (newPass != confirmNewPass)
        {
            if (changePassNofiText != null)
            {
                changePassNofiText.color = Color.red;
                changePassNofiText.text  = "Mật khẩu mới xác nhận không khớp!";
            }
            return;
        }

        SetSelectables(changePassSelectables, false);

        var (success, error) = await AuthManager.Instance.ChangePasswordAsync(oldPass, newPass);

        SetSelectables(changePassSelectables, true);

        if (success)
        {
            if (changePassNofiText != null)
            {
                changePassNofiText.color = Color.green;
                changePassNofiText.text  = "Đổi mật khẩu thành công!";
            }
            Invoke(nameof(ShowWelcomePanel), 1.5f);
        }
        else
        {
            if (changePassNofiText != null)
            {
                changePassNofiText.color = Color.red;
                changePassNofiText.text  = error;
            }
        }
    }

    private async void OnForgotClicked()
    {
        if (AuthManager.Instance == null) return;

        string email = forgotEmail != null ? forgotEmail.text : "";

        SetSelectables(forgotSelectables, false);

        var (success, error) = await AuthManager.Instance.ForgotPasswordAsync(email);

        SetSelectables(forgotSelectables, true);

        if (success)
        {
            if (forgotNofiText != null)
            {
                forgotNofiText.color = Color.green;
                forgotNofiText.text  = "Đã gửi link đặt lại mật khẩu vào email của bạn.\nVui lòng kiểm tra hộp thư.";
            }
        }
        else
        {
            if (forgotNofiText != null)
            {
                forgotNofiText.color = Color.red;
                forgotNofiText.text  = error;
            }
        }
    }

    private void OnLogoutClicked()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.Logout();
        }
        ShowPanel(loginPanel);
    }

    private void OnStartGameClicked()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(mainMenuSceneName, TransitionType.Fade);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Google & Facebook Login stub hooks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGoogleLoginClicked()
    {
        Debug.Log("[AccountUIController] Đăng nhập bằng Google được kích hoạt (Stub).");
        if (loginNofiText != null)
        {
            loginNofiText.color = new Color(1f, 0.6f, 0f); // Màu cam cảnh báo
            loginNofiText.text = "Tính năng đăng nhập Google đang được tích hợp!";
        }
    }

    private void OnFacebookLoginClicked()
    {
        Debug.Log("[AccountUIController] Đăng nhập bằng Facebook được kích hoạt (Stub).");
        if (loginNofiText != null)
        {
            loginNofiText.color = new Color(1f, 0.6f, 0f); // Màu cam cảnh báo
            loginNofiText.text = "Tính năng đăng nhập Facebook đang được tích hợp!";
        }
    }
}
