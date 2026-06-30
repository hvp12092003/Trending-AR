using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý giao diện người dùng cho chế độ Band (Band Mode).
/// Điều khiển quay lại Menu chính, mở popup chọn ban nhạc và ẩn/hiện UI để quay phim.
/// </summary>
public class BandPanelController : MonoBehaviour
{
    [Header("UI Panels Division")]
    [SerializeField] private GameObject bandPanel;   // Panel chọn Ban nhạc
    [SerializeField] private GameObject uiAr;        // Panel UI AR

    [Header("Panel Navigation")]
    [SerializeField] private Button backButton;

    [Header("UI Hide/Show Settings")]
    [SerializeField] private Button hideUiButton;
    [SerializeField] private Button showUiButton;
    [SerializeField] private Button refreshButton; // Nút Refresh đồng bộ Casts
    [SerializeField] private List<GameObject> arUiPanels = new List<GameObject>(); // Danh sách các UI AR cần ẩn khi quay màn hình

    [Header("Transition Settings (Black Screen Overlay)")]
    [SerializeField] private bool useBlackScreenTransition = false;
    [SerializeField] private Color transitionColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.25f;

    [Header("Transition Settings (Direct UI Fade & Scale)")]
    [SerializeField] private float uiFadeDuration = 0.35f;
    [SerializeField] private Ease uiFadeEase = Ease.OutQuad;
    [SerializeField] private Ease uiShowEase = Ease.OutBack;

    private Dictionary<GameObject, bool> m_UiActiveStates = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, Vector3> m_UiOriginalScales = new Dictionary<GameObject, Vector3>();
    private Image m_FadeOverlay;
    private bool m_Initialized = false;
    private bool m_IsGameplayUiHidden = false;

    public bool IsArUiActive => uiAr != null && uiAr.activeSelf;
    public bool IsUiHidden => m_IsGameplayUiHidden || (showUiButton != null && showUiButton.gameObject.activeSelf);

