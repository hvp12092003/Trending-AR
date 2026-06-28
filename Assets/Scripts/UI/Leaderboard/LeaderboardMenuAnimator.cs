using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardMenuAnimator : MonoBehaviour
{
    [SerializeField] private float rankMoveDuration = 0.85f;
    [SerializeField] private float pointCountDuration = 0.75f;
    [SerializeField] private float scrollDuration = 0.55f;
    [SerializeField] private Ease rankMoveEase = Ease.OutCubic;

    private Sequence _activeSequence;

    public void Kill()
    {
        _activeSequence?.Kill();
        _activeSequence = null;
    }

    public Sequence PlayRankUpdate(
        RectTransform content,
        ScrollRect scrollRect,
        List<LeaderboardItemUI> rows,
        LeaderboardSnapshotData snapshot,
        int maxPoints,
        Sprite playerHighlightSprite = null,
        float transitionDelay = 0.25f,
        bool skipInitialScroll = false)
    {
        Kill();

        if (content == null || rows == null || snapshot == null || snapshot.currentLeaderboard == null)
        {
            return null;
        }

        Dictionary<string, LeaderboardItemUI> rowByUserId = BuildRowMap(rows);
        Dictionary<string, Vector2> startPositions = CaptureCurrentPositions(rows, content);
        Dictionary<string, int> previousPointsByUserId = BuildPointsMap(snapshot.previousLeaderboard);

        ApplyFinalSiblingOrderAndData(rowByUserId, snapshot, previousPointsByUserId, maxPoints, playerHighlightSprite);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Dictionary<string, Vector2> endPositions = CaptureCurrentPositions(rows, content);
        bool shouldMoveRows = !snapshot.isFirstView &&
                               snapshot.previousCurrentUserRank > 0 &&
                               snapshot.currentUserRank > 0 &&
                               snapshot.currentUserRank < snapshot.previousCurrentUserRank;

        SetRowsIgnoreLayout(rows, true);

        for (int i = 0; i < rows.Count; i++)
        {
            LeaderboardItemUI row = rows[i];
            if (row == null || string.IsNullOrEmpty(row.UserId))
            {
                continue;
            }

            row.RectTransform.DOKill();

            if (shouldMoveRows && startPositions.TryGetValue(row.UserId, out Vector2 startPosition))
            {
                row.RectTransform.anchoredPosition = startPosition;
            }
            else if (endPositions.TryGetValue(row.UserId, out Vector2 endPosition))
            {
                row.RectTransform.anchoredPosition = endPosition;
            }
        }

        LeaderboardItemUI currentUserRow = rowByUserId.TryGetValue(snapshot.currentUserId, out LeaderboardItemUI foundRow)
            ? foundRow
            : null;

        if (shouldMoveRows)
        {
            RaiseCurrentUserRow(currentUserRow);
        }

        _activeSequence = DOTween.Sequence();

        // 1. Trì hoãn chờ chuyển panel hoàn tất
        if (transitionDelay > 0f)
        {
            _activeSequence.AppendInterval(transitionDelay);
        }

        // 2. Kéo content tới chỗ của player (chỉ cuộn nếu không bỏ qua)
        if (!skipInitialScroll && currentUserRow != null)
        {
            Tween initialScrollTween = CreateScrollTween(scrollRect, content, currentUserRow, scrollDuration);
            if (initialScrollTween != null)
            {
                _activeSequence.Append(initialScrollTween);
            }
        }

        // 3. Chờ 0.25s tại vị trí player trước khi thăng hạng
        _activeSequence.AppendInterval(0.25f);

        // Lưu lại vị trí ban đầu bằng callback động ngay trước khi thăng hạng để tính bù trừ
        Vector2 contentStartPos = Vector2.zero;
        float playerStartLocalY = 0f;

        _activeSequence.AppendCallback(() =>
        {
            if (content != null)
            {
                contentStartPos = content.anchoredPosition;
            }
            if (currentUserRow != null)
            {
                playerStartLocalY = currentUserRow.RectTransform.anchoredPosition.y;
            }
        });

        // 4. Thực hiện di chuyển dòng và tăng điểm song song
        bool isFirstTweenAdded = false;

        if (shouldMoveRows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                LeaderboardItemUI row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.UserId))
                {
                    continue;
                }

                // Loại trừ Player khỏi việc gán Tween di chuyển để tránh xung đột với logic bù trừ cuộn
                if (row.UserId == snapshot.currentUserId)
                {
                    continue;
                }

                if (endPositions.TryGetValue(row.UserId, out Vector2 endPosition))
                {
                    Tween moveTween = row.RectTransform.DOAnchorPos(endPosition, rankMoveDuration).SetEase(rankMoveEase);
                    if (!isFirstTweenAdded)
                    {
                        _activeSequence.Append(moveTween);
                        isFirstTweenAdded = true;
                    }
                    else
                    {
                        _activeSequence.Join(moveTween);
                    }
                }
            }

            // Đồng thời cuộn bám theo Player tới vị trí mới
            if (currentUserRow != null && endPositions.TryGetValue(currentUserRow.UserId, out Vector2 targetEndPos))
            {
                float contentHeight = content.rect.height;
                float viewportHeight = scrollRect.viewport.rect.height;
                if (contentHeight > viewportHeight && viewportHeight > 0f)
                {
                    float rowTop = Mathf.Abs(targetEndPos.y);
                    float rowHeight = currentUserRow.RectTransform.rect.height;
                    float rowCenter = rowTop + (rowHeight / 2f);
                    float targetOffset = rowCenter - (viewportHeight / 2f);
                    targetOffset = Mathf.Clamp(targetOffset, 0f, contentHeight - viewportHeight);
                    float targetNormalized = 1f - Mathf.Clamp01(targetOffset / (contentHeight - viewportHeight));

                    Tween followScrollTween = DOTween.To(
                        () => scrollRect.verticalNormalizedPosition,
                        value =>
                        {
                            if (scrollRect != null)
                            {
                                scrollRect.verticalNormalizedPosition = value;
                            }
                        },
                        targetNormalized,
                        rankMoveDuration
                    ).SetEase(rankMoveEase);

                    // Bù trừ vị trí Y của Player so với dịch chuyển của ScrollView để Player đứng im so với màn hình
                    followScrollTween.OnUpdate(() =>
                    {
                        if (currentUserRow != null && content != null)
                        {
                            float currentContentY = content.anchoredPosition.y;
                            float deltaContentY = currentContentY - contentStartPos.y;
                            Vector2 currentPos = currentUserRow.RectTransform.anchoredPosition;
                            currentUserRow.RectTransform.anchoredPosition = new Vector2(currentPos.x, playerStartLocalY - deltaContentY);
                        }
                    });

                    _activeSequence.Join(followScrollTween);
                }

                // Thêm Tween di chuyển từ từ người chơi lên vị trí rank mới sau khi các hoạt ảnh trước hoàn thành
                float playerMoveDuration = 0.35f;
                Tween playerMoveTween = currentUserRow.RectTransform
                    .DOAnchorPos(targetEndPos, playerMoveDuration)
                    .SetEase(Ease.OutCubic);
                _activeSequence.Append(playerMoveTween);
            }
        }

        Tween pointTween = CreateCurrentUserPointTween(currentUserRow, snapshot, previousPointsByUserId, maxPoints);
        if (pointTween != null)
        {
            if (!isFirstTweenAdded)
            {
                _activeSequence.Append(pointTween);
                isFirstTweenAdded = true;
            }
            else
            {
                _activeSequence.Join(pointTween);
            }
        }

        _activeSequence.OnComplete(() =>
        {
            SetRowsIgnoreLayout(rows, false);
            ApplyFinalSiblingOrderAndData(rowByUserId, snapshot, null, maxPoints, playerHighlightSprite);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            _activeSequence = null;
        });

        return _activeSequence;
    }

    private static Dictionary<string, LeaderboardItemUI> BuildRowMap(List<LeaderboardItemUI> rows)
    {
        Dictionary<string, LeaderboardItemUI> map = new Dictionary<string, LeaderboardItemUI>();
        for (int i = 0; i < rows.Count; i++)
        {
            LeaderboardItemUI row = rows[i];
            if (row != null && !string.IsNullOrEmpty(row.UserId))
            {
                map[row.UserId] = row;
            }
        }

        return map;
    }

    private static Dictionary<string, int> BuildPointsMap(List<UserData> leaderboard)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        if (leaderboard == null)
        {
            return map;
        }

        for (int i = 0; i < leaderboard.Count; i++)
        {
            UserData user = leaderboard[i];
            if (user != null && !string.IsNullOrEmpty(user.userId))
            {
                map[user.userId] = user.points;
            }
        }

        return map;
    }

    private static Dictionary<string, Vector2> CaptureCurrentPositions(List<LeaderboardItemUI> rows, RectTransform content)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        for (int i = 0; i < rows.Count; i++)
        {
            LeaderboardItemUI row = rows[i];
            if (row != null && !string.IsNullOrEmpty(row.UserId))
            {
                positions[row.UserId] = row.RectTransform.anchoredPosition;
            }
        }

        return positions;
    }

    private static void ApplyFinalSiblingOrderAndData(
        Dictionary<string, LeaderboardItemUI> rowByUserId,
        LeaderboardSnapshotData snapshot,
        Dictionary<string, int> previousPointsByUserId,
        int maxPoints,
        Sprite playerHighlightSprite = null)
    {
        for (int i = 0; i < snapshot.currentLeaderboard.Count; i++)
        {
            UserData user = snapshot.currentLeaderboard[i];
            if (user == null || string.IsNullOrEmpty(user.userId))
            {
                continue;
            }

            if (!rowByUserId.TryGetValue(user.userId, out LeaderboardItemUI row) || row == null)
            {
                continue;
            }

            bool isCurrentUser = user.userId == snapshot.currentUserId;
            int displayPoints = user.points;
            if (isCurrentUser && previousPointsByUserId != null && previousPointsByUserId.TryGetValue(user.userId, out int previousPoints))
            {
                displayPoints = previousPoints;
            }

            row.transform.SetSiblingIndex(i);
            row.Setup(user, i + 1, isCurrentUser, displayPoints, maxPoints);

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
        }
    }

    private Tween CreateCurrentUserPointTween(
        LeaderboardItemUI currentUserRow,
        LeaderboardSnapshotData snapshot,
        Dictionary<string, int> previousPointsByUserId,
        int maxPoints)
    {
        if (currentUserRow == null || snapshot.currentLeaderboard == null)
        {
            return null;
        }

        UserData currentUser = null;
        for (int i = 0; i < snapshot.currentLeaderboard.Count; i++)
        {
            if (snapshot.currentLeaderboard[i] != null && snapshot.currentLeaderboard[i].userId == snapshot.currentUserId)
            {
                currentUser = snapshot.currentLeaderboard[i];
                break;
            }
        }

        if (currentUser == null)
        {
            return null;
        }

        int fromPoints = currentUser.points;
        if (previousPointsByUserId != null && previousPointsByUserId.TryGetValue(snapshot.currentUserId, out int previousPoints))
        {
            fromPoints = previousPoints;
        }

        if (fromPoints == currentUser.points)
        {
            return null;
        }

        return currentUserRow.AnimatePoints(fromPoints, currentUser.points, maxPoints, pointCountDuration);
    }

    private Tween CreateScrollTween(ScrollRect scrollRect, RectTransform content, LeaderboardItemUI currentUserRow, float duration)
    {
        if (scrollRect == null || content == null || currentUserRow == null || scrollRect.viewport == null)
        {
            return null;
        }

        float contentHeight = content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;
        if (contentHeight <= viewportHeight || viewportHeight <= 0f)
        {
            return null;
        }

        float rowTop = Mathf.Abs(currentUserRow.RectTransform.anchoredPosition.y);
        float rowHeight = currentUserRow.RectTransform.rect.height;
        float rowCenter = rowTop + (rowHeight / 2f);
        
        float targetOffset = rowCenter - (viewportHeight / 2f);
        targetOffset = Mathf.Clamp(targetOffset, 0f, contentHeight - viewportHeight);
        float targetNormalized = 1f - Mathf.Clamp01(targetOffset / (contentHeight - viewportHeight));

        return DOTween.To(
            () => scrollRect.verticalNormalizedPosition,
            value =>
            {
                if (scrollRect != null)
                {
                    scrollRect.verticalNormalizedPosition = value;
                }
            },
            targetNormalized,
            duration
        ).SetEase(Ease.OutCubic);
    }

    private static void RaiseCurrentUserRow(LeaderboardItemUI currentUserRow)
    {
        if (currentUserRow == null)
        {
            return;
        }

        currentUserRow.transform.SetAsLastSibling();
    }

    private static void SetRowsIgnoreLayout(List<LeaderboardItemUI> rows, bool ignoreLayout)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            LeaderboardItemUI row = rows[i];
            if (row == null)
            {
                continue;
            }

            LayoutElement layoutElement = row.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = ignoreLayout;
            }
        }
    }
}
