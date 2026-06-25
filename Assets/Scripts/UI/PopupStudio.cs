using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện và logic của Popup Studio sử dụng 7 button Cast cố định được thiết kế sẵn.
/// </summary>
public class PopupStudio : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Danh sách 7 button nhân vật cố định trong giao diện (đã gắn sẵn PopupCastButton)")]
    [SerializeField] private List<PopupCastButton> castButtons = new List<PopupCastButton>();

    [Tooltip("Nút Cancel để đóng popup không lưu")]
    [SerializeField] private Button cancelButton;

    [Tooltip("Nút Start để xác nhận lưu sau khi đã xóa bớt nhân vật")]
    [SerializeField] private Button startButton;

    [Tooltip("Nút/Panel nền tối hoặc blocker tàng hình phía sau để click ra ngoài tắt chế độ xóa")]
    [SerializeField] private Button backgroundBlocker;

    // Các sự kiện callback
    public event Action OnCancel;
    public event Action OnStart;

    private int _currentCharacterCount = 0;
    private bool _isOpening = false;

    private void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(ClosePopup);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (backgroundBlocker != null)
        {
            backgroundBlocker.onClick.RemoveAllListeners();
            backgroundBlocker.onClick.AddListener(CancelAllDeleteModes);
            backgroundBlocker.gameObject.SetActive(false); // Mặc định ẩn blocker
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký sự kiện tránh rò rỉ bộ nhớ
        if (castButtons != null)
        {
            foreach (var btn in castButtons)
            {
                if (btn != null)
                {
                    btn.OnDeleted -= OnCharacterDeleted;
                    btn.OnDeleteModeEntered -= OnButtonDeleteModeEntered;
                }
            }
        }
    }

    /// <summary>
    /// Hiển thị popup Studio và làm mới lưới danh sách.
    /// </summary>
    public void OpenPopup()
    {
        if (_isOpening) return;
        _isOpening = true;

        gameObject.SetActive(true);
        CancelAllDeleteModes(); // Đảm bảo reset trạng thái khi mở

        // Hiệu ứng Fade In và Scale Up mượt mà bằng DOTween
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

        RefreshGrid();
    }

    /// <summary>
    /// Đóng popup và kích hoạt sự kiện Cancel.
    /// </summary>
    public void ClosePopup()
    {
        CancelAllDeleteModes();
        
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
        transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() => {
            gameObject.SetActive(false);
            OnCancel?.Invoke();
        });
    }

    /// <summary>
    /// Tải danh sách nhân vật đã tạo và cập nhật trạng thái cho 7 button cố định.
    /// </summary>
    public async void RefreshGrid()
    {
        if (MainMenuDataManager.Instance == null || castButtons == null) return;

        // 1. Lấy danh sách các nhân vật tự tạo từ PlayerPrefs
        var characters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();
        _currentCharacterCount = characters != null ? characters.Count : 0;

        // 2. Duyệt qua 7 button cố định để nạp dữ liệu hoặc đặt trạng thái trống
        for (int i = 0; i < castButtons.Count; i++)
        {
            if (castButtons[i] == null) continue;

            // Hủy đăng ký sự kiện cũ để tránh lặp bộ nhớ
            castButtons[i].OnDeleted -= OnCharacterDeleted;
            castButtons[i].OnDeleteModeEntered -= OnButtonDeleteModeEntered;

            if (i < _currentCharacterCount && characters != null)
            {
                var charData = characters[i];
                // Lấy ảnh avatar và nhạc cụ từ MainMenuDataManager
                Sprite avatar = MainMenuDataManager.Instance.GetCharacterAvatarSprite(charData.prefabName);
                Sprite instrument = MainMenuDataManager.Instance.GetInstrumentAvatarSprite(charData.instrumentId);

                // Setup dữ liệu cho button
                castButtons[i].Setup(charData, avatar, instrument);
                
                // Lắng nghe sự kiện
                castButtons[i].OnDeleted += OnCharacterDeleted;
                castButtons[i].OnDeleteModeEntered += OnButtonDeleteModeEntered;
            }
            else
            {
                // Slot trống
                castButtons[i].Setup(null, null, null);
            }
        }

        if (backgroundBlocker != null)
        {
            backgroundBlocker.gameObject.SetActive(false);
        }

        // 3. Cập nhật nút Start (chỉ nhấn được khi đã giải phóng chỗ trống < 7)
        UpdateStartButtonInteractive();
    }

    private void OnCharacterDeleted()
    {
        // Khi xóa thành công bất kỳ nhân vật nào, reload lại giao diện
        RefreshGrid();
    }

    private void OnButtonDeleteModeEntered(PopupCastButton sender)
    {
        // Tắt chế độ xóa của tất cả các button khác
        foreach (var btn in castButtons)
        {
            if (btn != null && btn != sender)
            {
                btn.CancelDeleteMode();
            }
        }

        // Bật blocker lên để khi click ra ngoài thì tắt chế độ xóa
        if (backgroundBlocker != null)
        {
            backgroundBlocker.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Tắt chế độ xóa của toàn bộ các button và ẩn blocker.
    /// </summary>
    public void CancelAllDeleteModes()
    {
        foreach (var btn in castButtons)
        {
            if (btn != null)
            {
                btn.CancelDeleteMode();
            }
        }

        if (backgroundBlocker != null)
        {
            backgroundBlocker.gameObject.SetActive(false);
        }
    }

    private void UpdateStartButtonInteractive()
    {
        if (startButton == null) return;

        bool hasFreeSpace = _currentCharacterCount < 7;
        startButton.interactable = hasFreeSpace;

        // Cập nhật độ mờ/màu sắc của nút Start để người dùng nhận biết
        CanvasGroup cg = startButton.GetComponent<CanvasGroup>();
        if (cg == null) cg = startButton.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = hasFreeSpace ? 1.0f : 0.4f;
    }

    private void OnStartButtonClicked()
    {
        if (_currentCharacterCount >= 7)
        {
            Debug.LogWarning("[PopupStudio] Vẫn đạt giới hạn 7 nhân vật. Hãy xóa bớt nhân vật cũ!");
            return;
        }

        CancelAllDeleteModes();

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
        transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() => {
            gameObject.SetActive(false);
            OnStart?.Invoke();
        });
    }
}
