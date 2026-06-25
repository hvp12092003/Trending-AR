using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện hiển thị cho một ô nhân vật trong ScrollView của Studio.
/// </summary>
public class CharacterItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private Button deleteButton;

    [Tooltip("Ảnh hiển thị nhạc cụ đi kèm")]
    [SerializeField] private Image instrumentImage;

    public Button DeleteButton => deleteButton;

    /// <summary>
    /// Gán thông tin nhân vật lên giao diện.
    /// </summary>
    public void Setup(CharacterData data)
    {
        if (nameText != null) 
            nameText.text = data.name;

        if (dateText != null)
        {
            // Chuyển đổi Unix timestamp (seconds) sang System.DateTime
            System.DateTime date = System.DateTimeOffset
                .FromUnixTimeSeconds(data.createdAtSeconds)
                .LocalDateTime;
            dateText.text = $"Tạo ngày: {date:dd/MM/yyyy}";
        }

        // Cập nhật hiển thị nhạc cụ đi kèm
        if (instrumentImage != null)
        {
            if (data != null && MainMenuDataManager.Instance != null)
            {
                Sprite instrumentSprite = MainMenuDataManager.Instance.GetInstrumentAvatarSprite(data.instrumentId);
                if (instrumentSprite != null)
                {
                    instrumentImage.sprite = instrumentSprite;
                    instrumentImage.gameObject.SetActive(true);
                }
                else
                {
                    instrumentImage.gameObject.SetActive(false);
                }
            }
            else
            {
                instrumentImage.gameObject.SetActive(false);
            }
        }
    }
}
