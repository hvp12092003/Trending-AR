using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

/// <summary>
/// Quản lý giao diện Popup lựa chọn ban nhạc cấu hình sẵn từ Main Menu Hub.
/// Tự sinh giao diện runtime sang trọng nếu chưa có sẵn trong Scene.
/// </summary>
public class BandSelectionPanelUI : MonoBehaviour
{
    public static BandSelectionPanelUI Instance { get; private set; }

    [Header("UI References (Optional)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform containerBox;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private Button closeButton;

    private System.Action<string, string> m_OnSceneLoadTrigger;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Hiển thị Panel chọn Ban nhạc.
    /// </summary>
    /// <param name="loadSceneCallback">Hành động kích hoạt tải Scene (arScene, nonArScene).</param>
    public void Show(System.Action<string, string> loadSceneCallback)
    {
        m_OnSceneLoadTrigger = loadSceneCallback;

        // Nếu chưa được khởi tạo UI từ inspector, tự động tạo UI runtime
        if (panelRoot == null)
        {
            CreateUIAtRuntime();
        }

        panelRoot.SetActive(true);

        // Nạp dữ liệu các ban nhạc cấu hình sẵn
        PopulateBandCards();

        // Hiệu ứng mở panel sang trọng (Fade và Scale Zoom)
        CanvasGroup cg = panelRoot.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.DOFade(1f, 0.25f).SetUpdate(true);
        }

        if (containerBox != null)
        {
            containerBox.localScale = Vector3.one * 0.85f;
            containerBox.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    /// <summary>
    /// Ẩn Panel chọn Ban nhạc.
    /// </summary>
    public void Hide()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        CanvasGroup cg = panelRoot.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0f, 0.2f).SetUpdate(true);
        }

        if (containerBox != null)
        {
            containerBox.DOScale(0.85f, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => panelRoot.SetActive(false));
        }
        else
        {
            panelRoot.SetActive(false);
        }
    }

    private async void PopulateBandCards()
    {
        // Xóa sạch các thẻ card cũ
        if (scrollContent != null)
        {
            foreach (Transform child in scrollContent)
            {
                Destroy(child.gameObject);
            }
        }

        if (MainMenuDataManager.Instance == null)
        {
            Debug.LogError("[BandSelectionPanelUI] MainMenuDataManager.Instance không tồn tại!");
            return;
        }

        List<BandData> bands = await MainMenuDataManager.Instance.GetCreatedBandsAsync();
        if (bands == null || bands.Count == 0)
        {
            GameObject emptyTextObj = new GameObject("Txt_Empty");
            emptyTextObj.transform.SetParent(scrollContent, false);
            
            RectTransform textRect = emptyTextObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(500, 100);

            TextMeshProUGUI txt = emptyTextObj.AddComponent<TextMeshProUGUI>();
            txt.text = "Khong tim thay ban nhac da tao!";
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            return;
        }

        foreach (var band in bands)
        {
            CreateBandCard(band);
        }
    }

    private void CreateBandCard(BandData band)
    {
        string displayName = GetBandDisplayName(band);

        // 1. Tạo Card Container
        GameObject cardObj = new GameObject($"BandCard_{displayName}");
        cardObj.transform.SetParent(scrollContent, false);

        RectTransform rect = cardObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500, 110);

