using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện và logic của Popup Cài đặt (Setting).
/// Lưu trữ dữ liệu cấu hình offline qua PlayerPrefs và áp dụng trực tiếp khi click gạt toggle.
/// </summary>
public class PopupSetting : MonoBehaviour
{
    public event Action OnCloseStarted;

    [Header("UI Toggles")]
    [Tooltip("Thanh gạt Sound")]
    [SerializeField] private SliceToggle soundToggle;

    [Tooltip("Thanh gạt VFX")]
    [SerializeField] private SliceToggle vfxToggle;

    [Tooltip("Thanh gạt Vibration")]
    [SerializeField] private SliceToggle vibrationToggle;

    [Header("Close Buttons")]
    [Tooltip("Nút Exit lớn bên dưới popup")]
    [SerializeField] private Button exitButton;

    [Tooltip("Nền tối phía sau, click để đóng popup")]
    [SerializeField] private Button backgroundBlocker;

    // Các key lưu trữ trong PlayerPrefs
    public const string SOUND_KEY = "Setting_Sound";
    public const string VFX_KEY = "Setting_VFX";
    public const string VIBRATION_KEY = "Setting_Vibration";

    private bool _isOpening = false;

    private void Awake()
    {
        // Gán sự kiện cho các nút đóng
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(ClosePopup);
        }

        if (backgroundBlocker != null)
        {
            backgroundBlocker.onClick.RemoveAllListeners();
            backgroundBlocker.onClick.AddListener(ClosePopup);
        }

        // Lắng nghe sự thay đổi của các thanh gạt
        if (soundToggle != null)
        {
            soundToggle.OnValueChanged += OnSoundChanged;
        }

        if (vfxToggle != null)
        {
            vfxToggle.OnValueChanged += OnVfxChanged;
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.OnValueChanged += OnVibrationChanged;
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký sự kiện tránh rò rỉ bộ nhớ
        if (soundToggle != null)
        {
            soundToggle.OnValueChanged -= OnSoundChanged;
        }

        if (vfxToggle != null)
        {
            vfxToggle.OnValueChanged -= OnVfxChanged;
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.OnValueChanged -= OnVibrationChanged;
        }
    }

    /// <summary>
    /// Hiển thị Popup và khởi tạo trạng thái các toggle từ PlayerPrefs.
    /// </summary>
    public void OpenPopup()
    {
        if (_isOpening) return;
        _isOpening = true;

        gameObject.SetActive(true);

        // Load cài đặt hiện tại và hiển thị lên UI
        LoadSettings();

        // Hiệu ứng Fade In và Scale Up mượt mà sử dụng DOTween
        transform.localScale = Vector3.one * 0.8f;
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
        transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).OnComplete(() => {
            _isOpening = false;
        });
    }

    /// <summary>
    /// Đóng Popup cài đặt với hiệu ứng thu nhỏ và mờ dần.
    /// </summary>
    public void ClosePopup()
    {
        OnCloseStarted?.Invoke();
        _isOpening = false;

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
        transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() => {
            gameObject.SetActive(false);
        });
    }

    private void LoadSettings()
    {
        bool soundOn = PlayerPrefs.GetInt(SOUND_KEY, 1) == 1;
        bool vfxOn = PlayerPrefs.GetInt(VFX_KEY, 1) == 1;
        bool vibrationOn = PlayerPrefs.GetInt(VIBRATION_KEY, 1) == 1;

        if (soundToggle != null)
        {
            soundToggle.SetState(soundOn, false);
        }

        if (vfxToggle != null)
        {
            vfxToggle.SetState(vfxOn, false);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.SetState(vibrationOn, false);
        }
    }

    private void OnSoundChanged(bool isOn)
    {
        PlayerPrefs.SetInt(SOUND_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();

        // Áp dụng âm lượng hệ thống ngay lập tức
        AudioListener.volume = isOn ? 1f : 0f;
        Debug.Log($"[PopupSetting] Sound changed: {isOn}. AudioListener volume applied.");
    }

    private void OnVfxChanged(bool isOn)
    {
        PlayerPrefs.SetInt(VFX_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[PopupSetting] VFX changed: {isOn}. Saved to PlayerPrefs.");
    }

    private void OnVibrationChanged(bool isOn)
    {
        PlayerPrefs.SetInt(VIBRATION_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[PopupSetting] Vibration changed: {isOn}. Saved to PlayerPrefs.");

        // Rung nhẹ phản hồi cho người dùng nếu bật Vibration
        if (isOn)
        {
#if UNITY_ANDROID || UNITY_IOS
            try
            {
                Handheld.Vibrate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PopupSetting] Lỗi trigger rung: {ex.Message}");
            }
#else
            Debug.Log("[PopupSetting] Giả lập Rung trên nền tảng Standalone/Editor.");
#endif
        }
    }
}
