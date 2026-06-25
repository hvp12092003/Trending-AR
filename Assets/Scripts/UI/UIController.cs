using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện người dùng chính (Bao gồm chức năng Ẩn/Hiện UI để quay màn hình).
/// </summary>
public class UIController : MonoBehaviour
{
    [Header("UI Hide/Show Settings")]
    [SerializeField] private Button hideUiButton;
    [SerializeField] private Button showUiButton;
    [SerializeField] private List<GameObject> uiPanels = new List<GameObject>(); // Danh sách các UI cần ẩn

    [Header("Transition Settings (Black Screen Overlay)")]
    [SerializeField] private bool useBlackScreenTransition = false; // Chọn true nếu muốn chớp đen toàn màn hình
    [SerializeField] private Color transitionColor = new Color(0f, 0f, 0f, 1f); // Màu chuyển cảnh (Mặc định là đen)
    [SerializeField] private float fadeOutDuration = 0.2f; // Thời gian chuyển dần sang màu tối
    [SerializeField] private float fadeInDuration = 0.25f; // Thời gian trả về trong suốt

    [Header("Transition Settings (Direct UI Fade & Scale)")]
    [SerializeField] private float uiFadeDuration = 0.35f; // Thời gian mờ/hiện giao diện
    [SerializeField] private Ease uiFadeEase = Ease.OutQuad; // Loại ease khi mờ dần
    [SerializeField] private Ease uiShowEase = Ease.OutBack; // Loại ease khi hiển thị lại (nảy nhẹ)

    // Lưu trữ trạng thái active của các đối tượng trước khi ẩn
    private Dictionary<GameObject, bool> m_UiActiveStates = new Dictionary<GameObject, bool>();
    // Lưu trữ scale ban đầu của các đối tượng trước khi ẩn/hiện
    private Dictionary<GameObject, Vector3> m_UiOriginalScales = new Dictionary<GameObject, Vector3>();
    private Image m_FadeOverlay;

    private void Start()
    {
        Debug.Log("[UIController] Khởi tạo giao diện thành công.");

        // Tự động tạo lớp phủ chuyển cảnh (chỉ tạo nếu chọn chế độ chớp đen)
        if (useBlackScreenTransition)
        {
            CreateFadeOverlay();
        }

        // Tự động tìm kiếm nếu chưa được gán trong Inspector
        AutoAssignReferences();

        // Đăng ký sự kiện
        if (hideUiButton != null)
        {
            hideUiButton.onClick.AddListener(HideUI);
            Debug.Log("[UIController] Đã gắn sự kiện HideUI cho nút: " + hideUiButton.gameObject.name);
        }
        else
        {
            Debug.LogWarning("[UIController] Không tìm thấy nút Hiden UI để gắn sự kiện!");
        }

        if (showUiButton != null)
        {
            showUiButton.onClick.AddListener(ShowUI);
            // Mặc định ẩn nút Show UI khi bắt đầu game
            showUiButton.gameObject.SetActive(false);
            
            // Thiết lập alpha và scale ban đầu của showUiButton để tránh bị nháy hình khi bắt đầu hiển thị
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();
            cgShow.alpha = 0f;
            showUiButton.transform.localScale = Vector3.zero;

            Debug.Log("[UIController] Đã gắn sự kiện ShowUI cho nút: " + showUiButton.gameObject.name);
        }
        else
        {
            Debug.LogWarning("[UIController] Không tìm thấy nút Show UI để gắn sự kiện!");
        }
    }

