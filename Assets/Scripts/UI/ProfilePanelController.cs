using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý giao diện và chức năng của Profile Panel, bao gồm hiển thị thông tin người dùng
/// (tên, avatar ký tự đầu, điểm số từ Firestore), đổi mật khẩu (ChangePassPanel), và đăng xuất.
/// </summary>
public class ProfilePanelController : MonoBehaviour
{
    [Header("Main Profile UI Elements")]
    [SerializeField] private TextMeshProUGUI userNameText;
    [SerializeField] private TMP_InputField userNameInputField;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button backButton;
    [SerializeField] private Button changeAvatarBtn;
    [SerializeField] private Button changePasswordBtn;
    [SerializeField] private Button helpCenterBtn;
    [SerializeField] private Button aboutUsBtn;
    [SerializeField] private Button logoutBtn;
    [SerializeField] private TextMeshProUGUI mainNofiText;

    [Header("Change Password Sub-Panel UI")]
    [SerializeField] private GameObject changePassPanel;
    [SerializeField] private TMP_InputField currentPasswordInput;
    [SerializeField] private TMP_InputField newPasswordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private Button changePassSubmitBtn;
    [SerializeField] private Button changePassBackBtn;
    [SerializeField] private TextMeshProUGUI changePassNofiText;

    [Header("Navigation Settings")]
    [SerializeField] private string accountSceneName = "Account Scene";
    [SerializeField] private float transitionDuration = 0.25f;

    // Sự kiện thông báo khi avatar thay đổi
    public static event Action OnAvatarChanged;

    // Sự kiện thông báo khi đóng Panel để MainMenuController hiển thị lại Bottom Bar
    public event Action OnPanelClosed;

