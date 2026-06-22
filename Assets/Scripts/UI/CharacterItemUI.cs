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
            // Chuyển đổi Firestore Timestamp sang System.DateTime
            System.DateTime date = data.createdAt.ToDateTime().ToLocalTime();
            dateText.text = $"Tạo ngày: {date:dd/MM/yyyy}";
        }
    }
}
