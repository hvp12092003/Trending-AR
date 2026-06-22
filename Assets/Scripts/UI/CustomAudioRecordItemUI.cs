using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component quản lý hiển thị của nút ghi âm (Record Trigger và Recorded Item) trong ScrollView.
/// Hỗ trợ hiển thị viền đỏ, tiến trình radial đếm ngược, và nút xóa bản ghi âm.
/// </summary>
public class CustomAudioRecordItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button itemButton;

    [Header("Selection Button Sprites")]
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite unselectedSprite;

    [Header("Record/Delete Optionals")]
    [SerializeField] private Button deleteButton;       // Nút xóa ghi âm (biểu tượng thùng rác)
    [SerializeField] private Image recordBorder;        // Viền đỏ khi thu âm
    [SerializeField] private Image radialFill;          // Ảnh tiến trình ghi âm (Radial 360)

    public string ItemId { get; private set; }

    public string GetTitle() => nameText != null ? nameText.text : "";

    private void EnsureReferences()
    {
        if (deleteButton == null)
        {
            Transform t = transform.Find("DeleteButton");
            if (t != null) deleteButton = t.GetComponent<Button>();
        }
        if (recordBorder == null)
        {
            Transform t = transform.Find("RecordBorder");
            if (t != null) recordBorder = t.GetComponent<Image>();
        }
        if (radialFill == null)
        {
            Transform t = transform.Find("RadialFill");
            if (t != null) radialFill = t.GetComponent<Image>();
        }
    }

    /// <summary>
    /// Thiết lập giao diện cho nút Bắt đầu ghi âm.
    /// </summary>
    public void SetupRecordTrigger(Sprite recordIcon, Action onClickCallback)
    {
        EnsureReferences();

        if (nameText != null) nameText.text = "Record";
        if (avatarImage != null)
        {
            if (recordIcon != null)
            {
                avatarImage.sprite = recordIcon;
                avatarImage.gameObject.SetActive(true);
            }
            else
            {
                avatarImage.gameObject.SetActive(false);
            }
        }

        ItemId = "Record_Trigger"; // Định danh đặc biệt

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke());
            itemButton.interactable = true;
        }

        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        if (recordBorder != null) recordBorder.gameObject.SetActive(false);
        if (radialFill != null)
        {
            radialFill.gameObject.SetActive(true);
            radialFill.fillAmount = 0f;
        }

        SetSelected(false);
    }

    /// <summary>
    /// Thiết lập giao diện cho thẻ Âm thanh đã ghi âm.
    /// </summary>
    public void SetupRecordedItem(string name, Sprite icon, Action onClickCallback, Action onDeleteCallback)
    {
        EnsureReferences();

        if (nameText != null) nameText.text = name;
        if (avatarImage != null)
        {
            if (icon != null)
            {
                avatarImage.sprite = icon;
                avatarImage.gameObject.SetActive(true);
            }
            else
            {
                avatarImage.gameObject.SetActive(false);
            }
        }

        ItemId = "Record_Audio"; // Định danh đặc biệt

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke());
            itemButton.interactable = true;
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(true);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDeleteCallback?.Invoke());
        }

        if (recordBorder != null) recordBorder.gameObject.SetActive(false);
        if (radialFill != null) radialFill.gameObject.SetActive(false);

        SetSelected(false);
    }

    /// <summary>
    /// Cập nhật viền đỏ và độ tương tác của nút khi đang ghi âm.
    /// </summary>
    public void SetRecordingState(bool isRecording)
    {
        EnsureReferences();
        if (recordBorder != null)
        {
            recordBorder.gameObject.SetActive(isRecording);
        }
        if (itemButton != null)
        {
            itemButton.interactable = !isRecording;
        }
    }

    /// <summary>
    /// Cập nhật phần trăm tiến trình ghi âm.
    /// </summary>
    public void SetRadialFill(float amount)
    {
        EnsureReferences();
        if (radialFill != null)
        {
            radialFill.gameObject.SetActive(true);
            radialFill.fillAmount = amount;
        }
    }

    /// <summary>
    /// Thay đổi Sprite của Button tương ứng với trạng thái được chọn hay không.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (itemButton != null)
        {
            Image btnImage = itemButton.image != null ? itemButton.image : itemButton.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.sprite = isSelected ? selectedSprite : unselectedSprite;
            }
        }
    }
}