    private void CreateFadeOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            GameObject overlayObj = new GameObject("UI_FadeOverlay_Dynamic");
            overlayObj.transform.SetParent(canvas.transform, false);
            overlayObj.transform.SetAsLastSibling(); // Đưa lên hiển thị trên cùng

            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            m_FadeOverlay = overlayObj.AddComponent<Image>();
            m_FadeOverlay.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f); // Bắt đầu bằng trong suốt
            m_FadeOverlay.raycastTarget = false; // Tránh chặn tương tác click khi không hoạt động
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
        m_FadeOverlay.transform.SetAsLastSibling(); // Luôn đảm bảo đè lên toàn bộ UI khác
        m_FadeOverlay.DOKill();
        
        // Chuyển sang màn tối màu transitionColor
        m_FadeOverlay.DOColor(transitionColor, fadeOutDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // Thực thi hành động ẩn/hiện UI ở giữa lúc màn hình đang tối hoàn toàn
                middleAction?.Invoke();

                // Trả màn hình về trạng thái trong suốt
                m_FadeOverlay.DOColor(new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f), fadeInDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        m_FadeOverlay.raycastTarget = false;
                    });
            });
    }

    private void AutoAssignReferences()
    {
        // 1. Tự động tìm hideUiButton ("Hiden UI")
        if (hideUiButton == null)
        {
            GameObject hideObj = GameObject.Find("Hiden UI");
            if (hideObj != null)
            {
                hideUiButton = hideObj.GetComponent<Button>();
            }
            else
            {
                // Thử tìm trong tất cả các button kể cả đang ẩn
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

        // 2. Tự động tìm showUiButton ("Show UI Button" hoặc "Show UI")
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

        // 3. Tự động điền uiPanels ("UI AR" children) nếu danh sách trống
        if (uiPanels == null || uiPanels.Count == 0)
        {
            uiPanels = new List<GameObject>();
            GameObject mainUiAr = GameObject.Find("UI AR");
            if (mainUiAr == null)
            {
                // Tìm kiếm gián tiếp qua Canvas
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform arTrans = canvas.transform.Find("UI AR");
                    if (arTrans != null)
                    {
                        mainUiAr = arTrans.gameObject;
                    }
                }
            }

            if (mainUiAr != null)
            {
                // Thêm toàn bộ các con trực tiếp của UI AR vào danh sách ẩn
                for (int i = 0; i < mainUiAr.transform.childCount; i++)
                {
                    Transform child = mainUiAr.transform.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    uiPanels.Add(child.gameObject);
                }
                Debug.Log($"[UIController] Tự động thêm {uiPanels.Count} phần tử con của 'UI AR' vào danh sách ẩn.");
            }
            else if (hideUiButton != null && hideUiButton.transform.parent != null)
            {
                // Fallback: Thêm các con của parent hideUiButton
                Transform parentTrans = hideUiButton.transform.parent;
                for (int i = 0; i < parentTrans.childCount; i++)
                {
                    Transform child = parentTrans.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    uiPanels.Add(child.gameObject);
                }
                Debug.Log($"[UIController] Fallback: Tự động thêm {uiPanels.Count} phần tử con của parent vào danh sách ẩn.");
            }
        }
    }

    /// <summary>
    /// Ẩn toàn bộ UI được cấu hình trong danh sách ngoại trừ nút Show UI
    /// </summary>
    public void HideUI()
    {
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
        if (uiPanels == null || uiPanels.Count == 0)
        {
            Debug.LogWarning("[UIController] Không có phần tử UI nào được thiết lập để ẩn!");
            return;
        }

        m_UiActiveStates.Clear();

        foreach (var uiObj in uiPanels)
        {
            if (uiObj == null) continue;

            // Bỏ qua nút showUiButton để nó có thể hiển thị độc lập
            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            // Lưu scale ban đầu nếu chưa lưu (chỉ lưu 1 lần duy nhất để giữ đúng scale gốc)
            if (!m_UiOriginalScales.ContainsKey(uiObj))
            {
                m_UiOriginalScales[uiObj] = uiObj.transform.localScale;
            }

            // Lưu trạng thái hoạt động hiện tại
            m_UiActiveStates[uiObj] = uiObj.activeSelf;
            
            if (uiObj.activeSelf)
            {
                if (useBlackScreenTransition)
                {
                    // Nếu dùng chớp đen, ẩn đột ngột vì đã có màn đen che phủ
                    uiObj.SetActive(false);
                }
                else
                {
                    // Chạy hiệu ứng làm mờ dần và thu nhỏ nhẹ trước khi tắt
                    CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                    cg.DOKill();
                    uiObj.transform.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;

                    GameObject targetObj = uiObj; // Tránh closure scope variable modification
                    Vector3 targetScale = m_UiOriginalScales[uiObj] * 0.95f; // Thu nhỏ 5% scale gốc
                    cg.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase);
                    uiObj.transform.DOScale(targetScale, uiFadeDuration).SetEase(uiFadeEase).OnComplete(() =>
                    {
                        targetObj.SetActive(false);
                    });
                }
            }
        }

        // Hiện nút Show UI lên với hiệu ứng nảy nhẹ
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

        // Ẩn các bệ đứng và Cast chưa đặt trong AR Spawner với transition
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        if (spawner != null)
        {
            spawner.SetPedestalsAndUnplacedCastsActive(false, !useBlackScreenTransition, uiFadeDuration);
        }
    }

    /// <summary>
    /// Hiện lại toàn bộ UI về trạng thái trước đó và ẩn nút Show UI
    /// </summary>
    public void ShowUI()
    {
        if (useBlackScreenTransition)
        {
            RunTransition(ShowUI_Internal);
        }
        else
        {
            ShowUI_Internal();
        }
    }

    private void ShowUI_Internal()
    {
        if (uiPanels == null || uiPanels.Count == 0) return;

        // Ẩn nút Show UI đi với hiệu ứng thu nhỏ
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

        // Khôi phục trạng thái hoạt động của các phần tử
        foreach (var uiObj in uiPanels)
        {
            if (uiObj == null) continue;

            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            // Lấy scale ban đầu của đối tượng (hoặc lưu lại nếu chưa có)
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
                        uiObj.transform.localScale = originalScale; // Khôi phục đúng scale gốc
                    }
                    else
                    {
                        // Kích hoạt lại và fade in nảy nhẹ
                        uiObj.SetActive(true);
                        CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                        if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                        cg.DOKill();
                        uiObj.transform.DOKill();
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                        cg.alpha = 0f;
                        // Bắt đầu từ 95% của scale gốc
                        uiObj.transform.localScale = originalScale * 0.95f;

                        cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                        // Scale về đúng scale gốc thay vì Vector3.one cứng nhắc
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
                // Mặc định bật nếu không lưu trạng thái trước đó
                if (useBlackScreenTransition)
                {
                    uiObj.SetActive(true);
                    uiObj.transform.localScale = originalScale; // Khôi phục đúng scale gốc
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
                    // Bắt đầu từ 95% của scale gốc
                    uiObj.transform.localScale = originalScale * 0.95f;

                    cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                    // Scale về đúng scale gốc thay vì Vector3.one cứng nhắc
                    uiObj.transform.DOScale(originalScale, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                    {
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    });
                }
            }
        }

        // Hiện lại các bệ đứng và Cast chưa đặt trong AR Spawner với transition
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        if (spawner != null)
        {
            spawner.SetPedestalsAndUnplacedCastsActive(true, !useBlackScreenTransition, uiFadeDuration);
        }
    }
}
