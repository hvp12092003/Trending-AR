using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý thanh điều hướng dưới (Bottom Bar), điều khiển hiển thị 4 panel chính
/// và cập nhật hiệu ứng sáng/tối của các nút tab tương ứng.
/// </summary>
public class BottomBarController : MonoBehaviour
{
    [System.Serializable]
    public class TabItem
    {
        [Tooltip("Nút bấm của Tab")]
        public Button tabButton;

        [Tooltip("Chữ của Tab (Tùy chọn)")]
        public TMP_Text tabText;

        [Tooltip("Icon của Tab (Tùy chọn)")]
        public Image tabIcon;

        [Tooltip("Panel tương ứng với Tab này")]
        public GameObject panel;
    }

    [Header("Tab Configuration")]
    [SerializeField] private TabItem[] tabs;
    [SerializeField] private int defaultTabIndex = 0;

    [Header("Tab Visual Transitions (DOTween)")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float tabTransitionDuration = 0.2f;

    [Space(5)]
    [SerializeField] private bool useTabScaleEffect = true;
    [SerializeField] private float tabScaleIntensity = 1.05f;

    [Header("Panel Transitions (DOTween)")]
    [SerializeField] private float panelTransitionDuration = 0.25f;

    // Sự kiện kích hoạt khi tab thay đổi (gửi kèm index của tab được chọn)
    [HideInInspector] public UnityEvent<int> onTabSelected = new UnityEvent<int>();

    private int currentTabIndex = -1;

    public int CurrentTabIndex => currentTabIndex;

    /// <summary>
    /// Ẩn lập tức toàn bộ các panel của các tab.
    /// </summary>
    public void DeactivateAllPanels()
    {
        if (tabs == null) return;
        foreach (var tab in tabs)
        {
            if (tab != null && tab.panel != null)
            {
                tab.panel.SetActive(false);
            }
        }
    }

    private void Start()
    {
        if (tabs == null || tabs.Length == 0) return;

        // Đăng ký sự kiện click cho từng button trong danh sách tab
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].tabButton != null)
            {
                tabs[i].tabButton.onClick.AddListener(() => SelectTab(index));
            }
        }

        // Kích hoạt tab mặc định ban đầu nếu có quyền hoặc nếu không dùng PermissionPanel
        PermissionPanelController permissionPanel = FindObjectOfType<PermissionPanelController>();
        if (permissionPanel == null || permissionPanel.AreAllPermissionsGranted())
        {
            SelectTab(defaultTabIndex, true);
        }
        else
        {
            DeactivateAllPanels();
        }
    }

    /// <summary>
    /// Chuyển đổi sang Tab được chỉ định theo index.
    /// </summary>
    /// <param name="index">Chỉ số của tab cần chuyển tới</param>
    /// <param name="force">Bắt buộc chuyển kể cả khi đang ở đúng tab đó (dùng khi khởi tạo)</param>
    public void SelectTab(int index, bool force = false)
    {
        if (index < 0 || index >= tabs.Length) return;
        if (index == currentTabIndex && !force) return;

        currentTabIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isSelected = (i == index);
            TabItem tab = tabs[i];

            // 1. Cập nhật trạng thái hiển thị của Tab Button (Chữ và Icon)
            Color targetColor = isSelected ? selectedColor : unselectedColor;

            if (tab.tabText != null)
            {
                tab.tabText.DOKill();
                tab.tabText.DOColor(targetColor, tabTransitionDuration);
            }

            if (tab.tabIcon != null)
            {
                tab.tabIcon.DOKill();
                tab.tabIcon.DOColor(targetColor, tabTransitionDuration);
            }

            // Tạo hiệu ứng phóng to nhẹ tab được chọn
            if (useTabScaleEffect && tab.tabButton != null)
            {
                tab.tabButton.transform.DOKill();
                float targetScale = isSelected ? tabScaleIntensity : 1.0f;
                tab.tabButton.transform.DOScale(targetScale, tabTransitionDuration).SetEase(Ease.OutBack);
            }

            // 2. Điều khiển hiển thị Panel tương ứng với hiệu ứng Fade/Scale
            if (tab.panel != null)
            {
                CanvasGroup cg = tab.panel.GetComponent<CanvasGroup>();
                if (cg == null) cg = tab.panel.AddComponent<CanvasGroup>();

                if (isSelected)
                {
                    tab.panel.SetActive(true);
                    cg.interactable = true;
                    cg.blocksRaycasts = true;

                    cg.DOKill();
                    tab.panel.transform.DOKill();

                    cg.alpha = 0f;
                    tab.panel.transform.localScale = Vector3.one * 0.95f;

                    cg.DOFade(1f, panelTransitionDuration).SetEase(Ease.OutQuad);
                    tab.panel.transform.DOScale(1f, panelTransitionDuration).SetEase(Ease.OutBack);
                }
                else
                {
                    cg.interactable = false;
                    cg.blocksRaycasts = false;

                    cg.DOKill();
                    tab.panel.transform.DOKill();

                    cg.DOFade(0f, panelTransitionDuration).SetEase(Ease.InQuad);
                    tab.panel.transform.DOScale(0.95f, panelTransitionDuration).SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            tab.panel.SetActive(false);
                        });
                }
            }
        }

        // Kích hoạt Event thông báo cho các component khác (như nạp dữ liệu Firebase)
        onTabSelected?.Invoke(index);
    }

    /// <summary>
    /// Hiển thị hoặc ẩn Bottom Bar với hiệu ứng mượt mà (Fade).
    /// </summary>
    public void SetVisible(bool visible, bool instant = false)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();

        if (instant)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
            gameObject.SetActive(visible);
            return;
        }

        if (visible)
        {
            gameObject.SetActive(true);
            cg.interactable = true;
            cg.blocksRaycasts = true;
            
            cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
        }
        else
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            
            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad)
                .OnComplete(() => gameObject.SetActive(false));
        }
    }
}
