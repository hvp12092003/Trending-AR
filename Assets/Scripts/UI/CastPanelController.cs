using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CastPanelController quản lý việc hiển thị danh sách nhân vật (Cast) trong ScrollView.
/// Tự động lấy thông tin (tên, avatar) từ list prefab nhân vật và sinh ra các button tương ứng.
/// </summary>
public class CastPanelController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Prefab của nút chọn nhân vật (hỗ trợ CustomCharacterItemUI, CastButtonUI hoặc Button tiêu chuẩn)")]
    [SerializeField] private GameObject characterButtonPrefab;

    [Tooltip("Transform Content của ScrollView để chứa các nút chọn nhân vật")]
    [SerializeField] private Transform scrollViewContent;

    [Header("Aesthetic Fallbacks (Optional)")]

    [Tooltip("Sprite nền của nút khi được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite selectedButtonSprite;

    [Tooltip("Sprite nền của nút khi không được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite unselectedButtonSprite;

    [Header("Initialization Settings")]
    [Tooltip("Tự động khởi tạo và sinh nút nhân vật khi Start")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("Tự động chọn nhân vật đầu tiên sau khi khởi tạo thành công")]
    [SerializeField] private bool selectFirstOnStart = true;

    [Header("Events")]
    [Tooltip("Sự kiện kích hoạt khi nhân vật được chọn (truyền vào prefab nhân vật)")]
    public UnityEngine.Events.UnityEvent<GameObject> onCharacterSelectedEvent;

    [Tooltip("Sự kiện kích hoạt khi nhân vật được chọn (truyền vào CastData của nhân vật)")]
    public UnityEngine.Events.UnityEvent<CastData> onCastDataSelectedEvent;

    // Sự kiện C# Actions để dễ đăng ký từ code
    public event Action<GameObject> OnCharacterSelected;
    public event Action<CastData> OnCastDataSelected;

    // Trạng thái lưu trữ các nút đã sinh
    private List<GameObject> _instantiatedButtons = new List<GameObject>();
    
    // Thuộc tính công khai để truy xuất dữ liệu đang chọn
    public GameObject SelectedPrefab { get; private set; }
    public CastData SelectedCastData { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public List<GameObject> CharacterPrefabs
    {
        get
        {
            if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.CharacterPrefabs != null)
            {
                return MainMenuDataManager.Instance.CharacterPrefabs;
            }
            return new List<GameObject>();
        }
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializePanel();
        }
    }

    /// <summary>
    /// Thực hiện xóa các nút cũ và sinh lại các nút nhân vật mới dựa trên danh sách prefab.
    /// </summary>
    public void InitializePanel()
    {
        ClearPanel();

        if (characterButtonPrefab == null)
        {
            Debug.LogError("[CastPanelController] Chưa gán characterButtonPrefab!");
            return;
        }

        if (scrollViewContent == null)
        {
            Debug.LogError("[CastPanelController] Chưa gán scrollViewContent!");
            return;
        }

        List<GameObject> prefabs = CharacterPrefabs;

        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("[CastPanelController] Danh sách nhân vật từ MainMenuDataManager trống!");
            return;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null) continue;

            // 1. Lấy thông tin từ prefab nhân vật
            CastPrefab config = prefab.GetComponent<CastPrefab>();
            string displayName = (config != null && !string.IsNullOrEmpty(config.Name)) ? config.Name : prefab.name;
            Sprite avatar = (config != null) ? config.characterAvatar : null;

            // Nếu avatar trên prefab trống, thử lấy từ danh sách avatarSprites hoặc load Resources
            if (avatar == null)
            {
                avatar = GetAvatarSprite(prefab.name);
            }

            // 2. Sinh nút bấm trong ScrollView
            GameObject buttonObj = Instantiate(characterButtonPrefab, scrollViewContent);
            _instantiatedButtons.Add(buttonObj);

            // Đặt tên GameObject cho dễ debug trong Hierarchy
            buttonObj.name = $"Btn_Character_{displayName}";

            // 3. Thiết lập logic và hiển thị cho nút dựa trên Component có sẵn
            SetupButtonComponent(buttonObj, prefab, displayName, avatar, i);
        }

        // Tự động chọn nhân vật đầu tiên nếu được cấu hình
        if (selectFirstOnStart && _instantiatedButtons.Count > 0)
        {
            SelectCharacter(0);
        }
    }

    /// <summary>
    /// Xóa toàn bộ các nút nhân vật đã được sinh ra trong ScrollView.
    /// </summary>
    public void ClearPanel()
    {
        if (scrollViewContent != null)
        {
            for (int i = scrollViewContent.childCount - 1; i >= 0; i--)
            {
                Destroy(scrollViewContent.GetChild(i).gameObject);
            }
        }

        _instantiatedButtons.Clear();
        SelectedPrefab = null;
        SelectedCastData = null;
        SelectedIndex = -1;
    }

    /// <summary>
    /// Chọn nhân vật theo Index tương ứng trong danh sách prefab.
    /// </summary>
    public void SelectCharacter(int index)
    {
        List<GameObject> prefabs = CharacterPrefabs;
        if (index < 0 || index >= prefabs.Count)
        {
            Debug.LogWarning($"[CastPanelController] Index {index} vượt quá giới hạn danh sách nhân vật.");
            return;
        }

        GameObject selectedPrefab = prefabs[index];
        if (selectedPrefab == null) return;

        SelectedIndex = index;
        SelectedPrefab = selectedPrefab;

        // Tạo CastData cho nhân vật được chọn
        SelectedCastData = CreateCastDataFromPrefab(selectedPrefab);

        // Cập nhật giao diện chọn của tất cả các nút
        UpdateSelectionVisuals();

        // Kích hoạt các sự kiện
        OnCharacterSelected?.Invoke(SelectedPrefab);
        onCharacterSelectedEvent?.Invoke(SelectedPrefab);

        OnCastDataSelected?.Invoke(SelectedCastData);
        onCastDataSelectedEvent?.Invoke(SelectedCastData);

        Debug.Log($"[CastPanelController] Đã chọn nhân vật: {SelectedCastData.name} (Index: {SelectedIndex})");
    }

    /// <summary>
    /// Thiết lập hiển thị và gán sự kiện click cho nút bấm.
    /// </summary>
    private void SetupButtonComponent(GameObject buttonObj, GameObject prefab, string displayName, Sprite avatar, int index)
    {
        // TH 1: Nút sử dụng CustomCharacterItemUI
        var customUI = buttonObj.GetComponent<CustomCharacterItemUI>();
        if (customUI != null)
        {
            customUI.Setup(displayName, avatar, () => SelectCharacter(index));
            return;
        }

        // TH 2: Nút sử dụng CastButtonUI
        var castUI = buttonObj.GetComponent<CastButtonUI>();
        if (castUI != null)
        {
            CastData tempCastData = CreateCastDataFromPrefab(prefab);
            castUI.Setup(tempCastData, avatar);

            // Ghi đè sự kiện click của button gốc
            Button btnComp = buttonObj.GetComponent<Button>();
            if (btnComp != null)
            {
                btnComp.onClick.RemoveAllListeners();
                btnComp.onClick.AddListener(() => SelectCharacter(index));
            }
            return;
        }

        // TH 3: Nút bấm tiêu chuẩn của Unity (Standard Button)
        Button standardBtn = buttonObj.GetComponent<Button>();
        if (standardBtn != null)
        {
            standardBtn.onClick.RemoveAllListeners();
            standardBtn.onClick.AddListener(() => SelectCharacter(index));

            // Tìm và gán tên nhân vật vào TextMeshProUGUI hoặc Text
            var nameText = buttonObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText != null)
            {
                nameText.text = displayName;
            }
            else
            {
                var legacyText = buttonObj.GetComponentInChildren<Text>(true);
                if (legacyText != null)
                {
                    legacyText.text = displayName;
                }
            }

            // Tìm và gán hình ảnh avatar (bỏ qua Image nền của chính nút bấm)
            Image[] images = buttonObj.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != buttonObj)
                {
                    img.sprite = avatar;
                    img.gameObject.SetActive(avatar != null);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Cập nhật trạng thái hiển thị được chọn/không được chọn cho từng nút bấm.
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < _instantiatedButtons.Count; i++)
        {
            GameObject buttonObj = _instantiatedButtons[i];
            if (buttonObj == null) continue;

            bool isSelected = (i == SelectedIndex);

            // TH 1: CustomCharacterItemUI
            var customUI = buttonObj.GetComponent<CustomCharacterItemUI>();
            if (customUI != null)
            {
                customUI.SetSelected(isSelected);
                continue;
            }

            // TH 2 & 3: CastButtonUI hoặc Button tiêu chuẩn sử dụng hình nền được chọn
            Button standardBtn = buttonObj.GetComponent<Button>();
            if (standardBtn != null)
            {
                Image btnImage = standardBtn.image != null ? standardBtn.image : standardBtn.GetComponent<Image>();
                if (btnImage != null)
                {
                    Sprite targetSprite = isSelected ? selectedButtonSprite : unselectedButtonSprite;
                    if (targetSprite != null)
                    {
                        btnImage.sprite = targetSprite;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tạo đối tượng CastData chứa đầy đủ thông tin tên, prefabName và danh sách animation nhảy từ prefab.
    /// </summary>
    private CastData CreateCastDataFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;

        if (MainMenuDataManager.Instance != null)
        {
            CastData cast = MainMenuDataManager.Instance.CreateCastDataFromPrefab(prefab);
            if (cast != null) return cast;
        }

        CastPrefab config = prefab.GetComponent<CastPrefab>();
        string displayName = (config != null && !string.IsNullOrEmpty(config.Name)) ? config.Name : prefab.name;

        List<string> danceAnimIds = new List<string>();
        string defaultDanceAnimId = "";

        if (config != null && config.animations != null)
        {
            foreach (var anim in config.animations)
            {
                if (anim.animation != null)
                {
                    string animName = anim.animation.name;
                    danceAnimIds.Add(animName);

                    // Mặc định chọn animation đầu tiên làm mặc định
                    if (string.IsNullOrEmpty(defaultDanceAnimId))
                    {
                        defaultDanceAnimId = animName;
                    }
                }
            }
        }

        return new CastData(displayName, prefab.name, "", defaultDanceAnimId, danceAnimIds);
    }

    /// <summary>
    /// Tìm kiếm Sprite avatar dự phòng từ Resources nếu trên prefab không có.
    /// </summary>
    private Sprite GetAvatarSprite(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        // Load dự phòng từ thư mục Resources/Avatars/
        return Resources.Load<Sprite>("Avatars/" + prefabName);
    }
}
