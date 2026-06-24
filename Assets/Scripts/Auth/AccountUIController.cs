using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Quản lý việc hiển thị, ẩn các Panel UI trong Account Scene và kết nối với AuthManager.
/// </summary>
public class AccountUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signupPanel;
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

    [Header("Forgot Password Panel UI")]
    [SerializeField] private TMP_InputField forgotEmail;
    [SerializeField] private TextMeshProUGUI forgotNofiText;
    [SerializeField] private Button forgotSendButton;
    [SerializeField] private Button backToLoginFromForgotPassButton;

    [Header("Welcome Panel UI")]
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private Button startButton;

    [Header("Navigation Settings")]
    [SerializeField] private string mainMenuSceneName = "Main Menu Scene";

    private void Start()
    {
        // Gán sự kiện cho các Button
        if (loginButton != null)            loginButton.onClick.AddListener(OnLoginClicked);
        if (signUpButton != null)           signUpButton.onClick.AddListener(() => ShowPanel(signupPanel));
        if (goToForgotPassButton != null)   goToForgotPassButton.onClick.AddListener(() => ShowPanel(forgotPassPanel));
        if (googleButton != null)           googleButton.onClick.AddListener(OnGoogleLoginClicked);
        if (facebookButton != null)         facebookButton.onClick.AddListener(OnFacebookLoginClicked);

        if (signupButton != null)           signupButton.onClick.AddListener(OnRegisterClicked);
        if (goToLoginButton != null)        goToLoginButton.onClick.AddListener(() => ShowPanel(loginPanel));

        if (forgotSendButton != null)                  forgotSendButton.onClick.AddListener(OnForgotClicked);
        if (backToLoginFromForgotPassButton != null)   backToLoginFromForgotPassButton.onClick.AddListener(() => ShowPanel(loginPanel));

        if (startButton != null)            startButton.onClick.AddListener(OnStartGameClicked);

        // Offline mode: người dùng luôn được coi là đã đăng nhập
        ShowWelcomePanel();
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

        ClearErrors();

        if (previousPanel != null)
        {
            CanvasGroup prevCg = previousPanel.GetComponent<CanvasGroup>();
            if (prevCg == null) prevCg = previousPanel.AddComponent<CanvasGroup>();
            prevCg.interactable    = false;
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
            if (loginPanel      != null && loginPanel      != targetPanel) loginPanel.SetActive(false);
            if (signupPanel     != null && signupPanel     != targetPanel) signupPanel.SetActive(false);
            if (forgotPassPanel != null && forgotPassPanel != targetPanel) forgotPassPanel.SetActive(false);
            if (welcomePanel    != null && welcomePanel    != targetPanel) welcomePanel.SetActive(false);
        }

        targetPanel.SetActive(true);
        CanvasGroup targetCg = targetPanel.GetComponent<CanvasGroup>();
        if (targetCg == null) targetCg = targetPanel.AddComponent<CanvasGroup>();

        targetCg.interactable    = true;
        targetCg.blocksRaycasts = true;
        targetCg.alpha           = 0f;
        targetPanel.transform.localScale = Vector3.one * 0.95f;

        targetPanel.transform.DOKill();
        targetCg.DOKill();

        targetPanel.transform.DOScale(1f, panelTransitionDuration).SetEase(Ease.OutBack);
        targetCg.DOFade(1f, panelTransitionDuration).SetEase(Ease.OutQuad);
    }

    private void ClearErrors()
    {
        if (loginNofiText  != null) loginNofiText.text  = "";
        if (signupNofiText != null) signupNofiText.text = "";
        if (forgotNofiText != null) forgotNofiText.text = "";
    }

    private void ShowWelcomePanel()
    {
        ShowPanel(welcomePanel);
        if (welcomeText != null && AuthManager.Instance != null)
            welcomeText.text = $"Xin chào,\n<color=#FFD700>{AuthManager.Instance.GetLoggedInUser()}</color>";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Click handlers (offline – tất cả đều thành công tức thì)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnLoginClicked()
    {
        // Offline mode: đăng nhập ngay lập tức
        ShowWelcomePanel();
    }

    private void OnRegisterClicked()
    {
        string displayName = signupName != null ? signupName.text.Trim() : "";
        string password    = signupPassword        != null ? signupPassword.text        : "";
        string confirmPass = signupConfirmPassword  != null ? signupConfirmPassword.text : "";

        if (password != confirmPass)
        {
            if (signupNofiText != null)
            {
                signupNofiText.color = Color.red;
                signupNofiText.text  = "Mật khẩu nhập lại không trùng khớp!";
            }
            return;
        }

        // Lưu tên hiển thị nếu có
        if (!string.IsNullOrEmpty(displayName) && AuthManager.Instance != null)
            AuthManager.Instance.UpdateOfflineUserName(displayName);

        if (signupNofiText != null)
        {
            signupNofiText.color = Color.green;
            signupNofiText.text  = "Đăng ký tài khoản thành công!";
        }

        Invoke(nameof(GoToLogin), 1.5f);
    }

    private void GoToLogin() => ShowPanel(loginPanel);

    private void OnForgotClicked()
    {
        if (forgotNofiText != null)
        {
            forgotNofiText.color = Color.green;
            forgotNofiText.text  = "Đã gửi link đặt lại mật khẩu vào email của bạn.\nVui lòng kiểm tra hộp thư.";
        }
    }

    private void OnStartGameClicked()
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.TransitionToScene(mainMenuSceneName, TransitionType.Fade);
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Social Login stubs (chưa tích hợp)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGoogleLoginClicked()
    {
        if (loginNofiText != null)
        {
            loginNofiText.color = new Color(1f, 0.6f, 0f);
            loginNofiText.text  = "Tính năng đăng nhập Google đang được tích hợp!";
        }
    }

    private void OnFacebookLoginClicked()
    {
        if (loginNofiText != null)
        {
            loginNofiText.color = new Color(1f, 0.6f, 0f);
            loginNofiText.text  = "Tính năng đăng nhập Facebook đang được tích hợp!";
        }
    }
}
