using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện cho nút Cast hiển thị riêng trong Popup Studio.
/// Tự động đổi trạng thái làm mờ ảnh avatar/nhạc cụ và hiện nút xóa khi bấm lần đầu.
/// </summary>
public class PopupCastButton : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Ảnh đại diện nhân vật (avatar 1)")]
    [SerializeField] private Image avatarImage;

    [Tooltip("Ảnh nhạc cụ đi kèm (avatar 2)")]
    [SerializeField] private Image instrumentImage;

    [Tooltip("Text hiển thị tên nhân vật")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Icon Delete (Hình ảnh nút xóa thùng rác)")]
    [SerializeField] private Image deleteIconImage;

    [Tooltip("Hình ảnh hiển thị khi slot chưa có Cast (ô trống)")]
    [SerializeField] private Image emptySlotImage;

    [Header("Buttons")]
    [Tooltip("Nút bấm chính bao phủ để chọn/kích hoạt chế độ xóa")]
    [SerializeField] private Button mainButton;

    [Tooltip("Nút bấm thực tế dùng để click xóa (bao bọc deleteIconImage)")]
    [SerializeField] private Button deleteButton;

    public CharacterData Character { get; private set; }
    public int SlotNumber { get; private set; }
    
    // Sự kiện khi nhân vật bị xóa thành công
    public event Action OnDeleted;

    // Sự kiện thông báo khi nút này chuyển sang chế độ xóa
    public event Action<PopupCastButton> OnDeleteModeEntered;
    public event Action<PopupCastButton> OnUnlockRequested;

    private bool _isDeleteMode = false;
    private bool _isLockedSlot = false;
    private bool _canRequestUnlock = false;
    public bool IsDeleteMode => _isDeleteMode;

    private void Awake()
    {
        if (mainButton == null)
        {
            mainButton = GetComponent<Button>();
        }

        if (mainButton != null)
        {
            mainButton.onClick.RemoveAllListeners();
            mainButton.onClick.AddListener(OnMainButtonClicked);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }

        // Mặc định ẩn chế độ xóa
        SetDeleteMode(false);
    }

    /// <summary>
    /// Nạp dữ liệu và cấu hình giao diện.
    /// </summary>
    public void Setup(CharacterData charData, Sprite avatarSprite, Sprite instrumentSprite)
    {
        Character = charData;
        _isLockedSlot = false;
        _canRequestUnlock = false;
        SetDeleteMode(false);

        bool hasCharacter = charData != null;

        if (mainButton != null)
        {
            mainButton.interactable = hasCharacter;
        }

        if (emptySlotImage != null)
        {
            emptySlotImage.gameObject.SetActive(!hasCharacter);
        }

        if (nameText != null)
        {
            nameText.text = hasCharacter ? charData.name : "";
            nameText.gameObject.SetActive(hasCharacter);
        }

        if (avatarImage != null)
        {
            avatarImage.sprite = avatarSprite;
            avatarImage.gameObject.SetActive(hasCharacter && avatarSprite != null);
        }

        if (instrumentImage != null)
        {
            instrumentImage.sprite = instrumentSprite;
            instrumentImage.gameObject.SetActive(hasCharacter && instrumentSprite != null);
        }
    }

    public void SetupLocked(int slotNumber, bool canRequestUnlock)
    {
        Character = null;
        SlotNumber = slotNumber;
        _isLockedSlot = true;
        _canRequestUnlock = canRequestUnlock;
        SetDeleteMode(false);

        if (mainButton != null)
        {
            mainButton.interactable = canRequestUnlock;
        }

        if (emptySlotImage != null)
        {
            emptySlotImage.gameObject.SetActive(true);
        }

        if (nameText != null)
        {
            nameText.text = canRequestUnlock ? $"Slot {slotNumber}\nWatch AD" : $"Slot {slotNumber}\nLocked";
            nameText.gameObject.SetActive(true);
        }

        if (avatarImage != null)
        {
            avatarImage.gameObject.SetActive(false);
        }

        if (instrumentImage != null)
        {
            instrumentImage.gameObject.SetActive(false);
        }
    }

    private void OnMainButtonClicked()
    {
        if (_isLockedSlot)
        {
            if (_canRequestUnlock)
            {
                OnUnlockRequested?.Invoke(this);
            }
            return;
        }

        if (Character == null) return;
        
        bool nextState = !_isDeleteMode;
        SetDeleteMode(nextState);

        if (nextState)
        {
            OnDeleteModeEntered?.Invoke(this);
        }
    }

    private async void OnDeleteButtonClicked()
    {
        if (Character == null || string.IsNullOrEmpty(Character.characterId) || MainMenuDataManager.Instance == null) return;

        Debug.Log($"[PopupCastButton] Yêu cầu xóa nhân vật: {Character.name} ({Character.characterId})");
        bool success = await MainMenuDataManager.Instance.DeleteCharacterAsync(Character.characterId);
        if (success)
        {
            Debug.Log($"[PopupCastButton] Đã xóa nhân vật thành công!");
            OnDeleted?.Invoke();
        }
        else
        {
            Debug.LogError($"[PopupCastButton] Xóa nhân vật thất bại!");
            SetDeleteMode(false);
        }
    }

    /// <summary>
    /// Tắt chế độ xóa từ bên ngoài.
    /// </summary>
    public void CancelDeleteMode()
    {
        if (_isDeleteMode)
        {
            SetDeleteMode(false);
        }
    }

    private void SetDeleteMode(bool active)
    {
        _isDeleteMode = active;

        // Làm mờ avatar và nhạc cụ (avatar 1 & 2) khi hiện nút xóa
        float targetAlpha = active ? 0.2f : 1.0f;

        if (avatarImage != null)
        {
            Color col = avatarImage.color;
            col.a = targetAlpha;
            avatarImage.color = col;
        }

        if (instrumentImage != null)
        {
            Color col = instrumentImage.color;
            col.a = targetAlpha;
            instrumentImage.color = col;
        }

        // Ẩn/hiện Icon Delete và Delete Button
        if (deleteIconImage != null)
        {
            deleteIconImage.gameObject.SetActive(active);
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(active);
        }
    }
}
