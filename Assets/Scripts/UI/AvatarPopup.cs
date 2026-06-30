using System;
using System.Collections.Generic;
using UnityEngine;
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
    [Tooltip("Prefab của button dùng để hiển thị avatar (cần có Image ở root hoặc con, và Button)")]
    [SerializeField] private Button avatarButtonPrefab;

    private Action<string, Sprite> _onAvatarSelected;
    private bool _isOpening = false;
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

    /// <summary>
    /// Đóng Popup với hiệu ứng thu nhỏ và mờ dần.
    /// </summary>
    public void ClosePopup()
    {
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

        if (scrollContent == null || avatarButtonPrefab == null || MainMenuDataManager.Instance == null)
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
                    CreateAvatarButton(avatarId, avatarSprite);
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
                    CreateAvatarButton(avatarId, avatarSprite);
                    addedSprites.Add(avatarSprite);
                }
            }
        }
    }

    private void CreateAvatarButton(string avatarId, Sprite avatarSprite)
    {
        Button btnObj = Instantiate(avatarButtonPrefab, scrollContent);
        btnObj.gameObject.SetActive(true);
        btnObj.name = $"Btn_Avatar_{avatarId}";

        // Tìm Image component để gán Sprite
        Image img = btnObj.GetComponent<Image>();
        if (img == null || img == btnObj.targetGraphic)
        {
            // Nếu Image ở root là background/target graphic của button, thử tìm Image ở các con
            var childImages = btnObj.GetComponentsInChildren<Image>(true);
            foreach (var childImg in childImages)
            {
                if (childImg != null && childImg.gameObject != btnObj.gameObject)
                {
                    img = childImg;
                    break;
                }
            }
        }

        if (img != null)
        {
            img.sprite = avatarSprite;
            img.enabled = true;
        }

        btnObj.onClick.RemoveAllListeners();
        btnObj.onClick.AddListener(() => {
            _onAvatarSelected?.Invoke(avatarId, avatarSprite);
            ClosePopup();
        });

        _instantiatedButtons.Add(btnObj.gameObject);
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
