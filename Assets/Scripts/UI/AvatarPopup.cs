using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện và logic của Popup lựa chọn ảnh đại diện (Avatar).
/// Tự động lấy danh sách ảnh đại diện từ Catalog của các nhân vật (Cast) và nhạc cụ (Instrument)
/// để hiển thị lên ScrollView.
/// </summary>
public class AvatarPopup : MonoBehaviour
{
    [Header("UI Containers")]
    [Tooltip("Panel chính chứa popup")]
    [SerializeField] private GameObject popupPanel;

    [Tooltip("Scroll content chứa danh sách nút bấm")]
    [SerializeField] private Transform scrollContent;

    [Header("UI Elements")]
    [Tooltip("Nút đóng popup")]
    [SerializeField] private Button closeButton;

    [Tooltip("Nền tối phía sau, click để đóng popup")]
    [SerializeField] private Button backgroundBlocker;

    [Header("Templates")]
    [Tooltip("Avatar_Flag Button prefab dung chung cho popup Avatar va Flag.")]
    [FormerlySerializedAs("avatarButtonPrefab")]
    [SerializeField] private Button avatarFlagButtonPrefab;

    private enum PickerMode
    {
        Avatar,
        Flag
    }

    private Action<string, Sprite> _onAvatarSelected;
    private bool _isOpening = false;
    private int _populateVersion;
    private PickerMode _currentMode = PickerMode.Avatar;
    private readonly List<GameObject> _instantiatedButtons = new List<GameObject>();

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (backgroundBlocker != null)
        {
            backgroundBlocker.onClick.RemoveAllListeners();
            backgroundBlocker.onClick.AddListener(ClosePopup);
        }
    }

    /// <summary>
    /// Hiển thị Popup và tạo danh sách avatar.
    /// </summary>
    public void OpenPopup(Action<string, Sprite> onAvatarSelected)
    {
        if (_isOpening) return;
        _isOpening = true;

        _onAvatarSelected = onAvatarSelected;
        _currentMode = PickerMode.Avatar;
        _populateVersion++;
        gameObject.SetActive(true);
        if (popupPanel != null) popupPanel.SetActive(true);

        // Sinh danh sách các avatar
        PopulateAvatars();

        // Hiệu ứng Fade In và Scale Up mượt mà sử dụng DOTween
        if (popupPanel != null)
        {
            popupPanel.transform.localScale = Vector3.one * 0.8f;
            CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = popupPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            cg.DOKill();
            popupPanel.transform.DOKill();
            cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
            popupPanel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).OnComplete(() => {
                _isOpening = false;
            });
        }
        else
        {
            _isOpening = false;
        }
    }

    public void OpenFlagPopup(Action<string, Sprite> onFlagSelected)
    {
        if (_isOpening) return;
        _isOpening = true;

        _onAvatarSelected = onFlagSelected;
        _currentMode = PickerMode.Flag;
        _populateVersion++;
        gameObject.SetActive(true);
        if (popupPanel != null) popupPanel.SetActive(true);

        PopulateCountryFlags();

        if (popupPanel != null)
        {
            popupPanel.transform.localScale = Vector3.one * 0.8f;
            CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = popupPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            cg.DOKill();
            popupPanel.transform.DOKill();
            cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
            popupPanel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).OnComplete(() => {
                _isOpening = false;
            });
        }
        else
        {
            _isOpening = false;
        }
    }

    /// <summary>
    /// Đóng Popup với hiệu ứng thu nhỏ và mờ dần.
    /// </summary>
    public void ClosePopup()
    {
        _populateVersion++;

        if (popupPanel != null)
        {
            CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = popupPanel.AddComponent<CanvasGroup>();

            cg.DOKill();
            popupPanel.transform.DOKill();
            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
            popupPanel.transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() => {
                gameObject.SetActive(false);
                ClearInstantiatedButtons();
            });
        }
        else
        {
            gameObject.SetActive(false);
            ClearInstantiatedButtons();
        }
    }

    private void PopulateAvatars()
    {
        ClearInstantiatedButtons();

        if (scrollContent == null || avatarFlagButtonPrefab == null || MainMenuDataManager.Instance == null)
        {
            Debug.LogWarning("[AvatarPopup] Thiếu cấu hình UI hoặc MainMenuDataManager chưa sẵn sàng.");
            return;
        }

        HashSet<Sprite> addedSprites = new HashSet<Sprite>();

        // 1. Lấy tất cả ảnh của các Cast (nhân vật)
        var characterEntries = MainMenuDataManager.Instance.CharacterEntries;
        if (characterEntries != null)
        {
            foreach (var entry in characterEntries)
            {
                if (entry == null) continue;
                Sprite avatarSprite = entry.Avatar;
                if (avatarSprite == null)
                {
                    // Thử load dự phòng từ Resources
                    avatarSprite = Resources.Load<Sprite>("Avatars/" + entry.Id);
                }

                if (avatarSprite != null && !addedSprites.Contains(avatarSprite))
                {
                    string avatarId = "cast_" + entry.Id;
                    CreateSelectionButton(avatarId, avatarSprite);
                    addedSprites.Add(avatarSprite);
                }
            }
        }

        // 2. Lấy tất cả ảnh của các Nhạc cụ (Instruments)
        var instrumentEntries = MainMenuDataManager.Instance.InstrumentEntries;
        if (instrumentEntries != null)
        {
            foreach (var entry in instrumentEntries)
            {
                if (entry == null) continue;
                Sprite avatarSprite = entry.Avatar;
                if (avatarSprite == null)
                {
                    // Thử load dự phòng từ Resources
                    avatarSprite = MainMenuDataManager.Instance.GetInstrumentAvatarSprite(entry.Id);
                }

                if (avatarSprite != null && !addedSprites.Contains(avatarSprite))
                {
                    string avatarId = "inst_" + entry.Id;
                    CreateSelectionButton(avatarId, avatarSprite);
                    addedSprites.Add(avatarSprite);
                }
            }
        }
    }

    private async void PopulateCountryFlags()
    {
        ClearInstantiatedButtons();

        int version = _populateVersion;
        if (scrollContent == null || avatarFlagButtonPrefab == null || MainMenuDataManager.Instance == null)
        {
            Debug.LogWarning("[AvatarPopup] Thiáº¿u cáº¥u hÃ¬nh UI hoáº·c MainMenuDataManager chÆ°a sáºµn sÃ ng.");
            return;
        }

        IReadOnlyList<MainMenuPrefabCatalog.CountryFlagEntry> flagEntries = MainMenuDataManager.Instance.CountryFlagEntries;
        if (flagEntries == null || flagEntries.Count == 0)
        {
            Debug.LogWarning("[AvatarPopup] Country Flag Catalog chÆ°a cÃ³ entry nÃ o.");
            return;
        }

        HashSet<string> addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < flagEntries.Count; i++)
        {
            MainMenuPrefabCatalog.CountryFlagEntry entry = flagEntries[i];
            string flagKey = entry != null ? entry.Key : "";
            if (entry == null || string.IsNullOrWhiteSpace(flagKey) || !addedIds.Add(flagKey))
            {
                continue;
            }

            Sprite flagSprite = await MainMenuDataManager.Instance.LoadCountryFlagSpriteByKeyAsync(flagKey);
            if (version != _populateVersion || this == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (flagSprite != null)
            {
                CreateSelectionButton(flagKey, flagSprite);
            }
        }
    }

    private void CreateSelectionButton(string itemId, Sprite itemSprite)
    {
        Button btnObj = Instantiate(avatarFlagButtonPrefab, scrollContent);
        btnObj.gameObject.SetActive(true);
        btnObj.name = _currentMode == PickerMode.Flag ? $"Btn_Flag_{itemId}" : $"Btn_Avatar_{itemId}";

        // Tìm Image component để gán Sprite
        Image img = ResolveSpriteImage(btnObj);

        if (img != null)
        {
            img.sprite = itemSprite;
            img.enabled = true;
        }

        btnObj.onClick.RemoveAllListeners();
        btnObj.onClick.AddListener(() => {
            _onAvatarSelected?.Invoke(itemId, itemSprite);
            ClosePopup();
        });

        _instantiatedButtons.Add(btnObj.gameObject);
    }

    private static Image ResolveSpriteImage(Button button)
    {
        if (button == null) return null;

        Image targetImage = button.targetGraphic as Image;
        Image rootImage = button.GetComponent<Image>();
        Image[] images = button.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image != null && image.gameObject != button.gameObject && image != targetImage)
            {
                return image;
            }
        }

        foreach (Image image in images)
        {
            if (image != null && image.gameObject != button.gameObject)
            {
                return image;
            }
        }

        if (targetImage != null) return targetImage;
        return rootImage;
    }

    private void ClearInstantiatedButtons()
    {
        foreach (var btn in _instantiatedButtons)
        {
            if (btn != null)
            {
                Destroy(btn);
            }
        }
        _instantiatedButtons.Clear();
    }
}
