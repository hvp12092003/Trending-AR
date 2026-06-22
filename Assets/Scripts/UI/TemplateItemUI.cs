using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Quản lý giao diện hiển thị cho một ô mẫu nhạc (Band Template) trong ScrollView của Studio.
/// </summary>
public class TemplateItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI creatorText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI downloadText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI buttonText;

    private BandTemplateData _data;
    private Action<string> _onBuyCallback;

    /// <summary>
    /// Gán thông tin mẫu nhạc lên giao diện, nhận diện trạng thái sở hữu của người dùng.
    /// </summary>
    public void Setup(BandTemplateData data, string currentUserId, Action<string> onBuyCallback)
    {
        _data = data;
        _onBuyCallback = onBuyCallback;

        if (nameText != null) nameText.text = data.name;
        if (creatorText != null) creatorText.text = $"Tác giả: {data.creatorName}";
        if (priceText != null) priceText.text = data.price > 0 ? $"{data.price} Điểm" : "Miễn phí";
        if (downloadText != null) downloadText.text = $"{data.downloadCount} lượt mua";

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();

            if (data.creatorId == currentUserId)
            {
                // Template do chính người dùng hiện tại thiết kế
                if (buttonText != null) buttonText.text = "Tác phẩm của bạn";
                actionButton.interactable = false;
            }
            else if (data.buyerIds != null && data.buyerIds.Contains(currentUserId))
            {
                // Template đã được tài khoản hiện tại mua
                if (buttonText != null) buttonText.text = "Đã sở hữu";
                actionButton.interactable = false;
            }
            else
            {
                // Template chưa mua
                if (buttonText != null) buttonText.text = "Mua";
                actionButton.interactable = true;
                actionButton.onClick.AddListener(OnBuyClicked);
            }
        }
    }

    private void OnBuyClicked()
    {
        if (_data != null && _onBuyCallback != null)
        {
            _onBuyCallback.Invoke(_data.templateId);
        }
    }
}
