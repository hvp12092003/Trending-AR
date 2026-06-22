using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AnimPanelController quản lý hiển thị danh sách các hoạt ảnh (animation) của nhân vật (Cast) trong ScrollView.
/// Tự động lấy các hoạt ảnh từ cấu hình CastAnimationConfig trên prefab của nhân vật được chọn và tạo các nút bấm tương ứng.
/// Nếu không có cấu hình, component sẽ sử dụng danh sách hoạt ảnh mặc định (defaultPresets).
/// </summary>
public class AnimPanelController : MonoBehaviour
{
    [System.Serializable]
    public struct AnimationPreset
    {
        public string displayName;
        public string animationId;
        public Sprite icon;
    }

    [Header("UI References")]
    [Tooltip("Transform Content của ScrollView để chứa các nút chọn hoạt ảnh")]
    [SerializeField] private Transform scrollViewContent;

    [Tooltip("Prefab của nút chọn hoạt ảnh (hỗ trợ CustomCharacterItemUI hoặc Button tiêu chuẩn)")]
    [SerializeField] private GameObject animationButtonPrefab;

    [Header("Aesthetic Fallbacks (Optional)")]
    [Tooltip("Sprite nền của nút khi được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite selectedButtonSprite;

    [Tooltip("Sprite nền của nút khi không được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite unselectedButtonSprite;

    [Header("Initialization Settings")]
    [Tooltip("Tự động chọn hoạt ảnh đầu tiên sau khi khởi tạo thành công")]
    [SerializeField] private bool selectFirstOnStart = true;

    [Header("Default Presets (Fallback)")]
    [Tooltip("Danh sách hoạt ảnh mặc định khi nhân vật không có cấu hình CastAnimationConfig")]
    [SerializeField] private List<AnimationPreset> defaultPresets = new List<AnimationPreset>();

    [Header("Events")]
    [Tooltip("Sự kiện kích hoạt khi một hoạt ảnh được chọn (truyền vào ID/tên clip hoạt ảnh)")]
    public UnityEngine.Events.UnityEvent<string> onAnimationSelectedEvent;

    [Tooltip("Sự kiện kích hoạt khi một hoạt ảnh được chọn (truyền vào CastAnimationItem tương ứng)")]
    public UnityEngine.Events.UnityEvent<CastAnimationItem> onAnimationItemSelectedEvent;

    // Sự kiện C# Actions để dễ đăng ký từ code
    public event Action<string> OnAnimationSelected;
    public event Action<CastAnimationItem> OnAnimationItemSelected;

    // Trạng thái lưu trữ
    private List<GameObject> _instantiatedButtons = new List<GameObject>();
    private List<CastAnimationItem> _availableAnimations = new List<CastAnimationItem>();

    public string SelectedAnimationId { get; private set; }
    public CastAnimationItem SelectedAnimationItem { get; private set; }
    public int SelectedIndex { get; private set; } = -1;

    private void Awake()
    {
        // Khởi tạo một số preset mặc định nếu danh sách trống để làm phương án dự phòng
        if (defaultPresets == null || defaultPresets.Count == 0)
        {
            defaultPresets = new List<AnimationPreset>
            {
                new AnimationPreset { displayName = "Jump", animationId = "jump" },
                new AnimationPreset { displayName = "Dance 1", animationId = "dance1" },
                new AnimationPreset { displayName = "Dance 2", animationId = "dance2" }
            };
        }
    }

    /// <summary>
    /// Xóa toàn bộ các nút đã được sinh ra trong ScrollView.
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
        _availableAnimations.Clear();
        SelectedAnimationId = null;
        SelectedAnimationItem = default;
        SelectedIndex = -1;
    }

    /// <summary>
    /// Khởi tạo danh sách nút hoạt ảnh dựa trên prefab nhân vật.
    /// </summary>
    public void InitializePanel(GameObject characterPrefab)
    {
        if (characterPrefab == null)
        {
            ClearPanel();
            return;
        }

        CastAnimationConfig config = characterPrefab.GetComponent<CastAnimationConfig>();
        InitializePanel(config);
    }

    /// <summary>
    /// Khởi tạo danh sách nút hoạt ảnh dựa trên cấu hình CastAnimationConfig.
    /// </summary>
    public void InitializePanel(CastAnimationConfig config)
    {
        ClearPanel();

        if (scrollViewContent == null)
        {
            Debug.LogError("[AnimPanelController] Chưa gán scrollViewContent!");
            return;
        }

        if (animationButtonPrefab == null)
        {
            Debug.LogError("[AnimPanelController] Chưa gán animationButtonPrefab!");
            return;
        }

        // 1. Thu thập danh sách các hoạt ảnh từ cấu hình nhân vật
        if (config != null && config.animations != null && config.animations.Count > 0)
        {
            foreach (var animInfo in config.animations)
            {
                if (animInfo.animation == null) continue;
                _availableAnimations.Add(animInfo);
            }
        }

        // 2. Nếu nhân vật không có hoạt ảnh cấu hình riêng, sử dụng danh sách mặc định (defaultPresets)
        if (_availableAnimations.Count == 0)
        {
            for (int i = 0; i < defaultPresets.Count; i++)
            {
                var preset = defaultPresets[i];
                CastAnimationItem fallbackItem = new CastAnimationItem
                {
                    animation = null,
                    sprite = preset.icon,
                    animName = preset.displayName
                };
                _availableAnimations.Add(fallbackItem);
            }
        }

        // 3. Sinh các nút trong ScrollView
        for (int i = 0; i < _availableAnimations.Count; i++)
        {
            CastAnimationItem animItem = _availableAnimations[i];
            
            // Xác định ID hoạt ảnh (ưu tiên lấy tên clip thực tế, nếu không lấy ID preset hoặc tên hiển thị)
            string animId = animItem.animation != null 
                ? animItem.animation.name 
                : (i < defaultPresets.Count ? defaultPresets[i].animationId : animItem.animName);
            
            string displayName = string.IsNullOrEmpty(animItem.animName) ? animId : animItem.animName;
            Sprite avatar = animItem.sprite;

            GameObject buttonObj = Instantiate(animationButtonPrefab, scrollViewContent);
            _instantiatedButtons.Add(buttonObj);
            
            buttonObj.name = $"Btn_Anim_{displayName}";

            int index = i;
            SetupButtonComponent(buttonObj, displayName, avatar, index);
        }

        // 4. Tự động chọn hoạt ảnh đầu tiên nếu được cấu hình
        if (selectFirstOnStart && _availableAnimations.Count > 0)
        {
            SelectAnimation(0);
        }
    }

    /// <summary>
    /// Chọn hoạt ảnh theo Index trong danh sách.
    /// </summary>
    public void SelectAnimation(int index)
    {
        if (index < 0 || index >= _availableAnimations.Count)
        {
            Debug.LogWarning($"[AnimPanelController] Index {index} vượt quá giới hạn danh sách hoạt ảnh.");
            return;
        }

        SelectedIndex = index;
        SelectedAnimationItem = _availableAnimations[index];

        // Xác định ID hoạt ảnh đang chọn
        string animId = SelectedAnimationItem.animation != null 
            ? SelectedAnimationItem.animation.name 
            : (index < defaultPresets.Count ? defaultPresets[index].animationId : SelectedAnimationItem.animName);
        
        SelectedAnimationId = animId;

        // Cập nhật giao diện chọn cho các nút
        UpdateSelectionVisuals();

        // Kích hoạt các sự kiện
        OnAnimationSelected?.Invoke(SelectedAnimationId);
        onAnimationSelectedEvent?.Invoke(SelectedAnimationId);

        OnAnimationItemSelected?.Invoke(SelectedAnimationItem);
        onAnimationItemSelectedEvent?.Invoke(SelectedAnimationItem);

        Debug.Log($"[AnimPanelController] Đã chọn hoạt ảnh: {SelectedAnimationId} (Index: {SelectedIndex})");
      
    }

    /// <summary>
    /// Chọn hoạt ảnh theo ID (tên clip hoặc ID preset).
    /// </summary>
    public void SelectAnimationById(string animId)
    {
        if (string.IsNullOrEmpty(animId)) return;

        for (int i = 0; i < _availableAnimations.Count; i++)
        {
            string currentAnimId = _availableAnimations[i].animation != null 
                ? _availableAnimations[i].animation.name 
                : (i < defaultPresets.Count ? defaultPresets[i].animationId : _availableAnimations[i].animName);

            if (currentAnimId.Equals(animId, StringComparison.OrdinalIgnoreCase))
            {
                SelectAnimation(i);
                return;
            }
        }
        Debug.LogWarning($"[AnimPanelController] Không tìm thấy hoạt ảnh có ID: {animId}");
    }

    /// <summary>
    /// Thiết lập hiển thị và sự kiện click cho nút hoạt ảnh.
    /// </summary>
    private void SetupButtonComponent(GameObject buttonObj, string displayName, Sprite avatar, int index)
    {
        // TH 1: Nút sử dụng CustomCharacterItemUI (đồng bộ visual)
        var customUI = buttonObj.GetComponent<CustomCharacterItemUI>();
        if (customUI != null)
        {
            customUI.Setup(displayName, avatar, () => SelectAnimation(index));
            return;
        }

        // TH 2: Nút bấm tiêu chuẩn (Standard Button)
        Button standardBtn = buttonObj.GetComponent<Button>();
        if (standardBtn != null)
        {
            standardBtn.onClick.RemoveAllListeners();
            standardBtn.onClick.AddListener(() => SelectAnimation(index));

            // Tìm và gán tên hoạt ảnh
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

            // Tìm và gán hình ảnh avatar
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
    /// Cập nhật hiển thị được chọn/không được chọn cho từng nút bấm.
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

            // TH 2: Button tiêu chuẩn
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
}
