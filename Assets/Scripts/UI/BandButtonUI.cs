using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Quản lý giao diện hiển thị cho một ô nút ban nhạc (Band Button) trong ScrollView của Studio.
/// Tự động cập nhật 4 ảnh đại diện (avatar) và 4 tên tương ứng của các cast thành viên.
/// </summary>
public class BandButtonUI : MonoBehaviour
{
    [System.Serializable]
    public struct CastSlotUI
    {
        [Tooltip("Ảnh đại diện của thành viên")]
        public Image avatarImage;

        [Tooltip("Text hiển thị tên thành viên")]
        public TextMeshProUGUI nameText;
    }

    [Header("UI References")]
    [Tooltip("Text hiển thị tên ban nhạc trên nút")]
    [SerializeField] private TextMeshProUGUI bandNameText;

    [Tooltip("4 slot hiển thị thông tin thành viên (avatar và tên)")]
    [SerializeField] private CastSlotUI[] castSlots = new CastSlotUI[4];

    [Tooltip("Nút bấm chính để mở thông tin ban nhạc này")]
    [SerializeField] private Button button;

    // ─────────────────────────────────────────────────────────────────────────
    // Dữ liệu ban nhạc đi kèm
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dữ liệu của ban nhạc.
    /// </summary>
    public BandData Band { get; private set; }

    /// <summary>
    /// Danh sách ảnh đại diện tương ứng của các thành viên.
    /// </summary>
    public List<Sprite> BandAvatars { get; private set; } = new List<Sprite>();

    /// <summary>
    /// Tên của ban nhạc.
    /// </summary>
    public string BandName { get; private set; } = "My Band";

    /// <summary>
    /// Sự kiện tĩnh kích hoạt khi người dùng click vào nút ban nhạc này.
    /// Truyền vào: dữ liệu ban nhạc, danh sách avatar, và tên ban nhạc.
    /// </summary>
    public static System.Action<BandData, List<Sprite>, string> OnBandButtonClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    /// <summary>
    /// Khởi tạo và cập nhật giao diện hiển thị cho nút Ban nhạc.
    /// </summary>
    /// <param name="bandData">Dữ liệu ban nhạc (chứa danh sách cast).</param>
    /// <param name="avatars">Danh sách sprite avatar tương ứng của các thành viên.</param>
    /// <param name="bandName">Tên ban nhạc hiển thị.</param>
    public void Setup(BandData bandData, List<Sprite> avatars, string bandName = "My Band")
    {
        this.Band = bandData;
        this.BandName = bandName;
        
        // Cập nhật tên ban nhạc hiển thị trên nút
        if (bandNameText != null)
        {
            bandNameText.text = bandName;
        }

        this.BandAvatars.Clear();
        if (avatars != null)
        {
            this.BandAvatars.AddRange(avatars);
        }

        if (this.Band == null || this.Band.casts == null)
        {
            Debug.LogWarning("[BandButtonUI] Dữ liệu ban nhạc truyền vào bị rỗng!");
            return;
        }

        // Cập nhật giao diện của 4 cast slots
        for (int i = 0; i < castSlots.Length; i++)
        {
            if (i < this.Band.casts.Count)
            {
                CastData cast = this.Band.casts[i];

                // Cập nhật Tên
                if (castSlots[i].nameText != null)
                {
                    castSlots[i].nameText.text = cast != null ? cast.name : "None";
                }

                // Cập nhật Avatar Sprite
                if (castSlots[i].avatarImage != null)
                {
                    Sprite memberAvatar = (avatars != null && i < avatars.Count) ? avatars[i] : null;
                    if (memberAvatar != null)
                    {
                        castSlots[i].avatarImage.sprite = memberAvatar;
                        castSlots[i].avatarImage.enabled = true;
                    }
                    else
                    {
                        // Ẩn ảnh đi hoặc đặt ảnh trống nếu không có sprite
                        castSlots[i].avatarImage.enabled = false;
                    }
                }
            }
            else
            {
                // Xóa trống các slot thừa nếu ban nhạc có ít hơn 4 thành viên
                if (castSlots[i].nameText != null)
                {
                    castSlots[i].nameText.text = "Empty";
                }
                if (castSlots[i].avatarImage != null)
                {
                    castSlots[i].avatarImage.enabled = false;
                }
            }
        }
    }

    private void OnButtonClicked()
    {
        if (Band != null)
        {
            Debug.Log($"[BandButtonUI] Đã chọn ban nhạc: {BandName} với {Band.casts.Count} thành viên.");
            
            // Kích hoạt sự kiện toàn cục để mở popup hiển thị thông tin
            OnBandButtonClicked?.Invoke(Band, BandAvatars, BandName);

            // Hoặc trực tiếp gọi Singleton của Popup nếu đã được đăng ký
            if (BandDetailPopupUI.Instance != null)
            {
                BandDetailPopupUI.Instance.Show(Band, BandAvatars, BandName);
            }
        }
    }
}
