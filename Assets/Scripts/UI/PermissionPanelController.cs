using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện yêu cầu cấp quyền truy cập Camera và Microphone.
/// Khi người chơi nhấn nút ALLOW, hệ thống tiến hành yêu cầu cấp quyền.
/// Trạng thái của từng quyền sẽ được cập nhật động và khi được cấp đủ cả 2 quyền,
/// panel sẽ tự động đóng bằng hiệu ứng DOTween và kích hoạt sự kiện để cho phép người chơi vào trong.
/// </summary>
public class PermissionPanelController : MonoBehaviour
{
    [Header("UI Canvas Elements")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button allowButton;

    [Header("Permission Status Texts")]
    [SerializeField] private TextMeshProUGUI cameraStatusText;
    [SerializeField] private TextMeshProUGUI microphoneStatusText;

    [Header("Status Visual Settings")]
    [SerializeField] [Tooltip("Text hiển thị khi chưa cấp quyền")] private string requiredText = "required";
    [SerializeField] [Tooltip("Text hiển thị khi đã cấp quyền")] private string grantedText = "granted";
    [SerializeField] [Tooltip("Màu sắc của text khi chưa cấp quyền")] private Color requiredColor = new Color(0.47f, 0.49f, 0.56f); // Màu xám tối sang trọng
    [SerializeField] [Tooltip("Màu sắc của text khi đã cấp quyền")] private Color grantedColor = new Color(0.3f, 0.85f, 0.4f);    // Màu xanh lá tươi mát
    [SerializeField] [Tooltip("Thời gian chuyển đổi hiệu ứng đóng/mở panel")] private float transitionDuration = 0.25f;

    /// <summary>
    /// Sự kiện được kích hoạt khi người dùng đã cấp đủ cả 2 quyền truy cập.
    /// </summary>
    public event Action OnPermissionsGranted;

    private bool m_IsRequesting = false;

#if UNITY_EDITOR
    // Biến giả lập trạng thái cấp quyền để kiểm thử trực tiếp trong Unity Editor
    private bool m_SimulatedCameraGranted = false;
    private bool m_SimulatedMicrophoneGranted = false;
#endif

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Start()
    {
        if (allowButton != null)
        {
            allowButton.onClick.AddListener(OnAllowButtonClicked);
        }

        // Cập nhật giao diện trạng thái ban đầu
        UpdatePermissionUI();

        // Nếu ngay lúc khởi động đã có đủ cả 2 quyền thì tự động ẩn panel và gọi sự kiện cho phép vào game
        if (AreAllPermissionsGranted())
        {
            gameObject.SetActive(false);
            OnPermissionsGranted?.Invoke();
        }
    }

    private void OnEnable()
    {
        // Cập nhật lại giao diện mỗi khi Panel được bật lên
        UpdatePermissionUI();
    }

    /// <summary>
    /// Kiểm tra xem cả hai quyền Camera và Microphone đã được cấp hay chưa.
    /// </summary>
    public bool AreAllPermissionsGranted()
    {
        return IsCameraPermissionGranted() && IsMicrophonePermissionGranted();
    }

    private bool IsCameraPermissionGranted()
    {
#if UNITY_EDITOR
        return m_SimulatedCameraGranted;
#elif UNITY_ANDROID
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#elif UNITY_IOS
        return Application.HasUserAuthorization(UserAuthorization.WebCam);
#else
        return true;
#endif
    }

    private bool IsMicrophonePermissionGranted()
    {
#if UNITY_EDITOR
        return m_SimulatedMicrophoneGranted;
#elif UNITY_ANDROID
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS
        return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
        return true;
#endif
    }

    /// <summary>
    /// Cập nhật hiển thị trạng thái text và màu sắc của các dòng yêu cầu quyền.
    /// </summary>
    public void UpdatePermissionUI()
    {
        UpdateStatusText(cameraStatusText, IsCameraPermissionGranted());
        UpdateStatusText(microphoneStatusText, IsMicrophonePermissionGranted());
    }

    private void UpdateStatusText(TextMeshProUGUI statusText, bool isGranted)
    {
        if (statusText == null) return;

        string targetText = isGranted ? grantedText : requiredText;
        Color targetColor = isGranted ? grantedColor : requiredColor;

        // Nếu trạng thái thay đổi so với hiển thị cũ, cập nhật kèm hiệu ứng scale nhẹ (micro-animation)
        if (statusText.text != targetText)
        {
            statusText.text = targetText;
            statusText.color = targetColor;

            statusText.transform.DOKill();
            statusText.transform.localScale = Vector3.one;
            // Tạo hiệu ứng nảy nhẹ khi chuyển đổi trạng thái thành công
            statusText.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0.2f), 0.35f, 6, 0.5f);
        }
        else
        {
            statusText.text = targetText;
            statusText.color = targetColor;
        }
    }

    private void OnAllowButtonClicked()
    {
        if (m_IsRequesting) return;
        StartCoroutine(RequestPermissionsCoroutine());
    }

    private IEnumerator RequestPermissionsCoroutine()
    {
        m_IsRequesting = true;
        if (allowButton != null) allowButton.interactable = false;

        // 1. Yêu cầu cấp quyền Camera
        if (!IsCameraPermissionGranted())
        {
#if UNITY_EDITOR
            // Giả lập popup xin quyền bằng cách chờ 1 giây rồi tự động đồng ý
            yield return new WaitForSeconds(1.0f);
            m_SimulatedCameraGranted = true;
#elif UNITY_ANDROID
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            float elapsed = 0f;
            // Chờ người dùng tương tác với popup (tối đa 15 giây)
            while (!IsCameraPermissionGranted() && elapsed < 15f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
#elif UNITY_IOS
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
            UpdatePermissionUI();
        }

        // Chờ 1 frame trước khi xin tiếp quyền thứ hai
        yield return null;

        // 2. Yêu cầu cấp quyền Microphone
        if (!IsMicrophonePermissionGranted())
        {
#if UNITY_EDITOR
            // Giả lập popup xin quyền bằng cách chờ 1 giây rồi tự động đồng ý
            yield return new WaitForSeconds(1.0f);
            m_SimulatedMicrophoneGranted = true;
#elif UNITY_ANDROID
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            float elapsed = 0f;
            // Chờ người dùng tương tác với popup (tối đa 15 giây)
            while (!IsMicrophonePermissionGranted() && elapsed < 15f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
#elif UNITY_IOS
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
            UpdatePermissionUI();
        }

        m_IsRequesting = false;
        if (allowButton != null) allowButton.interactable = true;

        // 3. Đóng panel nếu cả 2 quyền đều đã được cấp thành công
        if (AreAllPermissionsGranted())
        {
            ClosePanel();
        }
    }

    /// <summary>
    /// Hiển thị Permission Panel với hiệu ứng xuất hiện mượt mà.
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        UpdatePermissionUI();

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        canvasGroup.DOKill();
        transform.DOKill();

        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.95f;

        canvasGroup.DOFade(1f, transitionDuration).SetEase(Ease.OutQuad);
        transform.DOScale(1f, transitionDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Đóng Permission Panel với hiệu ứng biến mất mượt mà và gọi callback OnPermissionsGranted.
    /// </summary>
    public void ClosePanel()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        canvasGroup.DOKill();
        transform.DOKill();

        canvasGroup.DOFade(0f, transitionDuration).SetEase(Ease.InQuad);
        transform.DOScale(0.95f, transitionDuration).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnPermissionsGranted?.Invoke();
            });
    }
}
