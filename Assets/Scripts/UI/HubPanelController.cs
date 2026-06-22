using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;

/// <summary>
/// Quản lý giao diện Hub Panel và xử lý chuyển đổi Scene cho 3 chế độ chơi:
/// - AR BAND (Band Mode Scene)
/// - AR CUSTOME (Custome Scene)
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

    [Header("Scene Configuration")]
    [Tooltip("Tên Scene của chế độ AR BAND")]
    [SerializeField] private string arBandSceneName = "Band Mode Scene";

    [Tooltip("Tên Scene của chế độ AR CUSTOME")]
    [SerializeField] private string arCustomeSceneName = "Custome Scene";

    [Tooltip("Tên Scene của chế độ AR MINIGAMES")]
    [SerializeField] private string arMiniGamesSceneName = "Mini Game Mode Scene";

    [Header("UI Loading Transition Overlay")]
    [Tooltip("Giao diện phủ (Overlay) hiển thị khi đang tải Scene")]
    [SerializeField] private GameObject loadingOverlay;

    [Tooltip("Chữ hiển thị phần trăm hoặc trạng thái tải")]
    [SerializeField] private TextMeshProUGUI loadingText;

    [Tooltip("Thời gian hiệu ứng chuyển động mờ (Fade duration)")]
    [SerializeField] private float fadeDuration = 0.3f;

    private void Start()
    {
        // 1. Ẩn loading overlay khi bắt đầu
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(false);
        }

        // 2. Đăng ký sự kiện click cho các nút bấm chuyển Scene
        if (arBandButton != null)
        {
            arBandButton.onClick.AddListener(() => LoadGameModeScene(arBandSceneName));
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arBandButton chưa được gán trong Inspector.");
        }

        if (arCustomeButton != null)
        {
            arCustomeButton.onClick.AddListener(() => LoadGameModeScene(arCustomeSceneName));
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arCustomeButton chưa được gán trong Inspector.");
        }

        if (arMiniGamesButton != null)
        {
            arMiniGamesButton.onClick.AddListener(() => LoadGameModeScene(arMiniGamesSceneName));
        }
        else
        {
            Debug.LogWarning("[HubPanelController] arMiniGamesButton chưa được gán trong Inspector.");
        }
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