        Image img = cardObj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.18f, 0.9f); // Nền xám đen đặc trưng
        img.type = Image.Type.Simple;

        // Outline bo viền card nhẹ
        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        outline.effectDistance = new Vector2(1, -1);

        Button btn = cardObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnBandSelected(band));

        // Thiết lập hiệu ứng đổi màu tương tác
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;

        // 2. Ảnh Logo ban nhạc bên trái
        GameObject logoObj = new GameObject("Logo");
        logoObj.transform.SetParent(cardObj.transform, false);
        RectTransform logoRect = logoObj.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 0.5f);
        logoRect.anchorMax = new Vector2(0f, 0.5f);
        logoRect.pivot = new Vector2(0f, 0.5f);
        logoRect.anchoredPosition = new Vector2(15f, 0f);
        logoRect.sizeDelta = new Vector2(80, 80);

        Image logoImg = logoObj.AddComponent<Image>();
        logoImg.type = Image.Type.Simple;

        // Nền logo tròn màu ngẫu nhiên dịu mắt
        logoImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        // Gán chữ cái đầu của ban nhạc
        GameObject initialTextObj = new GameObject("InitialText");
        initialTextObj.transform.SetParent(logoObj.transform, false);
        RectTransform itRect = initialTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI initialTxt = initialTextObj.AddComponent<TextMeshProUGUI>();
        initialTxt.text = !string.IsNullOrEmpty(displayName) ? displayName[0].ToString().ToUpper() : "B";
        initialTxt.fontSize = 32;
        initialTxt.fontStyle = FontStyles.Bold;
        initialTxt.alignment = TextAlignmentOptions.Center;
        initialTxt.color = Color.white;

        // 3. Tên ban nhạc
        GameObject nameObj = new GameObject("BandName");
        nameObj.transform.SetParent(cardObj.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(110f, -15f);
        nameRect.sizeDelta = new Vector2(-125f, 25f);

        TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
        nameTxt.text = displayName;
        nameTxt.fontSize = 20;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAlignmentOptions.Left;

        // 4. Layout chứa Avatars nhỏ của 4 Cast
        GameObject avatarContainer = new GameObject("AvatarsContainer");
        avatarContainer.transform.SetParent(cardObj.transform, false);
        RectTransform acRect = avatarContainer.AddComponent<RectTransform>();
        acRect.anchorMin = new Vector2(0f, 0f);
        acRect.anchorMax = new Vector2(1f, 0f);
        acRect.pivot = new Vector2(0f, 0f);
        acRect.anchoredPosition = new Vector2(110f, 12f);
        acRect.sizeDelta = new Vector2(-125f, 32f);

        HorizontalLayoutGroup layout = avatarContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        int castCount = band.casts != null ? Mathf.Min(4, band.casts.Count) : 0;
        for (int i = 0; i < castCount; i++)
        {
            var cast = band.casts[i];
            GameObject avObj = new GameObject($"CastAv_{i}");
            avObj.transform.SetParent(avatarContainer.transform, false);
            RectTransform avRect = avObj.AddComponent<RectTransform>();
            avRect.sizeDelta = new Vector2(30, 30);

            Image avImg = avObj.AddComponent<Image>();
            avImg.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            avImg.type = Image.Type.Simple;

            Sprite avatar = GetCastAvatar(cast);
            if (avatar != null)
            {
                avImg.color = Color.white;
                avImg.sprite = avatar;
            }
        }
    }

    private string GetBandDisplayName(BandData band)
    {
        if (band == null || string.IsNullOrEmpty(band.name))
        {
            return "Band";
        }

        return band.name;
    }

    private Sprite GetCastAvatar(CastData cast)
    {
        if (cast == null || MainMenuDataManager.Instance == null)
        {
            return null;
        }

        Sprite avatar = MainMenuDataManager.Instance.GetCharacterAvatarSprite(cast.prefabName);
        if (avatar != null)
        {
            return avatar;
        }

        return Resources.Load<Sprite>("Avatars/" + cast.prefabName);
    }

    private void OnBandSelected(BandData band)
    {
        if (band == null || band.casts == null || band.casts.Count == 0)
        {
            Debug.LogWarning("[BandSelectionPanelUI] Band duoc chon khong co du lieu cast hop le.");
            return;
        }

        string displayName = GetBandDisplayName(band);
        Debug.Log($"[BandSelectionPanelUI] Dang ky ban nhac duoc chon: {displayName}");

        if (MainMenuDataManager.Instance != null)
        {
            MainMenuDataManager.Instance.selectedBandData = null;
        }

        List<CastData> castsList = new List<CastData>(band.casts);
        List<Sprite> avatarsList = new List<Sprite>();
        foreach (var cast in castsList)
        {
            avatarsList.Add(GetCastAvatar(cast));
        }

        string bandId = string.IsNullOrEmpty(band.bandId) ? displayName : band.bandId;
        BandSelectionManager.SelectedBand = new BandData(bandId, displayName, castsList);
        BandSelectionManager.SelectedBandAvatars = avatarsList;
        BandSelectionManager.SelectedBandName = displayName;

        Hide();

        if (m_OnSceneLoadTrigger != null)
        {
            m_OnSceneLoadTrigger.Invoke("Band Mode AR Scene", "Band Mode NonAR Scene");
        }
    }

    private void CreateUIAtRuntime()
    {
        // 1. Tìm hoặc tạo Canvas chính
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_AutoCreated");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Tạo panelRoot che phủ toàn màn hình
        panelRoot = new GameObject("BandSelectionPanel_Root");
        panelRoot.transform.SetParent(canvas.transform, false);
        
        RectTransform rootRect = panelRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        Image bgImg = panelRoot.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.75f); // Làm mờ nền

        CanvasGroup cg = panelRoot.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // 3. Khung chứa chính (containerBox)
        GameObject containerObj = new GameObject("ContainerBox");
        containerObj.transform.SetParent(panelRoot.transform, false);
        containerBox = containerObj.AddComponent<RectTransform>();
        containerBox.anchorMin = new Vector2(0.5f, 0.5f);
        containerBox.anchorMax = new Vector2(0.5f, 0.5f);
        containerBox.pivot = new Vector2(0.5f, 0.5f);
        containerBox.sizeDelta = new Vector2(550, 650);

        Image boxImg = containerObj.AddComponent<Image>();
        boxImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f); // Glassmorphism dark
        boxImg.type = Image.Type.Simple;

        Outline boxOutline = containerObj.AddComponent<Outline>();
        boxOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        boxOutline.effectDistance = new Vector2(1, -1);

        // 4. Tiêu đề chính
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(containerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);
        titleRect.sizeDelta = new Vector2(-40f, 40f);

        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "CHỌN BAN NHẠC";
        titleTxt.fontSize = 26;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = Color.white;
        titleTxt.alignment = TextAlignmentOptions.Center;

        // 5. ScrollView chứa danh sách các thẻ ban nhạc
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(containerObj.transform, false);
        RectTransform scrollRectTrans = scrollViewObj.AddComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0f, 0f);
        scrollRectTrans.anchorMax = new Vector2(1f, 1f);
        scrollRectTrans.pivot = new Vector2(0.5f, 0.5f);
        scrollRectTrans.anchoredPosition = new Vector2(0f, -40f);
        scrollRectTrans.sizeDelta = new Vector2(-40f, -170f);

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewRect = viewportObj.AddComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.sizeDelta = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewRect;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        scrollContent = contentObj.AddComponent<RectTransform>();
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        scrollContent.anchoredPosition = Vector2.zero;
        scrollContent.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 15f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = scrollContent;

        // 6. Nút quay lại (Close Button)
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(containerObj.transform, false);
        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 25f);
        closeRect.sizeDelta = new Vector2(200, 45);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        closeImg.type = Image.Type.Simple;

        closeButton = closeBtnObj.AddComponent<Button>();
        closeButton.onClick.AddListener(Hide);

        closeButton.transition = Selectable.Transition.ColorTint;
        ColorBlock closeBtnColors = closeButton.colors;
        closeBtnColors.normalColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        closeBtnColors.highlightedColor = new Color(0.32f, 0.32f, 0.36f, 1f);
        closeBtnColors.pressedColor = new Color(0.18f, 0.18f, 0.2f, 1f);
        closeBtnColors.selectedColor = closeBtnColors.normalColor;
        closeButton.colors = closeBtnColors;

        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform ctRect = closeTextObj.AddComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI closeTxt = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "QUAY LẠI";
        closeTxt.fontSize = 18;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
    }
}
