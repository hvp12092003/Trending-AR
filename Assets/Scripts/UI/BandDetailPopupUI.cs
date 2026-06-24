using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện Popup thông tin chi tiết của một ban nhạc.
/// Hiển thị thông tin 4 thành viên và cung cấp nút "Thử" để nhảy sang Scene AR cùng ban nhạc này.
/// </summary>
public class BandDetailPopupUI : MonoBehaviour
{
    public static BandDetailPopupUI Instance { get; private set; }

    [System.Serializable]
    public struct MemberDetailUI
    {
        [Tooltip("Ảnh đại diện thành viên")]
        public Image avatarImage;

        [Tooltip("Tên hiển thị thành viên")]
        public TextMeshProUGUI nameText;

        [Tooltip("Nhạc cụ/Audio đi kèm")]
        public TextMeshProUGUI audioText;
    }

    [Header("UI Panel Elements")]
    [Tooltip("Panel cha của Popup (dùng để ẩn/hiện hoặc tạo hiệu ứng)")]
    [SerializeField] private GameObject popupRoot;

    [Tooltip("CanvasGroup của Popup (dùng cho hiệu ứng Fade)")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Khung chứa chính để scale hoặc zoom nhẹ khi mở")]
    [SerializeField] private RectTransform popupBox;

    [Header("Band Information Elements")]
    [Tooltip("Text hiển thị tên ban nhạc")]
    [SerializeField] private TextMeshProUGUI bandNameText;

    [Tooltip("4 slot hiển thị chi tiết thành viên")]
    [SerializeField] private MemberDetailUI[] memberSlots = new MemberDetailUI[4];

    [Header("Interactive Buttons")]
    [Tooltip("Nút bấm 'Thử' (đưa ban nhạc vào scene AR)")]
    [SerializeField] private Button tryButton;

    [Tooltip("Nút đóng popup")]
    [SerializeField] private Button closeButton;

    [Header("AR Configuration")]
    [Tooltip("Tên Scene AR của ban nhạc")]
    [SerializeField] private string arSceneName = "Band Mode AR Scene";

    [Tooltip("Tên Scene Non-AR của ban nhạc")]
    [SerializeField] private string nonArSceneName = "Band Mode NonAR Scene";

    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 0.25f;

    // Dữ liệu ban nhạc hiện hành đang hiển thị trên popup
    private BandData _currentBand;
    private List<Sprite> _currentAvatars = new List<Sprite>();
    private string _currentBandName;

    private void Awake()
    {
        // Khởi tạo Singleton cho Popup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Đăng ký sự kiện click nút
        if (tryButton != null)
        {
            tryButton.onClick.AddListener(OnTryButtonClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        // Tự động ẩn popup khi bắt đầu
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Hiển thị popup và nạp dữ liệu chi tiết của ban nhạc.
    /// </summary>
    /// <param name="band">Dữ liệu ban nhạc.</param>
    /// <param name="avatars">Danh sách ảnh đại diện.</param>
    /// <param name="bandName">Tên hiển thị ban nhạc.</param>
    public void Show(BandData band, List<Sprite> avatars, string bandName)
    {
        _currentBand = band;
        _currentBandName = bandName;
        _currentAvatars.Clear();
        if (avatars != null)
        {
            _currentAvatars.AddRange(avatars);
        }

        if (_currentBand == null) return;

        // Cập nhật tên ban nhạc
        if (bandNameText != null)
        {
            bandNameText.text = bandName;
        }

        // Cập nhật thông tin chi tiết từng thành viên
        for (int i = 0; i < memberSlots.Length; i++)
        {
            if (i < _currentBand.casts.Count)
            {
                CastData cast = _currentBand.casts[i];

                // Cập nhật Tên
                if (memberSlots[i].nameText != null)
                {
                    memberSlots[i].nameText.text = cast != null ? cast.name : "None";
                }

                // Cập nhật Nhạc cụ/Audio
                if (memberSlots[i].audioText != null)
                {
                    memberSlots[i].audioText.text = (cast != null && !string.IsNullOrEmpty(cast.audioId)) ? "Audio: " + cast.audioId : "No Audio";
                }

                // Cập nhật Avatar Sprite
                if (memberSlots[i].avatarImage != null)
                {
                    Sprite memberAvatar = (avatars != null && i < avatars.Count) ? avatars[i] : null;
                    if (memberAvatar != null)
                    {
                        memberSlots[i].avatarImage.sprite = memberAvatar;
                        memberSlots[i].avatarImage.enabled = true;
                    }
                    else
                    {
                        memberSlots[i].avatarImage.enabled = false;
                    }
                }
            }
            else
            {
                // Slot trống nếu ban nhạc có ít hơn 4 cast
                if (memberSlots[i].nameText != null) memberSlots[i].nameText.text = "Empty";
                if (memberSlots[i].audioText != null) memberSlots[i].audioText.text = "-";
                if (memberSlots[i].avatarImage != null) memberSlots[i].avatarImage.enabled = false;
            }
        }

        // Bật panel và chạy hiệu ứng mượt mà (Fade & Scale Zoom)
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            
            // Diệt các animation đang chạy dở trước đó
            if (canvasGroup != null) canvasGroup.DOKill();
            if (popupBox != null) popupBox.DOKill();

            // Hiệu ứng Fade alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            }

            // Hiệu ứng Scale zoom nhẹ từ 0.9 lên 1.0
            if (popupBox != null)
            {
                popupBox.localScale = Vector3.one * 0.9f;
                popupBox.DOScale(1.0f, fadeDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }
    }

    /// <summary>
    /// Ẩn popup thông tin chi tiết.
    /// </summary>
    public void Hide()
    {
        if (popupRoot == null || !popupRoot.activeSelf) return;

        // Chạy hiệu ứng biến mất (Fade out & Scale down)
        if (canvasGroup != null) canvasGroup.DOKill();
        if (popupBox != null) popupBox.DOKill();

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        }

        if (popupBox != null)
        {
            popupBox.DOScale(0.9f, fadeDuration).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => popupRoot.SetActive(false));
        }
        else
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Xử lý khi nhấn nút Thử (Try).
    /// Lưu ban nhạc được chọn vào Selection Manager toàn cục và tải Scene AR/Non-AR tương ứng.
    /// </summary>
    private void OnTryButtonClicked()
    {
        if (_currentBand == null)
        {
            Debug.LogError("[BandDetailPopupUI] Không có dữ liệu ban nhạc để thử nghiệm!");
            return;
        }

        Debug.Log($"[BandDetailPopupUI] Đang thử ban nhạc '{_currentBandName}'. Lưu dữ liệu và chuyển cảnh AR...");

        // 1. Lưu trữ ban nhạc đang chọn vào BandSelectionManager
        BandSelectionManager.SelectedBand = _currentBand;
        BandSelectionManager.SelectedBandAvatars = _currentAvatars;
        BandSelectionManager.SelectedBandName = _currentBandName;

        // 2. Ẩn popup
        Hide();

        // 3. Bắt đầu Coroutine kiểm tra ARCore và tải Scene tương ứng
        StartCoroutine(CheckARCoreAndTransition());
    }

    private IEnumerator CheckARCoreAndTransition()
    {
        // Kiểm tra khả năng hỗ trợ ARCore của thiết bị
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        // Tạm thời vô hiệu hóa AR theo yêu cầu, luôn định tuyến sang Non-AR
        bool isSupported = false;

        string targetScene = isSupported ? arSceneName : nonArSceneName;
        Debug.Log($"[BandDetailPopupUI] ARCore Routing temporarily disabled. Loading Non-AR scene: {targetScene}");

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(targetScene, TransitionType.Fade);
        }
        else
        {
            Debug.LogWarning("[BandDetailPopupUI] Không tìm thấy SceneTransitionManager. Thực hiện load trực tiếp.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
