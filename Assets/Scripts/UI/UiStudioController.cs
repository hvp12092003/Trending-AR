using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Điều khiển toàn bộ UI bên trong Studio Panel của Main Menu.
/// 
/// Kiến trúc:
///   • Hai nút toggle (Cast / Band) chuyển đổi giữa 2 ScrollView.
///   • ScrollView "Cast"  → populate bằng castPrefab   (CastButtonUI hoặc CharacterItemUI).
///   • ScrollView "Band"  → populate bằng purchasedPrefab / createdPrefab (TemplateItemUI).
///   • Tất cả transition dùng DOTween (fade + slide) nhất quán với AccountUIController.
/// </summary>
public class UiStudioController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Tab Toggle Buttons")]
    [Tooltip("Nút chuyển sang tab Cast (Nhân vật)")]
    [SerializeField] private Button castTabButton;

    [Tooltip("Nút chuyển sang tab Band (Templates)")]
    [SerializeField] private Button bandTabButton;

    [Header("Tab Texts")]
    [Tooltip("Text hiển thị trên nút Cast")]
    [SerializeField] private TMP_Text castTabText;

    [Tooltip("Text hiển thị trên nút Band")]
    [SerializeField] private TMP_Text bandTabText;



    [Header("Tab Background Sprites")]
    [Tooltip("Sprite của nút khi được chọn")]
    [SerializeField] private Sprite selectedTabSprite;

    [Tooltip("Sprite của nút khi không được chọn")]
    [SerializeField] private Sprite unselectedTabSprite;

    [Header("Tab Text Colors")]
    [SerializeField] private Color selectedTextColor = Color.black;
    [SerializeField] private Color unselectedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("ScrollView Containers")]
    [Tooltip("Root GameObject chứa ScrollView của tab Cast")]
    [SerializeField] private GameObject castScrollView;

    [Tooltip("Root GameObject chứa ScrollView của tab Band")]
    [SerializeField] private GameObject bandScrollView;

    [Header("Casts ScrollView")]
    [Tooltip("Content Transform bên trong ScrollView Cast")]
    [SerializeField] private Transform castContent;

    [Tooltip("Prefab của từng ô Cast (CastButtonUI hoặc CharacterItemUI)")]
    [SerializeField] private GameObject castPrefab;

    [Header("Bands ScrollView")]

    [Tooltip("Content Transform của danh sách Band tự tạo")]
    [SerializeField] private Transform createdContent;

    [Tooltip("Prefab ô nút bấm Ban nhạc (BandButtonUI)")]
    [SerializeField] private GameObject bandPrefab;

    [Header("Empty State Labels")]
    [Tooltip("Text hiển thị khi chưa có Cast nào")]
    [SerializeField] private GameObject emptyCastLabel;

    [Tooltip("Text hiển thị khi chưa có Band nào")]
    [SerializeField] private GameObject emptyBandLabel;

    [Header("Loading Indicator")]
    [Tooltip("Spinner hoặc overlay loading nhỏ bên trong Studio Panel (tuỳ chọn)")]
    [SerializeField] private GameObject loadingIndicator;

    [Header("Avatar Settings")]
    [Tooltip("Danh sách Sprite avatar của nhân vật. Tên Sprite trùng với prefabName của nhân vật.")]
    [SerializeField] private List<Sprite> avatarSprites = new List<Sprite>();

    [Header("DOTween Transition Settings")]
    [SerializeField] private float tabTransitionDuration  = 0.2f;
    [SerializeField] private float viewTransitionDuration = 0.25f;
    [SerializeField] private float viewSlideOffset        = 30f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private enum StudioTab { Cast, Band }
    private StudioTab _currentTab = StudioTab.Cast;

    private bool _isLoading = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Đăng ký sự kiện cho các nút tab
        if (castTabButton != null)
            castTabButton.onClick.AddListener(() => SwitchTab(StudioTab.Cast));

        if (bandTabButton != null)
            bandTabButton.onClick.AddListener(() => SwitchTab(StudioTab.Band));
    }

    private void OnEnable()
    {
        // Mỗi khi Studio Panel được bật, tải lại dữ liệu mới nhất
        RefreshAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — MainMenuController có thể gọi trực tiếp
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tải lại toàn bộ dữ liệu Casts + Bands và làm mới giao diện.
    /// </summary>
    public async void RefreshAll()
    {
        if (_isLoading) return;
        _isLoading = true;

        // Cập nhật giao diện tab được chọn ngay lập tức trước khi tải dữ liệu
        ApplyTabVisual(_currentTab, instant: true);
        SetLoadingState(true);

        await LoadCastsAsync();
        await LoadBandsAsync();

        SetLoadingState(false);

        // Đảm bảo hiển thị đúng tab đang chọn sau khi load xong
        ApplyTabVisual(_currentTab, instant: true);

        _isLoading = false;
    }

    /// <summary>
    /// Chuyển sang tab chỉ định từ code (ví dụ: deep link sau khi tạo nhân vật mới).
    /// </summary>
    public void ShowCastTab() => SwitchTab(StudioTab.Cast);

    /// <summary>
    /// Chuyển sang tab Band từ code.
    /// </summary>
    public void ShowBandTab() => SwitchTab(StudioTab.Band);

    // ─────────────────────────────────────────────────────────────────────────
    // Tab Switching
    // ─────────────────────────────────────────────────────────────────────────

    private void SwitchTab(StudioTab tab)
    {
        if (tab == _currentTab) return;
        _currentTab = tab;
        ApplyTabVisual(tab);
    }

    /// <summary>
    /// Cập nhật trạng thái visual của các nút tab và điều khiển hiển thị hai ScrollView.
    /// </summary>
    private void ApplyTabVisual(StudioTab tab, bool instant = false)
    {
        bool isCast = (tab == StudioTab.Cast);

        // Lấy Image trực tiếp từ Button Component của Tab
        Image castImg = castTabButton != null ? castTabButton.GetComponent<Image>() : null;
        Image bandImg = bandTabButton != null ? bandTabButton.GetComponent<Image>() : null;

        // ── Màu chữ & Background Sprite của nút tab ──────────────────────────────────────
        AnimateTabButton(castTabText, castImg, isCast, instant);
        AnimateTabButton(bandTabText, bandImg, !isCast, instant);

        // ── Hiệu ứng scale nút ──────────────────────────────────────────────────────────
        ScaleButton(castTabButton, isCast, instant);
        ScaleButton(bandTabButton, !isCast, instant);

        // ── Chỉ bật/tắt (SetActive) để giữ nguyên vị trí, tỉ lệ và layout thiết kế gốc ──
        if (castScrollView != null)
            castScrollView.SetActive(isCast);

        if (bandScrollView != null)
            bandScrollView.SetActive(!isCast);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: Lấy Sprite đại diện cho nhân vật dựa trên prefabName
    // ─────────────────────────────────────────────────────────────────────────
    
    private Sprite GetAvatarSprite(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        // 1. Tìm trong danh sách Inspector
        if (avatarSprites != null)
        {
            foreach (var sprite in avatarSprites)
            {
                if (sprite != null && sprite.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }
        }

        // 2. Fallback load từ thư mục Resources/Avatars/
        return Resources.Load<Sprite>("Avatars/" + prefabName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data Loading
    // ─────────────────────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task LoadCastsAsync()
    {
        ClearChildren(castContent);

        if (MainMenuDataManager.Instance == null) return;

        var characters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();

        bool isEmpty = characters == null || characters.Count == 0;
        SetEmptyLabel(emptyCastLabel, isEmpty);

        if (!isEmpty && castPrefab != null && castContent != null)
        {
            foreach (var charData in characters)
            {
                var itemObj = Instantiate(castPrefab, castContent);
                
                // Kiểm tra xem prefab sử dụng CastButtonUI hay CharacterItemUI cũ
                var castUI = itemObj.GetComponent<CastButtonUI>();
                if (castUI != null)
                {
                    Sprite avatar = GetAvatarSprite(charData.prefabName);
                    CastData castData = new CastData(charData.name, charData.prefabName, charData.instrumentId, charData.danceAnimId, charData.danceAnimIds);
                    castUI.Setup(castData, avatar);

                    if (castUI.DeleteButton != null)
                    {
                        castUI.DeleteButton.gameObject.SetActive(true);
                        castUI.DeleteButton.onClick.RemoveAllListeners();
                        castUI.DeleteButton.onClick.AddListener(async () =>
                        {
                            bool success = await MainMenuDataManager.Instance.DeleteCharacterAsync(charData.characterId);
                            if (success)
                            {
                                RefreshAll();
                            }
                        });
                    }
                }
                else
                {
                    var itemUI = itemObj.GetComponent<CharacterItemUI>();
                    if (itemUI != null)
                    {
                        itemUI.Setup(charData);

                        if (itemUI.DeleteButton != null)
                        {
                            itemUI.DeleteButton.gameObject.SetActive(true);
                            itemUI.DeleteButton.onClick.RemoveAllListeners();
                            itemUI.DeleteButton.onClick.AddListener(async () =>
                            {
                                bool success = await MainMenuDataManager.Instance.DeleteCharacterAsync(charData.characterId);
                                if (success)
                                {
                                    RefreshAll();
                                }
                            });
                        }
                    }
                }

                // Micro-animation entrance
                AnimateItemEntrance(itemObj);
            }
        }
    }

    private async System.Threading.Tasks.Task LoadBandsAsync()
    {
        ClearChildren(createdContent);

        if (MainMenuDataManager.Instance == null) return;

        // Tải danh sách Ban nhạc (BandButtonUI)
        if (bandPrefab != null && createdContent != null)
        {
            var characters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();
            int bandCount = 0;

            if (characters != null && characters.Count > 0)
            {
                // Nhóm các nhân vật đã tạo thành các ban nhạc (tối đa 4 nhân vật mỗi ban nhạc)
                for (int i = 0; i < characters.Count; i += 4)
                {
                    List<CastData> bandCasts = new List<CastData>();
                    List<Sprite> bandAvatars = new List<Sprite>();

                    for (int j = 0; j < 4; j++)
                    {
                        int index = i + j;
                        if (index < characters.Count)
                        {
                            var charData = characters[index];
                            bandCasts.Add(new CastData(charData.name, charData.prefabName, charData.instrumentId, charData.danceAnimId, charData.danceAnimIds));
                            bandAvatars.Add(GetAvatarSprite(charData.prefabName));
                        }
                    }

                    if (bandCasts.Count > 0)
                    {
                        bandCount++;
                        BandData bandData = new BandData(bandCasts);
                        
                        var itemObj = Instantiate(bandPrefab, createdContent);
                        var bandUI = itemObj.GetComponent<BandButtonUI>();
                        if (bandUI != null)
                        {
                            bandUI.Setup(bandData, bandAvatars, $"Ban nhạc của tôi {bandCount}");
                        }
                        
                        AnimateItemEntrance(itemObj);
                    }
                }
            }

            bool noBands = (bandCount == 0);
            SetEmptyLabel(emptyBandLabel, noBands);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DOTween Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowView(GameObject view, bool fromRight, bool instant)
    {
        if (view == null) return;

        view.SetActive(true);

        CanvasGroup cg = GetOrAddCanvasGroup(view);
        cg.interactable    = true;
        cg.blocksRaycasts  = true;

        if (instant)
        {
            cg.alpha = 1f;
            view.transform.localPosition = Vector3.zero;
            return;
        }

        float startX = fromRight ? viewSlideOffset : -viewSlideOffset;

        cg.DOKill();
        view.transform.DOKill();

        cg.alpha = 0f;
        view.transform.localPosition = new Vector3(startX, 0f, 0f);

        cg.DOFade(1f, viewTransitionDuration).SetEase(Ease.OutQuad);
        view.transform.DOLocalMoveX(0f, viewTransitionDuration).SetEase(Ease.OutCubic);
    }

    private void HideView(GameObject view, bool toLeft, bool instant)
    {
        if (view == null) return;

        CanvasGroup cg = GetOrAddCanvasGroup(view);
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        if (instant)
        {
            cg.alpha = 0f;
            view.SetActive(false);
            return;
        }

        float endX = toLeft ? -viewSlideOffset : viewSlideOffset;

        cg.DOKill();
        view.transform.DOKill();

        cg.DOFade(0f, viewTransitionDuration).SetEase(Ease.InQuad);
        view.transform.DOLocalMoveX(endX, viewTransitionDuration).SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                view.SetActive(false);
                view.transform.localPosition = Vector3.zero;
            });
    }

    private void AnimateTabButton(TMP_Text label, Image bg, bool isSelected, bool instant)
    {
        Color textColor = isSelected ? selectedTextColor : unselectedTextColor;
        Sprite targetSprite = isSelected ? selectedTabSprite : unselectedTabSprite;

        if (label != null)
        {
            if (instant) label.color = textColor;
            else
            {
                label.DOKill();
                label.DOColor(textColor, tabTransitionDuration);
            }
        }

        if (bg != null)
        {
            if (targetSprite != null)
            {
                bg.sprite = targetSprite;
            }

            // Giữ nguyên màu trắng để hiển thị đúng màu của Sprite gốc
            Color targetColor = Color.white;
            if (instant) bg.color = targetColor;
            else
            {
                bg.DOKill();
                bg.DOColor(targetColor, tabTransitionDuration);
            }
        }
    }

    private void ScaleButton(Button btn, bool isSelected, bool instant)
    {
        if (btn == null) return;
        
        btn.transform.DOKill();
        float targetScale = isSelected ? 1.05f : 1f;
        if (instant)
        {
            btn.transform.localScale = Vector3.one * targetScale;
        }
        else
        {
            btn.transform.DOScale(targetScale, tabTransitionDuration).SetEase(Ease.OutBack);
        }
    }

    private static void AnimateItemEntrance(GameObject item)
    {
        if (item == null) return;

        CanvasGroup cg = item.GetComponent<CanvasGroup>();
        if (cg == null) cg = item.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        item.transform.localScale = Vector3.one * 0.85f;

        cg.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
        item.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
    }

    private void SetLoadingState(bool loading)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(loading);
    }

    private static void SetEmptyLabel(GameObject label, bool visible)
    {
        if (label != null) label.SetActive(visible);
    }

    private static void ClearChildren(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
            Destroy(child.gameObject);
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}
