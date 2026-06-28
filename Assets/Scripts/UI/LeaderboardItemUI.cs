using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI pointsText;

    private RectTransform _rectTransform;
    private int _displayedPoints;

    public string UserId { get; private set; }
    public int DisplayedPoints => _displayedPoints;
    public RectTransform RectTransform => GetRectTransform();

    private RectTransform GetRectTransform()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        return _rectTransform;
    }

    public void Setup(UserData data, int rank, bool isCurrentUser)
    {
        Setup(data, rank, isCurrentUser, data != null ? data.points : 0, 1);
    }

    public void Setup(UserData data, int rank, bool isCurrentUser, int displayPoints, int maxPoints)
    {
        if (data == null)
        {
            return;
        }

        UserId = data.userId;

        if (rankText != null)
        {
            rankText.text = rank.ToString();
        }

        if (nameText != null)
        {
            nameText.text = data.displayName;
        }

        SetPoints(displayPoints);
    }

    public Tween AnimatePoints(int fromPoints, int toPoints, int maxPoints, float duration)
    {
        SetPoints(fromPoints);

        return DOVirtual.Int(fromPoints, toPoints, duration, SetPoints)
            .SetEase(Ease.OutCubic);
    }

    private void SetPoints(int points)
    {
        _displayedPoints = points;

        if (pointsText != null)
        {
            pointsText.text = points.ToString();
        }
    }

    public static LeaderboardItemUI CreateRuntimeItem(Transform parent)
    {
        GameObject rowObject = new GameObject("Leaderboard Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement), typeof(CanvasGroup));
        rowObject.transform.SetParent(parent, false);
        rowObject.layer = parent != null ? parent.gameObject.layer : 5;

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 72f);

        LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 72f;
        layoutElement.preferredHeight = 72f;
        layoutElement.flexibleWidth = 1f;

        LeaderboardItemUI itemUI = rowObject.AddComponent<LeaderboardItemUI>();
        itemUI.rankText = CreateText("Rank Text", rowObject.transform, "1", 28, TextAlignmentOptions.Center);
        itemUI.nameText = CreateText("Name Text", rowObject.transform, "Player", 28, TextAlignmentOptions.Left);
        itemUI.pointsText = CreateText("Points Text", rowObject.transform, "0", 28, TextAlignmentOptions.Right);

        SetRect(itemUI.rankText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(80f, 0f));
        SetRect(itemUI.nameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(96f, 0f), new Vector2(-220f, 0f));
        SetRect(itemUI.pointsText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-210f, 0f), new Vector2(0f, 0f));

        return itemUI;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
