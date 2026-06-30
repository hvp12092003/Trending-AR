using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using TMPro;

/// <summary>
/// Quản lý giao diện Hub Panel và xử lý chuyển đổi Scene cho 3 chế độ chơi:
/// - AR BAND (Band AR / Band Non AR)
/// - AR CUSTOME (costom AR / costom Non AR)
/// - AR MINIGAMES (Mini Game Mode Scene)
/// </summary>
public class HubPanelController : MonoBehaviour
{
    [Header("Game Mode Buttons")]
    [Tooltip("Nút bấm chọn chế độ AR BAND")]
    [SerializeField] private Button arBandButton;

    [Tooltip("Nút bấm chọn chế độ AR CUSTOME")]
    [SerializeField] private Button arCustomeButton;

    [Tooltip("Nút bấm chọn chế độ AR MINIGAMES")]
    [SerializeField] private Button arMiniGamesButton;

    [Header("Daily Challenge")]
    [Tooltip("Nút bấm Daily Challenge")]
    [SerializeField] private Button dailyChallengeButton;

    [Tooltip("Text TMP hiển thị thông báo Daily Challenge")]
    [SerializeField] private TextMeshProUGUI textTMPNotifi;

    [Header("Scene Configuration")]
    [Tooltip("Tên Scene Custom AR")]
    [SerializeField] private string customArSceneName = "Custome AR Scene";

    [Tooltip("Tên Scene Custom Non-AR")]
    [SerializeField] private string customNonArSceneName = "Custome NonAR Scene";

    [Tooltip("Tên Scene Band AR")]
    [SerializeField] private string bandArSceneName = "Band Mode AR Scene";

    [Tooltip("Tên Scene Band Non-AR")]
    [SerializeField] private string bandNonArSceneName = "Band Mode NonAR Scene";

    [Tooltip("Tên Scene của chế độ AR MINIGAMES")]
    [SerializeField] private string arMiniGamesSceneName = "Mini Game Mode Scene";

    private const string ComingSoonMessage = "Comming soon";
    private const float NotificationVisibleSeconds = 3f;

    private Coroutine notificationCoroutine;

    private void Start()
    {
        HideNotification();

        // 1. Đăng ký sự kiện click cho các nút bấm chuyển Scene
        if (arBandButton != null)
        {
            arBandButton.onClick.AddListener(OnBandButtonClicked);
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arBandButton chưa được gán trong Inspector.");
        }

        if (arCustomeButton != null)
        {
            arCustomeButton.onClick.AddListener(OnCustomButtonClicked);
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arCustomeButton chưa được gán trong Inspector.");
        }

        if (arMiniGamesButton != null)
        {
            arMiniGamesButton.onClick.AddListener(OnMiniGamesButtonClicked);
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arMiniGamesButton chưa được gán trong Inspector.");
        }

        if (dailyChallengeButton != null)
        {
            dailyChallengeButton.onClick.AddListener(OnDailyChallengeButtonClicked);
        }
        else
        {
            Debug.LogWarning("[HubPanelController] dailyChallengeButton chưa được gán trong Inspector.");
        }
    }

    private void OnCustomButtonClicked()
    {
        StartCoroutine(CheckARAndLoadScene(customArSceneName, customNonArSceneName));
    }

    private void OnBandButtonClicked()
    {
        // Reset selectedBandData để Scene chơi nhạc tự động mở Popup chọn ban nhạc mới
        if (MainMenuDataManager.Instance != null)
        {
            MainMenuDataManager.Instance.selectedBandData = null;
        }

        BandSelectionManager.ClearSelection();

        StartCoroutine(CheckARAndLoadScene(bandArSceneName, bandNonArSceneName));
    }

    private void OnMiniGamesButtonClicked()
    {
        ShowTemporaryNotification(ComingSoonMessage);
    }

    private void OnDailyChallengeButtonClicked()
    {
        ShowTemporaryNotification(ComingSoonMessage);
    }

    private void ShowTemporaryNotification(string message)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        if (textTMPNotifi == null)
        {
            Debug.LogWarning("[HubPanelController] textTMPNotifi chưa được gán trong Inspector.");
            return;
        }

        textTMPNotifi.text = message;
        textTMPNotifi.gameObject.SetActive(true);
        notificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(NotificationVisibleSeconds);
        HideNotification();
        notificationCoroutine = null;
    }

    private void HideNotification()
    {
        if (textTMPNotifi != null)
        {
            textTMPNotifi.text = string.Empty;
            textTMPNotifi.gameObject.SetActive(false);
        }
    }

    private IEnumerator CheckARAndLoadScene(string arScene, string nonArScene)
    {
        // Gọi API ARFoundation để kiểm tra khả năng tương thích của thiết bị
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        // Tạm thời vô hiệu hóa AR theo yêu cầu, luôn định tuyến sang Non-AR
        bool isSupported = false;

        string targetScene = isSupported ? arScene : nonArScene;
        Debug.Log($"[HubPanelController] ARCore Routing temporarily disabled. Loading Non-AR scene: {targetScene}");
        LoadGameModeScene(targetScene);
    }

    /// <summary>
    /// Tải Scene bất đồng bộ sử dụng SceneTransitionManager toàn cục.
    /// Có fallback tự động load trực tiếp nếu không qua màn khởi đầu (Bootstrap).
    /// </summary>
    /// <param name="sceneName">Tên Scene cần chuyển tới</param>
    private void LoadGameModeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[HubPanelController] Tên Scene chuyển tới đang để trống!");
            return;
        }

        if (SceneTransitionManager.Instance != null)
        {
            // Chuyển scene sử dụng hệ thống chuyển cảnh mượt mà toàn cục
            SceneTransitionManager.Instance.TransitionToScene(sceneName, TransitionType.Fade);
        }
        else
        {
            Debug.LogWarning("[HubPanelController] Không tìm thấy SceneTransitionManager. Tải trực tiếp Scene (Chế độ test editor).");
            SceneManager.LoadScene(sceneName);
        }
    }
}