    private CanvasGroup _canvasGroup;
    private Tween _nofiTween;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (avatarImage != null)
        {
            _defaultAvatarSprite = avatarImage.sprite;
        }
    }

    private void Start()
    {
        // 1. Đăng ký sự kiện Click cho các Button chính
        if (backButton != null)
            backButton.onClick.AddListener(ClosePanel);
        
        if (changeAvatarBtn != null)
            changeAvatarBtn.onClick.AddListener(OnChangeAvatarClicked);

        if (changePasswordBtn != null)
            changePasswordBtn.onClick.AddListener(OpenChangePasswordPanel);

        if (helpCenterBtn != null)
            helpCenterBtn.onClick.AddListener(() => ShowMainNotification("Trung tâm hỗ trợ sẽ sớm ra mắt!"));

        if (aboutUsBtn != null)
            aboutUsBtn.onClick.AddListener(() => ShowMainNotification("Dự án Trending AR - Phiên bản 1.0"));

        if (logoutBtn != null)
            logoutBtn.onClick.AddListener(OnLogoutClicked);

        // 2. Đăng ký sự kiện cho Sub-panel đổi mật khẩu
        if (changePassSubmitBtn != null)
            changePassSubmitBtn.onClick.AddListener(OnChangePasswordSubmit);

        if (changePassBackBtn != null)
            changePassBackBtn.onClick.AddListener(CloseChangePasswordPanel);

        // Đảm bảo ban đầu ẩn Sub-panel đổi mật khẩu và xóa text thông báo
        if (changePassPanel != null)
            changePassPanel.SetActive(false);

        // Tự động tìm kiếm userNameInputField nếu chưa được gán
        if (userNameInputField == null)
        {
            Transform inputFieldTrans = transform.Find("UserNameInputField");
            if (inputFieldTrans == null) inputFieldTrans = transform.Find("NameInputField");
            if (inputFieldTrans != null)
            {
                userNameInputField = inputFieldTrans.GetComponent<TMP_InputField>();
            }
        }

        if (userNameInputField != null)
        {
            userNameInputField.onSubmit.AddListener(OnRenameSubmit);
            userNameInputField.onEndEdit.AddListener(OnRenameEndEdit);
        }

        ClearNotifications();
    }

    /// <summary>
    /// Hiển thị Profile Panel với hiệu ứng Tween mượt mà.
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        if (changePassPanel != null)
            changePassPanel.SetActive(false);

        ClearNotifications();
        RefreshProfileData();

        // Animate in
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        
        _canvasGroup.DOKill();
        transform.DOKill();

        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.95f;

        _canvasGroup.DOFade(1f, transitionDuration).SetEase(Ease.OutQuad);
        transform.DOScale(1f, transitionDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Đóng Profile Panel với hiệu ứng Tween.
    /// </summary>
    public void ClosePanel()
    {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _canvasGroup.DOKill();
        transform.DOKill();

        _canvasGroup.DOFade(0f, transitionDuration).SetEase(Ease.InQuad);
        transform.DOScale(0.95f, transitionDuration).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnPanelClosed?.Invoke();
            });
    }

    /// <summary>
    /// Cập nhật thông tin người dùng từ Auth.
    /// </summary>
    public void RefreshProfileData()
    {
        // Lấy thông tin cơ bản từ Auth
        string userName = GetCurrentUserName();

        if (userNameText != null)
            userNameText.text = userName;

        if (userNameInputField != null)
            userNameInputField.text = userName;

        // Kiểm tra và dọn dẹp cache của tài khoản cũ
        ValidateAndCleanAvatarCache();

        // Tải ảnh đại diện
        LoadLocalAvatar();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý logic Đổi Mật Khẩu (Change Password Sub-Panel)
    // ─────────────────────────────────────────────────────────────────────────

    private void OpenChangePasswordPanel()
    {
        if (changePassPanel == null) return;

        // Reset các trường nhập liệu và thông báo lỗi
        if (currentPasswordInput != null) currentPasswordInput.text = "";
        if (newPasswordInput != null) newPasswordInput.text = "";
        if (confirmPasswordInput != null) confirmPasswordInput.text = "";
        if (changePassNofiText != null) changePassNofiText.text = "";

        changePassPanel.SetActive(true);

        // Hiệu ứng scale nhẹ khi mở sub-panel
        changePassPanel.transform.DOKill();
        changePassPanel.transform.localScale = Vector3.one * 0.9f;
        changePassPanel.transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
    }

    private void CloseChangePasswordPanel()
    {
        if (changePassPanel == null) return;

        changePassPanel.transform.DOKill();
        changePassPanel.transform.DOScale(0.9f, 0.15f).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                changePassPanel.SetActive(false);
            });
    }

    private async void OnChangePasswordSubmit()
    {
        if (AuthManager.Instance == null)
        {
            ShowChangePassNotification("Hệ thống xác thực không khả dụng!", Color.red);
            return;
        }

        string currentPass = currentPasswordInput != null ? currentPasswordInput.text : "";
        string newPass = newPasswordInput != null ? newPasswordInput.text : "";
        string confirmPass = confirmPasswordInput != null ? confirmPasswordInput.text : "";

        if (string.IsNullOrEmpty(currentPass) || string.IsNullOrEmpty(newPass) || string.IsNullOrEmpty(confirmPass))
        {
            ShowChangePassNotification("Vui lòng điền đầy đủ tất cả các trường!", Color.red);
            return;
        }

        if (newPass != confirmPass)
        {
            ShowChangePassNotification("Mật khẩu mới xác nhận không khớp!", Color.red);
            return;
        }

        if (newPass.Length < 6)
        {
            ShowChangePassNotification("Mật khẩu mới phải có tối thiểu 6 ký tự!", Color.red);
            return;
        }

        // Vô hiệu hóa nút và input trong lúc xử lý
        SetChangePassInteractable(false);
        ShowChangePassNotification("Đang xử lý đổi mật khẩu...", Color.yellow);

        var (success, error) = await AuthManager.Instance.ChangePasswordAsync(currentPass, newPass);

        SetChangePassInteractable(true);

        if (success)
        {
            ShowChangePassNotification("Đổi mật khẩu thành công!", Color.green);
            // Quay lại màn hình chính sau 1.5s
            Invoke(nameof(CloseChangePasswordPanel), 1.5f);
        }
        else
        {
            ShowChangePassNotification(error, Color.red);
        }
    }

    private void SetChangePassInteractable(bool interactable)
    {
        if (currentPasswordInput != null) currentPasswordInput.interactable = interactable;
        if (newPasswordInput != null) newPasswordInput.interactable = interactable;
        if (confirmPasswordInput != null) confirmPasswordInput.interactable = interactable;
        if (changePassSubmitBtn != null) changePassSubmitBtn.interactable = interactable;
        if (changePassBackBtn != null) changePassBackBtn.interactable = interactable;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý Đăng Xuất (Logout)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnLogoutClicked()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.Logout();
        }

        ShowMainNotification("Đang đăng xuất...");

        // Chuyển về màn hình đăng nhập
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(accountSceneName, TransitionType.Fade);
        }
        else
        {
            SceneManager.LoadScene(accountSceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper Methods hiển thị thông báo
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowMainNotification(string message)
    {
        if (mainNofiText == null) return;

        mainNofiText.text = message;
        mainNofiText.color = Color.white;

        // Reset tween và fade out sau 2.5 giây
        _nofiTween?.Kill();
        mainNofiText.alpha = 1f;
        _nofiTween = mainNofiText.DOFade(0f, 0.5f).SetDelay(2.0f);
    }

    private void ShowChangePassNotification(string message, Color color)
    {
        if (changePassNofiText == null) return;

        changePassNofiText.text = message;
        changePassNofiText.color = color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý Thay đổi Avatar
    // ─────────────────────────────────────────────────────────────────────────

    private Sprite _defaultAvatarSprite;

    private string GetLocalAvatarPath() => Path.Combine(Application.persistentDataPath, "avatar_cache.jpg");

    private void ValidateAndCleanAvatarCache()
    {
        string currentUserId = "offline_user_id";

        string cachedUserId = PlayerPrefs.GetString("CachedAvatarUserId", "");
        if (cachedUserId != currentUserId)
        {
            string localPath = GetLocalAvatarPath();
            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ProfilePanelController] Lỗi xóa cache avatar cũ: {ex.Message}");
                }
            }
            PlayerPrefs.SetString("CachedAvatarUserId", currentUserId);
            PlayerPrefs.Save();
        }
    }

    private void LoadLocalAvatar()
    {
        string localPath = GetLocalAvatarPath();
        if (File.Exists(localPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(localPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    UpdateAvatarUIFromTexture(tex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfilePanelController] Lỗi load avatar cache: {ex.Message}");
            }
        }
        else
        {
            // Reset về default nếu không có cache của user hiện tại
            if (_dynamicAvatarSprite != null)
            {
                Destroy(_dynamicAvatarSprite);
            }
            avatarImage.sprite = _defaultAvatarSprite;
        }
    }


    private Sprite _dynamicAvatarSprite;

    private void UpdateAvatarUIFromTexture(Texture2D texture)
    {
        if (avatarImage == null || texture == null) return;

        // Dọn dẹp sprite động cũ để tránh rò rỉ bộ nhớ (không hủy tài liệu gốc của dự án)
        if (_dynamicAvatarSprite != null)
        {
            Destroy(_dynamicAvatarSprite);
        }

        _dynamicAvatarSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        avatarImage.sprite = _dynamicAvatarSprite;
        avatarImage.enabled = true;
    }

    private void OnChangeAvatarClicked()
    {
        // NativeGallery sẽ tự động yêu cầu cấp quyền truy cập nếu cần
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                HandlePickedImage(path);
            }
        }, "Chọn ảnh đại diện", "image/*");
    }

    private void HandlePickedImage(string path)
    {
        // 1. Tải và tự động xoay ảnh dựa trên EXIF
        Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 256, markTextureNonReadable: false);
        if (texture == null)
        {
            ShowMainNotification("Không thể tải ảnh đã chọn.");
            return;
        }

        // 2. Nén thành JPG chất lượng 75%
        byte[] bytes = texture.EncodeToJPG(75);
        string localPath = GetLocalAvatarPath();
        try
        {
            File.WriteAllBytes(localPath, bytes);
            
            // Lưu userId vào PlayerPrefs của tệp cache mới này
            string currentUserId = "offline_user_id";
            PlayerPrefs.SetString("CachedAvatarUserId", currentUserId);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProfilePanelController] Lỗi lưu cache avatar: {ex.Message}");
        }

        // 3. Cập nhật UI lập tức
        UpdateAvatarUIFromTexture(texture);

        // Kích hoạt sự kiện đổi ảnh đại diện
        OnAvatarChanged?.Invoke();

        ShowMainNotification("Cập nhật ảnh đại diện thành công!");
    }

    private void ClearNotifications()
    {
        if (mainNofiText != null) mainNofiText.text = "";
        if (changePassNofiText != null) changePassNofiText.text = "";
    }

    private void OnRenameSubmit(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            ShowMainNotification("Tên người chơi không được để trống!");
            if (userNameInputField != null)
            {
                userNameInputField.text = GetCurrentUserName();
            }
            return;
        }

        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.UpdateOfflineUserName(newName);
        }
        else
        {
            PlayerPrefs.SetString("OfflineUserName", newName);
            PlayerPrefs.Save();
        }

        ShowMainNotification("Đổi tên thành công!");
        RefreshProfileData();
    }

    private void OnRenameEndEdit(string text)
    {
        if (userNameInputField != null)
        {
            userNameInputField.text = GetCurrentUserName();
        }
    }

    private string GetCurrentUserName()
    {
        string userName = "";
        if (AuthManager.Instance != null)
        {
            userName = AuthManager.Instance.GetLoggedInUser();
        }
        else
        {
            userName = PlayerPrefs.GetString("OfflineUserName", "");
            if (string.IsNullOrEmpty(userName) || userName == "Offline User")
            {
                System.DateTime now = System.DateTime.Now;
                string day = now.Day.ToString();
                string month = now.Month.ToString();
                string year = now.Year.ToString();
                string hour = now.Hour.ToString("D2");
                string minute = now.Minute.ToString("D2");
                string second = now.Second.ToString("D2");
                userName = $"Player{day}{month}{year}{hour}{minute}{second}";
                PlayerPrefs.SetString("OfflineUserName", userName);
                PlayerPrefs.Save();
            }
        }
        return userName;
    }
}
