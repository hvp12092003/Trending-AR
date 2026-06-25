using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Component quản lý hiển thị của từng item trong ScrollView.
/// Hỗ trợ ảnh đại diện (avatar), tên (name), subtitle (category), và trạng thái chọn.
/// </summary>
public class CustomCharacterItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subtitleText; // dòng nhỏ bên dưới (ví dụ: Reaction, Comedy, Beat)
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button itemButton;

    [Header("Selection Button Sprites")]
    [FormerlySerializedAs("selectedBgSprite")]
    [SerializeField] private Sprite selectedSprite;
    [FormerlySerializedAs("unselectedBgSprite")]
    [SerializeField] private Sprite unselectedSprite;

    public string ItemId { get; private set; }

    public string GetTitle() => nameText != null ? nameText.text : "";

    /// <summary>
    /// Thiết lập giao diện cho Item (không có subtitle).
    /// </summary>
    public void Setup(string name, Sprite avatar, Action onClickCallback)
    {
        Setup(name, avatar, onClickCallback, null);
    }

    /// <summary>
    /// Thiết lập giao diện cho Item (có subtitle).
    /// </summary>
    public void Setup(string name, Sprite avatar, Action onClickCallback, string subtitle)
    {
        if (nameText != null) nameText.text = name;

        if (subtitleText != null)
        {
            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            subtitleText.text = hasSubtitle ? subtitle : "";
            subtitleText.gameObject.SetActive(hasSubtitle);
        }

        if (avatarImage != null)
        {
            if (avatar != null)
            {
                avatarImage.sprite = avatar;
                avatarImage.gameObject.SetActive(true);
            }
            else
            {
                avatarImage.gameObject.SetActive(false);
            }
        }

        ItemId = name; // Lưu tên làm định danh

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke());
            itemButton.interactable = true;
        }

        SetSelected(false);
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