    public void InitializeIfNeeded()
    {
        if (m_Initialized) return;
        m_Initialized = true;

        // Khởi tạo overlay chớp đen nếu cấu hình
        if (useBlackScreenTransition)
        {
            CreateFadeOverlay();
        }

        // Tự động tìm kiếm các tham chiếu nếu chưa được gán
        AutoAssignReferences();

        // Đăng ký sự kiện nút Back
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ClosePanel);
            backButton.onClick.AddListener(ClosePanel);
        }


        // Đăng ký sự kiện nút Ẩn UI
        if (hideUiButton != null)
        {
            hideUiButton.onClick.RemoveListener(HideUI);
            hideUiButton.onClick.AddListener(HideUI);
        }

        // Đăng ký sự kiện nút Hiện UI
        if (showUiButton != null)
        {
            showUiButton.onClick.RemoveListener(ShowUI);
            showUiButton.onClick.AddListener(ShowUI);

            // Mặc định ẩn showUiButton lúc ban đầu
            showUiButton.gameObject.SetActive(false);
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();
            cgShow.alpha = 0f;
            showUiButton.transform.localScale = Vector3.zero;
        }

        // Đăng ký sự kiện nút Refresh
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshCasts);
            refreshButton.onClick.AddListener(RefreshCasts);
        }
    }

    private void Start()
    {
        Debug.Log("[BandPanelController] Khởi tạo giao diện Band Mode.");
        InitializeIfNeeded();
    }

    private void ClosePanel()
    {
        // ── Phân biệt ngữ cảnh: đang ở Band Selection UI hay đang trải nghiệm AR? ──
        bool isInArMode = (uiAr != null && uiAr.activeSelf) ||
                          (bandPanel != null && !bandPanel.activeSelf);

        if (isInArMode)
        {
            // Đang ở chế độ AR → Back-from-AR: dọn Cast + hiện lại UI Band
            BackFromArToBandUI();
        }
        else
        {
            // Đang ở màn hình Band Selection → Về Main Menu như cũ
            ARFallbackManager.ReleaseDeviceCamera();

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("Main Menu Scene");
            }
            else
            {
                SceneManager.LoadScene("Main Menu Scene");
            }
        }
    }

    /// <summary>
    /// Xử lý Back từ chế độ AR về lại giao diện chọn Ban nhạc trong cùng Scene:
    /// 1. Dọn sạch Cast đã spawn trong AR và reset các bệ đứng.
    /// 2. Ẩn UI AR, hiện lại Band Selection Panel để người dùng chọn lại ban nhạc.
    /// 3. Sau khi chọn xong, SpawnBand() sẽ tự cập nhật Cast mới trên bệ.
    /// </summary>
    private void BackFromArToBandUI()
    {
        Debug.Log("[BandPanelController] Back-from-AR: Dọn Cast và hiện lại UI Band.");
        ARFallbackManager.ReleaseDeviceCamera();

        // 1. Dọn sạch Cast trong AR và reset bệ đứng
        BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
        spawner = FindAnyObjectByType<BandARSpawner>();
#else
        spawner = FindObjectOfType<BandARSpawner>();
#endif
        if (spawner != null)
        {
            spawner.DestroyAllAndResetPedestals();
            spawner.SetPedestalsAndUnplacedCastsActive(false);
        }

        // Clear the previous selection before showing the picker again.
        BandSelectionManager.ClearSelection();
        if (MainMenuDataManager.Instance != null)
        {
            MainMenuDataManager.Instance.selectedBandData = null;
        }

        // Khôi phục tất cả UI AR đã ẩn về trạng thái hiển thị mặc định và reset bộ nhớ trạng thái
        m_UiActiveStates.Clear();
        ResetGameplayUiHiddenState();

        // 2. Ẩn UI AR với hiệu ứng fade out
        if (uiAr != null)
        {
            CanvasGroup cgAr = uiAr.GetComponent<CanvasGroup>();
            if (cgAr == null) cgAr = uiAr.AddComponent<CanvasGroup>();

            cgAr.DOKill();
            uiAr.transform.DOKill();
            cgAr.interactable = false;
            cgAr.blocksRaycasts = false;
            cgAr.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase).OnComplete(() =>
            {
                uiAr.SetActive(false);
            });
        }

        // 3. Hiện lại Band Selection Panel với hiệu ứng fade in
        if (bandPanel != null)
        {
            CanvasGroup cgBand = bandPanel.GetComponent<CanvasGroup>();
            if (cgBand == null) cgBand = bandPanel.AddComponent<CanvasGroup>();

            bandPanel.SetActive(true);
            cgBand.alpha = 0f;
            bandPanel.transform.localScale = Vector3.one * 0.95f;
            cgBand.interactable = false;
            cgBand.blocksRaycasts = false;

            cgBand.DOKill();
            bandPanel.transform.DOKill();
            cgBand.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
            bandPanel.transform.DOScale(1f, uiFadeDuration + 0.05f).SetEase(uiShowEase).OnComplete(() =>
            {
                cgBand.interactable = true;
                cgBand.blocksRaycasts = true;
            });
        }
        else
        {
            // Fallback: Không có bandPanel → Hiện trực tiếp popup chọn band
            if (spawner != null)
            {
                spawner.ShowBandSelectionUI();
            }
        }
    }

    private void ShowBandSelectionPopup()
    {
        BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
        spawner = FindAnyObjectByType<BandARSpawner>();
#else
        spawner = FindObjectOfType<BandARSpawner>();
#endif
        if (spawner != null)
        {
            spawner.ShowBandSelectionUI();
        }
        else
        {
            Debug.LogWarning("[BandPanelController] Không tìm thấy BandARSpawner để hiển thị UI chọn ban nhạc!");
        }
    }

    private void TogglePedestals()
    {
        Debug.Log("[BandPanelController] Manual pedestal toggle is disabled. Pedestals hide automatically after 4 Casts are placed.");
    }

    public void HideUI()
    {
        InitializeIfNeeded();
        if (useBlackScreenTransition)
        {
            RunTransition(HideUI_Internal);
        }
        else
        {
            HideUI_Internal();
        }
    }

    private void HideUI_Internal()
    {
        m_IsGameplayUiHidden = true;

        BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
        spawner = FindAnyObjectByType<BandARSpawner>();
#else
        spawner = FindObjectOfType<BandARSpawner>();
#endif
        if (spawner != null)
        {
            spawner.SetPedestalsAndUnplacedCastsActive(false, !useBlackScreenTransition, uiFadeDuration);
        }

        if (arUiPanels == null || arUiPanels.Count == 0)
        {
            Debug.LogWarning("[BandPanelController] Không có phần tử UI nào được thiết lập để ẩn!");
            return;
        }

        m_UiActiveStates.Clear();

        foreach (var uiObj in arUiPanels)
        {
            if (uiObj == null) continue;

            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            if (!m_UiOriginalScales.ContainsKey(uiObj))
            {
                m_UiOriginalScales[uiObj] = uiObj.transform.localScale;
            }

            m_UiActiveStates[uiObj] = uiObj.activeSelf;
            
            if (uiObj.activeSelf)
            {
                if (useBlackScreenTransition)
                {
                    uiObj.SetActive(false);
                }
                else
                {
                    CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                    cg.DOKill();
                    uiObj.transform.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;

                    GameObject targetObj = uiObj;
                    Vector3 targetScale = m_UiOriginalScales[uiObj] * 0.95f;
                    cg.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase);
                    uiObj.transform.DOScale(targetScale, uiFadeDuration).SetEase(uiFadeEase).OnComplete(() =>
                    {
                        targetObj.SetActive(false);
                    });
                }
            }
        }

        if (showUiButton != null)
        {
            showUiButton.gameObject.SetActive(true);
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();

            cgShow.DOKill();
            showUiButton.transform.DOKill();
            cgShow.interactable = false;
            cgShow.blocksRaycasts = false;

            if (useBlackScreenTransition)
            {
                cgShow.alpha = 1f;
                showUiButton.transform.localScale = Vector3.one;
                cgShow.interactable = true;
                cgShow.blocksRaycasts = true;
            }
            else
            {
                cgShow.alpha = 0f;
                showUiButton.transform.localScale = Vector3.zero;
                cgShow.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                showUiButton.transform.DOScale(1f, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                {
                    cgShow.interactable = true;
                    cgShow.blocksRaycasts = true;
                });
            }
        }

    }

    public void ShowUI()
    {
        InitializeIfNeeded();
        if (useBlackScreenTransition)
        {
            RunTransition(() => ShowUI_Internal(true, true));
        }
        else
        {
            ShowUI_Internal(true, true);
        }
    }

    private void ShowUI_Internal(bool showPedestals = true, bool animatePedestals = true)
    {
        m_IsGameplayUiHidden = false;

        BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
        spawner = FindAnyObjectByType<BandARSpawner>();
#else
        spawner = FindObjectOfType<BandARSpawner>();
#endif
        if (spawner != null && showPedestals)
        {
            spawner.SetPedestalsAndUnplacedCastsActive(true, !useBlackScreenTransition && animatePedestals, uiFadeDuration);
        }

        if (arUiPanels == null || arUiPanels.Count == 0) return;

        if (showUiButton != null)
        {
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();

            cgShow.DOKill();
            showUiButton.transform.DOKill();
            cgShow.interactable = false;
            cgShow.blocksRaycasts = false;

            if (useBlackScreenTransition)
            {
                showUiButton.gameObject.SetActive(false);
            }
            else
            {
                cgShow.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase);
                showUiButton.transform.DOScale(0f, uiFadeDuration).SetEase(Ease.InBack).OnComplete(() =>
                {
                    showUiButton.gameObject.SetActive(false);
                });
            }
        }

        foreach (var uiObj in arUiPanels)
        {
            if (uiObj == null) continue;

            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            Vector3 originalScale = Vector3.one;
            if (m_UiOriginalScales.TryGetValue(uiObj, out Vector3 savedScale))
            {
                originalScale = savedScale;
            }
            else
            {
                originalScale = uiObj.transform.localScale;
                m_UiOriginalScales[uiObj] = originalScale;
            }

            if (m_UiActiveStates.TryGetValue(uiObj, out bool wasActive))
            {
                if (wasActive)
                {
                    if (useBlackScreenTransition)
                    {
                        uiObj.SetActive(true);
                        uiObj.transform.localScale = originalScale;
                    }
                    else
                    {
                        uiObj.SetActive(true);
                        CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                        if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                        cg.DOKill();
                        uiObj.transform.DOKill();
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                        cg.alpha = 0f;
                        uiObj.transform.localScale = originalScale * 0.95f;

                        cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                        uiObj.transform.DOScale(originalScale, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                        {
                            cg.interactable = true;
                            cg.blocksRaycasts = true;
                        });
                    }
                }
                else
                {
                    uiObj.SetActive(false);
                }
            }
            else
            {
                if (useBlackScreenTransition)
                {
                    uiObj.SetActive(true);
                    uiObj.transform.localScale = originalScale;
                }
                else
                {
                    uiObj.SetActive(true);
                    CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                    cg.DOKill();
                    uiObj.transform.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                    cg.alpha = 0f;
                    uiObj.transform.localScale = originalScale * 0.95f;

                    cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                    uiObj.transform.DOScale(originalScale, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                    {
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    });
                }
            }
        }

    }

    /// <summary>
    /// Thực hiện chuyển cảnh từ giao diện chọn ban nhạc (Band Panel) sang giao diện trải nghiệm AR (UI AR).
    /// </summary>
    public void TransitionToAR()
    {
        InitializeIfNeeded();
        ARFallbackManager.ResumeForCurrentScene();
        ResetGameplayUiHiddenState();

        Debug.Log("[BandPanelController] Bắt đầu chuyển cảnh vào trải nghiệm AR.");

        // Bật canvas chứa BandPanelController lên nếu nó bị tắt
        if (bandPanel != null)
        {
            bandPanel.SetActive(false);
        }

        EnsureTransitionHostActive();

        // Đảm bảo CanvasGroup của UI AR được hiển thị và tương tác bình thường
        if (uiAr != null)
        {
            CanvasGroup cgAr = uiAr.GetComponent<CanvasGroup>();
            if (cgAr == null) cgAr = uiAr.AddComponent<CanvasGroup>();
            cgAr.alpha = 1f;
            cgAr.interactable = true;
            cgAr.blocksRaycasts = true;
        }

        // Reset trạng thái ẩn UI để hiển thị lại đầy đủ khi bắt đầu lượt chơi mới
        m_UiActiveStates.Clear();

        // Tự động tìm/tạo overlay nếu chưa có
        if (m_FadeOverlay == null)
        {
            CreateFadeOverlay();
        }

        // Thực hiện hiệu ứng chuyển cảnh chớp đen
        if (m_FadeOverlay != null)
        {
            RunTransition(() => {
                if (bandPanel != null) bandPanel.SetActive(false);
                if (uiAr != null) uiAr.SetActive(true);

                BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
                spawner = FindAnyObjectByType<BandARSpawner>();
#else
                spawner = FindObjectOfType<BandARSpawner>();
#endif
                if (spawner != null)
                {
                    spawner.UpdateARUI();
                }
                else
                {
                    ShowUI_Internal(true, false);
                }
            });
        }
        else
        {
            // Fallback: Chuyển đổi trực tiếp nếu không có overlay
            if (bandPanel != null) bandPanel.SetActive(false);
            if (uiAr != null) uiAr.SetActive(true);

            BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
            spawner = FindAnyObjectByType<BandARSpawner>();
#else
            spawner = FindObjectOfType<BandARSpawner>();
#endif
            if (spawner != null)
            {
                spawner.UpdateARUI();
            }
            else
            {
                ShowUI_Internal(true, false);
            }
        }
    }

    private void CreateFadeOverlay()
    {
        Canvas canvas = null;
#if UNITY_2023_1_OR_NEWER
        canvas = FindAnyObjectByType<Canvas>();
#else
        canvas = FindObjectOfType<Canvas>();
#endif
        if (canvas != null)
        {
            GameObject overlayObj = new GameObject("UI_FadeOverlay_Dynamic");
            overlayObj.transform.SetParent(canvas.transform, false);
            overlayObj.transform.SetAsLastSibling();

            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            m_FadeOverlay = overlayObj.AddComponent<Image>();
            m_FadeOverlay.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f);
            m_FadeOverlay.raycastTarget = false;
        }
    }

    private void RunTransition(System.Action middleAction)
    {
        if (m_FadeOverlay == null)
        {
            middleAction?.Invoke();
            return;
        }

        m_FadeOverlay.raycastTarget = true;
        m_FadeOverlay.transform.SetAsLastSibling();
        m_FadeOverlay.DOKill();
        
        m_FadeOverlay.DOColor(transitionColor, fadeOutDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                middleAction?.Invoke();

                m_FadeOverlay.DOColor(new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f), fadeInDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        m_FadeOverlay.raycastTarget = false;
                    });
            });
    }

    private void EnsureTransitionHostActive()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && !parentCanvas.gameObject.activeSelf)
        {
            parentCanvas.gameObject.SetActive(true);
        }

        bool controllerIsOnPicker = bandPanel != null &&
            (gameObject == bandPanel || transform.IsChildOf(bandPanel.transform));

        if (!controllerIsOnPicker && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void ResetGameplayUiHiddenState()
    {
        m_IsGameplayUiHidden = false;

        if (showUiButton == null)
        {
            return;
        }

        CanvasGroup cgShow = showUiButton.GetComponent<CanvasGroup>();
        if (cgShow == null)
        {
            cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();
        }

        cgShow.DOKill();
        showUiButton.transform.DOKill();
        cgShow.alpha = 0f;
        cgShow.interactable = false;
        cgShow.blocksRaycasts = false;
        showUiButton.transform.localScale = Vector3.zero;
        showUiButton.gameObject.SetActive(false);
    }

    private void ApplyHiddenGameplayUiState()
    {
        if (arUiPanels != null)
        {
            foreach (GameObject uiObj in arUiPanels)
            {
                if (uiObj == null) continue;
                if (showUiButton != null && uiObj == showUiButton.gameObject) continue;

                CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOKill();
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }

                uiObj.transform.DOKill();
                uiObj.SetActive(false);
            }
        }

        if (showUiButton == null)
        {
            return;
        }

        CanvasGroup cgShow = showUiButton.GetComponent<CanvasGroup>();
        if (cgShow == null)
        {
            cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();
        }

        showUiButton.gameObject.SetActive(true);
        showUiButton.transform.DOKill();
        cgShow.DOKill();
        showUiButton.transform.localScale = Vector3.one;
        cgShow.alpha = 1f;
        cgShow.interactable = true;
        cgShow.blocksRaycasts = true;
    }

    private void AutoAssignReferences()
    {
        // 1. Tự động tìm nút Back
        if (backButton == null)
        {
            GameObject backObj = GameObject.Find("Back Button");
            if (backObj == null) backObj = GameObject.Find("BackButton");
            if (backObj != null)
            {
                backButton = backObj.GetComponent<Button>();
            }
        }


        // 4. Tự động tìm hideUiButton ("Hiden UI")
        if (hideUiButton == null)
        {
            GameObject hideObj = GameObject.Find("Hiden UI");
            if (hideObj != null)
            {
                hideUiButton = hideObj.GetComponent<Button>();
            }
            else
            {
                Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
                foreach (var btn in allButtons)
                {
                    if (btn.gameObject.name == "Hiden UI" || btn.gameObject.name.Contains("HidenUI"))
                    {
                        hideUiButton = btn;
                        break;
                    }
                }
            }
        }

        // 5. Tự động tìm showUiButton ("Show UI Button" hoặc "Show UI")
        if (showUiButton == null)
        {
            Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var btn in allButtons)
            {
                if (btn.gameObject.name == "Show UI Button" || btn.gameObject.name == "Show UI" || btn.gameObject.name.Contains("ShowUI"))
                {
                    showUiButton = btn;
                    break;
                }
            }
        }

        // 5b. Tự động tìm refreshButton ("Refresh Button", "RefreshButton", "Refresh", "refesh")
        if (refreshButton == null)
        {
            Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var btn in allButtons)
            {
                if (btn.gameObject.name.Equals("Refresh Button", System.StringComparison.OrdinalIgnoreCase) || 
                    btn.gameObject.name.Equals("RefreshButton", System.StringComparison.OrdinalIgnoreCase) || 
                    btn.gameObject.name.Equals("Refresh", System.StringComparison.OrdinalIgnoreCase) ||
                    btn.gameObject.name.Equals("refesh", System.StringComparison.OrdinalIgnoreCase) ||
                    btn.gameObject.name.Contains("Refresh") ||
                    btn.gameObject.name.Contains("refesh"))
                {
                    refreshButton = btn;
                    break;
                }
            }
        }

        // 6. Tự động điền arUiPanels từ các con của uiAr (nếu được gán) hoặc các đối tượng UI khác
        if (arUiPanels == null || arUiPanels.Count == 0)
        {
            arUiPanels = new List<GameObject>();
            GameObject mainUiCanvasObj = uiAr;
            if (mainUiCanvasObj == null) mainUiCanvasObj = GameObject.Find("UI AR");
            if (mainUiCanvasObj == null) mainUiCanvasObj = GameObject.Find("UI Band");
            if (mainUiCanvasObj == null) mainUiCanvasObj = GameObject.Find("UI_Band");

            if (mainUiCanvasObj == null)
            {
                Canvas canvas = null;
#if UNITY_2023_1_OR_NEWER
                canvas = FindAnyObjectByType<Canvas>();
#else
                canvas = FindObjectOfType<Canvas>();
#endif
                if (canvas != null)
                {
                    Transform arTrans = canvas.transform.Find("UI AR");
                    if (arTrans == null) arTrans = canvas.transform.Find("UI Band");
                    if (arTrans == null) arTrans = canvas.transform.Find("UI_Band");
                    if (arTrans != null)
                    {
                        mainUiCanvasObj = arTrans.gameObject;
                    }
                }
            }

            if (mainUiCanvasObj != null)
            {
                for (int i = 0; i < mainUiCanvasObj.transform.childCount; i++)
                {
                    Transform child = mainUiCanvasObj.transform.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    arUiPanels.Add(child.gameObject);
                }
                Debug.Log($"[BandPanelController] Tự động thêm {arUiPanels.Count} phần tử con của '{mainUiCanvasObj.name}' vào danh sách ẩn.");
            }
            else if (hideUiButton != null && hideUiButton.transform.parent != null)
            {
                Transform parentTrans = hideUiButton.transform.parent;
                for (int i = 0; i < parentTrans.childCount; i++)
                {
                    Transform child = parentTrans.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    arUiPanels.Add(child.gameObject);
                }
                Debug.Log($"[BandPanelController] Fallback: Tự động thêm {arUiPanels.Count} phần tử con của parent vào danh sách ẩn.");
            }
        }
    }

    /// <summary>
    /// Cập nhật hiển thị giao diện AR UI dựa trên việc đã có Cast nào được thả ra thực tế ảo hay chưa.
    /// </summary>
    public void UpdateARUIBasedOnPlacement(bool hasPlacedCasts)
    {
        InitializeIfNeeded();

        if (IsUiHidden)
        {
            ApplyHiddenGameplayUiState();
            return;
        }

        if (arUiPanels == null || arUiPanels.Count == 0) return;

        foreach (var uiObj in arUiPanels)
        {
            if (uiObj == null) continue;

            // Không bao giờ ẩn nút Back
            if (backButton != null && uiObj == backButton.gameObject)
            {
                uiObj.SetActive(true);
                continue;
            }

            // Bỏ qua nút hiển thị lại UI
            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            if (hasPlacedCasts)
            {
                // Bỏ qua hiển thị Joystick và Scale Slider nếu không có nhân vật nào được chọn
                if (uiObj.GetComponentInChildren<Joystick>(true) != null ||
                    uiObj.GetComponentInChildren<CharacterScaleSlider>(true) != null ||
                    uiObj.GetComponent<Joystick>() != null ||
                    uiObj.GetComponent<CharacterScaleSlider>() != null)
                {
                    bool isAnySelected = CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter != null;
                    if (!isAnySelected)
                    {
                        uiObj.SetActive(false);
                        continue;
                    }
                }

                uiObj.SetActive(true);

                CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }

                if (m_UiOriginalScales.TryGetValue(uiObj, out Vector3 originalScale))
                {
                    uiObj.transform.localScale = originalScale;
                }
                else
                {
                    uiObj.transform.localScale = Vector3.one;
                }
            }
            else
            {
                uiObj.SetActive(false);
            }
        }

        if (!hasPlacedCasts && showUiButton != null)
        {
            showUiButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Reset âm thanh và hoạt ảnh của tất cả các Cast về 0 để bắt đầu chạy cùng nhau.
    /// </summary>
    public void RefreshCasts()
    {
        Debug.Log("[BandPanelController] Người dùng yêu cầu Refresh: reset audio và anim của các Cast về 0.");
        if (BandAudioManager.Instance != null)
        {
            BandAudioManager.Instance.ResetAllAudioAndAnimations();
        }
    }
}
