using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;

/// <summary>
/// Quản lý giao diện và logic chuyển đổi Panel, tải Scene ở màn hình Menu chính (Main Menu).
/// Kết nối với MainMenuDataManager để cập nhật dữ liệu từ Firebase Firestore lên ScrollViews.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Bottom Bar Navigation")]
    [SerializeField] private BottomBarController bottomBarController;



    [Header("Studio Panel")]
    [SerializeField] private UiStudioController studioController;

    [Header("Leaderboard Panel ScrollRect")]
    [SerializeField] private Transform leaderboardContent;
    [SerializeField] private GameObject leaderboardPrefab;

    [Header("UI Scene Loading Overlay")]
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Profile Panel")]
    [SerializeField] private Button profileButton;
    [SerializeField] private ProfilePanelController profilePanel;
    [SerializeField] private TextMeshProUGUI userName;
    [SerializeField] private Image mainAvatarImage;

    [Header("Permission Panel")]
    [SerializeField] private PermissionPanelController permissionPanel;

    private async void Start()
    {
        // 1. Reset trạng thái loading overlay
        if (loadingOverlay != null) 
            loadingOverlay.SetActive(false);

        // Reset profile panel state
        if (profilePanel != null)
        {
            profilePanel.gameObject.SetActive(false);
            profilePanel.OnPanelClosed += OnProfilePanelClosedCallback;
        }

        // Register profile button events
        if (profileButton != null)
        {
            profileButton.onClick.AddListener(OnProfileButtonClicked);
        }

        // 2. Đăng ký sự kiện Bottom Bar
        if (bottomBarController != null)
        {
            bottomBarController.onTabSelected.AddListener(OnTabSelected);
        }

        // 3. Kiểm tra quyền truy cập khi vào game
        if (permissionPanel != null)
        {
            permissionPanel.OnPermissionsGranted += OnPermissionsGrantedCallback;

            if (!permissionPanel.AreAllPermissionsGranted())
            {
                // Nếu chưa đủ quyền, tạm thời ẩn Bottom Bar và hiển thị Panel yêu cầu quyền
                if (bottomBarController != null)
                {
                    bottomBarController.SetVisible(false);
                }
                permissionPanel.OpenPanel();
            }
            else
            {
                // Đã đủ quyền, tiếp tục kích hoạt giao diện bình thường
                OnPermissionsGrantedCallback();
            }
        }
        else
        {
            if (bottomBarController != null)
            {
                bottomBarController.SetVisible(true);
            }
        }

        if (mainAvatarImage != null)
        {
            _defaultAvatarSprite = mainAvatarImage.sprite;
        }

        ValidateAndCleanAvatarCache();

        // Cập nhật tên hiển thị người dùng ban đầu và avatar
        UpdateUserNameUI();
        UpdateUserAvatarUI();
        SyncUserAvatarFromFirestore();

        // Đăng ký sự kiện thay đổi avatar
        ProfilePanelController.OnAvatarChanged += UpdateUserAvatarUI;

        // 4. Đảm bảo người dùng hiện tại đã có document cấu hình trong Firestore
        if (MainMenuDataManager.Instance != null)
        {
            await MainMenuDataManager.Instance.EnsureUserDocumentExistsAsync();
            // Cập nhật lại sau khi đảm bảo thông tin Firestore tồn tại
            UpdateUserNameUI();
        }
    }

    private void OnPermissionsGrantedCallback()
    {
        Debug.Log("[MainMenuController] Cấp đủ quyền Camera & Microphone. Hiển thị thanh Bottom Bar chính.");
        if (bottomBarController != null)
        {
            bottomBarController.SetVisible(true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý sự kiện khi chuyển đổi Tab từ BottomBarController
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTabSelected(int index)
    {
        // Tải dữ liệu tương ứng khi tab tương ứng được hiển thị
        switch (index)
        {
            case 1: // Studio Panel
                if (studioController != null)
                {
                    studioController.RefreshAll();
                }
                break;
            case 2: // Leaderboard Panel
                LoadLeaderboardData();
                break;
        }
    }



    // ─────────────────────────────────────────────────────────────────────────
    // Nạp dữ liệu Bảng xếp hạng
    // ─────────────────────────────────────────────────────────────────────────

    private async void LoadLeaderboardData()
    {
        if (MainMenuDataManager.Instance == null) return;

        ClearChildren(leaderboardContent);

        string currentUserId = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId ?? "";

        var leaderboard = await MainMenuDataManager.Instance.GetLeaderboardAsync();
        if (leaderboardPrefab != null && leaderboardContent != null)
        {
            int rank = 1;
            foreach (var userEntry in leaderboard)
            {
                var itemObj = Instantiate(leaderboardPrefab, leaderboardContent);
                var itemUI = itemObj.GetComponent<LeaderboardItemUI>();
                if (itemUI != null)
                {
                    bool isCurrentUser = userEntry.userId == currentUserId;
                    itemUI.Setup(userEntry, rank, isCurrentUser);
                }
                rank++;
            }
        }
    }



    // ─────────────────────────────────────────────────────────────────────────
    // Hàm phụ trợ dọn dẹp phần tử UI
    // ─────────────────────────────────────────────────────────────────────────

    private void ClearChildren(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý sự kiện Profile Panel
    // ─────────────────────────────────────────────────────────────────────────

    private void OnProfileButtonClicked()
    {
        if (profilePanel == null) return;

        // Ẩn bottom bar khi mở Profile
        if (bottomBarController != null)
        {
            bottomBarController.SetVisible(false);
        }

        profilePanel.OpenPanel();
    }


    private void OnProfilePanelClosedCallback()
    {
        // Hiển thị lại bottom bar khi đóng Profile
        if (bottomBarController != null)
        {
            bottomBarController.SetVisible(true);
        }

        // Cập nhật lại userName khi đóng panel Profile
        UpdateUserNameUI();
    }

    /// <summary>
    /// Cập nhật tên hiển thị của người dùng ở cạnh Avatar trên Menu chính.
    /// </summary>
    private void UpdateUserNameUI()
    {
        string userDisplayName = "User";
        if (AuthManager.Instance != null)
        {
            userDisplayName = AuthManager.Instance.GetLoggedInUser();
        }
        else if (Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
            userDisplayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.Email;
        }

        if (userName != null)
        {
            userName.text = userDisplayName;
        }
    }

    private Sprite _dynamicAvatarSprite;
    private Sprite _defaultAvatarSprite;

    private string GetLocalAvatarPath()
    {
        return Path.Combine(Application.persistentDataPath, "avatar_cache.jpg");
    }

    private void ValidateAndCleanAvatarCache()
    {
        string currentUserId = "";
        if (Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            currentUserId = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        }

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
                    Debug.LogWarning($"[MainMenuController] Lỗi xóa cache avatar cũ: {ex.Message}");
                }
            }
            PlayerPrefs.SetString("CachedAvatarUserId", currentUserId);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Tải ảnh đại diện từ cache cục bộ và hiển thị ở Menu chính.
    /// </summary>
    private void UpdateUserAvatarUI()
    {
        if (mainAvatarImage == null) return;

        string localPath = GetLocalAvatarPath();
        if (File.Exists(localPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(localPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    // Dọn dẹp sprite động cũ để tránh rò rỉ bộ nhớ (không hủy tài liệu gốc của dự án)
                    if (_dynamicAvatarSprite != null)
                    {
                        Destroy(_dynamicAvatarSprite);
                    }

                    _dynamicAvatarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    mainAvatarImage.sprite = _dynamicAvatarSprite;
                    mainAvatarImage.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MainMenuController] Lỗi load avatar từ cache cục bộ: {ex.Message}");
            }
        }
        else
        {
            // Reset về default nếu không có cache của user hiện tại
            if (_dynamicAvatarSprite != null)
            {
                Destroy(_dynamicAvatarSprite);
            }
            mainAvatarImage.sprite = _defaultAvatarSprite;
        }
    }

    /// <summary>
    /// Đồng bộ avatar từ Firestore về máy khi khởi động Main Menu.
    /// </summary>
    private async void SyncUserAvatarFromFirestore()
    {
        if (MainMenuDataManager.Instance == null) return;

        // Chỉ đồng bộ khi thiết bị đang trực tuyến
        if (SceneTransitionManager.Instance != null)
        {
            bool hasInternet = await SceneTransitionManager.Instance.CheckInternetConnectionAsync();
            if (!hasInternet)
            {
                Debug.Log("[MainMenuController] Thiết bị ngoại tuyến, sử dụng avatar từ cache cục bộ.");
                return;
            }
        }

        string base64 = await MainMenuDataManager.Instance.GetUserAvatarBase64Async();
        if (!string.IsNullOrEmpty(base64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                string localPath = GetLocalAvatarPath();

                if (File.Exists(localPath))
                {
                    byte[] localBytes = File.ReadAllBytes(localPath);
                    if (AreByteArraysEqual(bytes, localBytes))
                    {
                        return;
                    }
                }

                File.WriteAllBytes(localPath, bytes);
                UpdateUserAvatarUI();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MainMenuController] Lỗi đồng bộ avatar từ Firestore: {ex.Message}");
            }
        }
    }

    private bool AreByteArraysEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private void OnDestroy()
    {
        if (profilePanel != null)
        {
            profilePanel.OnPanelClosed -= OnProfilePanelClosedCallback;
        }

        if (permissionPanel != null)
        {
            permissionPanel.OnPermissionsGranted -= OnPermissionsGrantedCallback;
        }

        ProfilePanelController.OnAvatarChanged -= UpdateUserAvatarUI;
    }
}
