using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LeaderboardMenuController : MonoBehaviour
{
    private const int DefaultContentPadding = 24;

    [Header("Components")]
    [SerializeField] private LeaderboardMenuDataProvider dataProvider;
    [SerializeField] private LeaderboardMenuAnimator animator;

    [Header("Scroll View")]
    [SerializeField] private ScrollRect leaderboardScrollRect;
    [SerializeField] private RectTransform leaderboardContent;
    [SerializeField] private GameObject leaderboardItemPrefab;
    [SerializeField] private bool autoCreateMissingScrollView = true;

    [Header("Runtime Layout")]
    [SerializeField] private float rowSpacing = 10f;
    [SerializeField] private RectOffset contentPadding;
    [SerializeField] private bool refreshOnEnable;

    [Header("Player Highlight")]
    [SerializeField] private Sprite playerHighlightSprite;

    private readonly List<LeaderboardItemUI> _rows = new List<LeaderboardItemUI>();
    private int _refreshVersion;
    private static bool _isFirstOpenInSession = true;

    private void Awake()
    {
        EnsureContentPadding();
        EnsureComponents();
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshLeaderboard();
        }
    }

    private void OnDestroy()
    {
        if (animator != null)
        {
            animator.Kill();
        }
    }

    public async void RefreshLeaderboard()
    {
        EnsureComponents();

        if (dataProvider == null || !EnsureScrollView())
        {
            Debug.LogWarning("[LeaderboardMenuController] Missing data provider or scroll view.");
            return;
        }

        int version = ++_refreshVersion;
        LeaderboardSnapshotData snapshot = await dataProvider.GetSnapshotAsync();
        if (version != _refreshVersion || this == null || snapshot == null)
        {
            return;
        }

        int maxPoints = GetMaxPoints(snapshot.previousLeaderboard, snapshot.currentLeaderboard);

        // Tính toán xem điểm số và thứ hạng của người chơi có thay đổi không
        int previousPoints = 0;
        int currentPoints = 0;
        if (snapshot.previousLeaderboard != null)
        {
            for (int i = 0; i < snapshot.previousLeaderboard.Count; i++)
            {
                if (snapshot.previousLeaderboard[i] != null && snapshot.previousLeaderboard[i].userId == snapshot.currentUserId)
                {
                    previousPoints = snapshot.previousLeaderboard[i].points;
                    break;
                }
            }
        }
        if (snapshot.currentLeaderboard != null)
        {
            for (int i = 0; i < snapshot.currentLeaderboard.Count; i++)
            {
                if (snapshot.currentLeaderboard[i] != null && snapshot.currentLeaderboard[i].userId == snapshot.currentUserId)
                {
                    currentPoints = snapshot.currentLeaderboard[i].points;
                    break;
                }
            }
        }

        bool hasRankChanged = snapshot.currentUserRank != snapshot.previousCurrentUserRank;
        bool hasScoreUpdated = currentPoints != previousPoints;
        bool hasUpdates = !snapshot.isFirstView && (hasRankChanged || hasScoreUpdated);

        if (!hasUpdates)
        {
            // KHÔNG CÓ CẬP NHẬT: Vẽ thẳng bảng xếp hạng hiện tại
            RenderLeaderboard(snapshot.currentLeaderboard, maxPoints);
            ForceContentLayout();

            bool shouldScrollFromTop = _isFirstOpenInSession;
            _isFirstOpenInSession = false;

            if (shouldScrollFromTop)
            {
                if (leaderboardScrollRect != null)
                {
                    leaderboardScrollRect.verticalNormalizedPosition = 1f;

                    DOVirtual.DelayedCall(0.25f, () =>
                    {
                        if (this == null || leaderboardScrollRect == null || leaderboardContent == null) return;

                        LeaderboardItemUI playerRow = null;
                        for (int i = 0; i < _rows.Count; i++)
                        {
                            if (_rows[i] != null && _rows[i].UserId == snapshot.currentUserId)
                            {
                                playerRow = _rows[i];
                                break;
                            }
                        }

                        if (playerRow != null)
                        {
                            float contentHeight = leaderboardContent.rect.height;
                            float viewportHeight = leaderboardScrollRect.viewport.rect.height;
                            if (contentHeight > viewportHeight)
                            {
                                float rowTop = Mathf.Abs(playerRow.RectTransform.anchoredPosition.y);
                                float rowHeight = playerRow.RectTransform.rect.height;
                                float rowCenter = rowTop + (rowHeight / 2f);
                                float targetOffset = rowCenter - (viewportHeight / 2f);
                                targetOffset = Mathf.Clamp(targetOffset, 0f, contentHeight - viewportHeight);
                                float targetNormalized = 1f - Mathf.Clamp01(targetOffset / (contentHeight - viewportHeight));

                                leaderboardScrollRect.DOKill();
                                leaderboardScrollRect.DOVerticalNormalizedPos(targetNormalized, 0.55f).SetEase(Ease.OutCubic);
                            }
                        }
                    });
                }
            }
            else
            {
                ScrollToCurrentUser(snapshot.currentUserId);
            }

            return;
        }

        // CÓ CẬP NHẬT: Vẽ bảng xếp hạng cũ và chạy hoạt ảnh thăng hạng
        List<UserData> initialLeaderboard = snapshot.previousLeaderboard;
        RenderLeaderboard(initialLeaderboard, maxPoints);
        ForceContentLayout();

        bool isFirstOpen = _isFirstOpenInSession;
        if (isFirstOpen)
        {
            if (leaderboardScrollRect != null)
            {
                leaderboardScrollRect.verticalNormalizedPosition = 1f;
            }
            _isFirstOpenInSession = false;
        }
        else
        {
            ScrollToCurrentUser(snapshot.currentUserId);
        }

        if (animator == null)
        {
            if (snapshot.isFirstView)
            {
                ScrollToCurrentUser(snapshot.currentUserId);
            }
            else
            {
                RenderLeaderboard(snapshot.currentLeaderboard, maxPoints);
                ForceContentLayout();
                ScrollToCurrentUser(snapshot.currentUserId);
            }
            return;
        }

        float transitionDelay = 0.25f;
        bool skipInitialScroll = !isFirstOpen;
        animator.PlayRankUpdate(leaderboardContent, leaderboardScrollRect, _rows, snapshot, maxPoints, playerHighlightSprite, transitionDelay, skipInitialScroll);
    }

    public void SetItemPrefab(GameObject itemPrefab)
    {
        leaderboardItemPrefab = itemPrefab;
    }

    public void SetScrollView(ScrollRect scrollRect, RectTransform content)
    {
        leaderboardScrollRect = scrollRect;
        leaderboardContent = content != null ? content : scrollRect != null ? scrollRect.content : null;
    }

    private void EnsureComponents()
    {
        if (dataProvider == null)
        {
            dataProvider = GetComponent<LeaderboardMenuDataProvider>();
            if (dataProvider == null)
            {
                dataProvider = gameObject.AddComponent<LeaderboardMenuDataProvider>();
            }
        }

        if (animator == null)
        {
            animator = GetComponent<LeaderboardMenuAnimator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<LeaderboardMenuAnimator>();
            }
        }
    }

    private bool EnsureScrollView()
    {
        if (leaderboardContent != null)
        {
            return true;
        }

        if (leaderboardScrollRect == null)
        {
            leaderboardScrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (leaderboardScrollRect != null)
        {
            leaderboardContent = leaderboardScrollRect.content;
            if (leaderboardContent != null)
            {
                ConfigureContentLayout(leaderboardContent);
                return true;
            }
        }

        if (!autoCreateMissingScrollView)
        {
            return false;
        }

        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null)
        {
            return false;
        }

        HidePlaceholderText(parentRect);
        BuildRuntimeScrollView(parentRect);
        return leaderboardScrollRect != null && leaderboardContent != null;
    }

    private void RenderLeaderboard(List<UserData> leaderboard, int maxPoints)
    {
        ClearRows();

        if (leaderboard == null || leaderboardContent == null)
        {
            return;
        }

        for (int i = 0; i < leaderboard.Count; i++)
        {
            UserData user = leaderboard[i];
            if (user == null)
            {
                continue;
            }

            LeaderboardItemUI row = CreateRow();
            if (row == null)
            {
                continue;
            }

            bool isCurrentUser = user.userId == LeaderboardMenuDataProvider.CurrentUserId;
            row.Setup(user, i + 1, isCurrentUser, user.points, maxPoints);

            if (isCurrentUser && playerHighlightSprite != null)
            {
                Image bgImage = row.GetComponent<Image>();
                if (bgImage == null)
                {
                    bgImage = row.GetComponentInChildren<Image>();
                }
                if (bgImage != null)
                {
                    bgImage.sprite = playerHighlightSprite;
                }
            }

            _rows.Add(row);
        }

        ForceContentLayout();
    }

    private LeaderboardItemUI CreateRow()
    {
        if (leaderboardItemPrefab != null)
        {
            GameObject instance = Instantiate(leaderboardItemPrefab, leaderboardContent);
            LeaderboardItemUI prefabItem = instance.GetComponent<LeaderboardItemUI>();
            if (prefabItem != null)
            {
                return prefabItem;
            }

            Destroy(instance);
            Debug.LogWarning("[LeaderboardMenuController] Leaderboard item prefab needs a LeaderboardItemUI component. Runtime item will be used instead.");
        }

        return LeaderboardItemUI.CreateRuntimeItem(leaderboardContent);
    }

    private void ClearRows()
    {
        if (animator != null)
        {
            animator.Kill();
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
            {
                Destroy(_rows[i].gameObject);
            }
        }

        _rows.Clear();

        if (leaderboardContent == null)
        {
            return;
        }

        for (int i = leaderboardContent.childCount - 1; i >= 0; i--)
        {
            Destroy(leaderboardContent.GetChild(i).gameObject);
        }
    }

    private void BuildRuntimeScrollView(RectTransform parentRect)
    {
        GameObject scrollObject = new GameObject("Leaderboard Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parentRect, false);
        scrollObject.layer = parentRect.gameObject.layer;

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(24f, 24f);
        scrollRectTransform.offsetMax = new Vector2(-24f, -24f);

        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0f);
        scrollImage.raycastTarget = true;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        viewportObject.layer = scrollObject.layer;

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportImage.raycastTarget = true;

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentObject.layer = scrollObject.layer;

        leaderboardContent = contentObject.GetComponent<RectTransform>();
        leaderboardContent.anchorMin = new Vector2(0f, 1f);
        leaderboardContent.anchorMax = new Vector2(1f, 1f);
        leaderboardContent.pivot = new Vector2(0.5f, 1f);
        leaderboardContent.anchoredPosition = Vector2.zero;
        leaderboardContent.sizeDelta = Vector2.zero;

        ConfigureContentLayout(leaderboardContent);

        leaderboardScrollRect = scrollObject.GetComponent<ScrollRect>();
        leaderboardScrollRect.content = leaderboardContent;
        leaderboardScrollRect.viewport = viewportRect;
        leaderboardScrollRect.horizontal = false;
        leaderboardScrollRect.vertical = true;
        leaderboardScrollRect.movementType = ScrollRect.MovementType.Elastic;
        leaderboardScrollRect.scrollSensitivity = 20f;
    }

    private void ConfigureContentLayout(RectTransform content)
    {
        EnsureContentPadding();

        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layoutGroup.padding = contentPadding;
        layoutGroup.spacing = rowSpacing;
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter = content.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureContentPadding()
    {
        if (contentPadding == null)
        {
            contentPadding = new RectOffset(
                DefaultContentPadding,
                DefaultContentPadding,
                DefaultContentPadding,
                DefaultContentPadding);
        }
    }

    private void ForceContentLayout()
    {
        if (leaderboardContent == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(leaderboardContent);
    }

    private void ScrollToCurrentUser(string currentUserId)
    {
        if (leaderboardScrollRect == null || leaderboardScrollRect.viewport == null || leaderboardContent == null)
        {
            return;
        }

        DOVirtual.DelayedCall(0.15f, () =>
        {
            if (this == null || leaderboardScrollRect == null || leaderboardScrollRect.viewport == null || leaderboardContent == null)
            {
                return;
            }

            LeaderboardItemUI currentUserRow = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null && _rows[i].UserId == currentUserId)
                {
                    currentUserRow = _rows[i];
                    break;
                }
            }

            if (currentUserRow == null)
            {
                leaderboardScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            float contentHeight = leaderboardContent.rect.height;
            float viewportHeight = leaderboardScrollRect.viewport.rect.height;
            if (contentHeight <= viewportHeight)
            {
                leaderboardScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            float rowTop = Mathf.Abs(currentUserRow.RectTransform.anchoredPosition.y);
            float rowHeight = currentUserRow.RectTransform.rect.height;
            float rowCenter = rowTop + (rowHeight / 2f);
            
            float targetOffset = rowCenter - (viewportHeight / 2f);
            targetOffset = Mathf.Clamp(targetOffset, 0f, contentHeight - viewportHeight);
            
            leaderboardScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(targetOffset / (contentHeight - viewportHeight));
        });
    }

    private static int GetMaxPoints(List<UserData> previousLeaderboard, List<UserData> currentLeaderboard)
    {
        int maxPoints = 1;
        ApplyMax(previousLeaderboard, ref maxPoints);
        ApplyMax(currentLeaderboard, ref maxPoints);
        return maxPoints;
    }

    private static void ApplyMax(List<UserData> leaderboard, ref int maxPoints)
    {
        if (leaderboard == null)
        {
            return;
        }

        for (int i = 0; i < leaderboard.Count; i++)
        {
            if (leaderboard[i] != null)
            {
                maxPoints = Mathf.Max(maxPoints, leaderboard[i].points);
            }
        }
    }

    private static void HidePlaceholderText(RectTransform parentRect)
    {
        TMP_Text[] texts = parentRect.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && (text.text == "Emty" || text.text == "Empty"))
            {
                text.gameObject.SetActive(false);
            }
        }
    }
}
