using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardItemUI : MonoBehaviour
{
    private const float RuntimeRowHeight = 72f;

    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image countryFlagImage;
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

        EnsureImageReferences();
        UserId = data.userId;

        if (rankText != null)
        {
            rankText.text = rank.ToString();
        }

        if (nameText != null)
        {
            nameText.text = data.displayName;
        }

        SetCountryFlag(data.countryFlagSprite);
        SetAvatar(data.avatarSprite);
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

    private void SetAvatar(Sprite avatarSprite)
    {
        if (avatarImage == null)
        {
            return;
        }

        bool hasFlagChild = countryFlagImage != null && countryFlagImage.transform.IsChildOf(avatarImage.transform);
        bool hasFlagSprite = hasFlagChild && countryFlagImage.sprite != null;
        avatarImage.sprite = avatarSprite;
        avatarImage.enabled = avatarSprite != null;
        avatarImage.gameObject.SetActive(avatarSprite != null || hasFlagSprite);
        avatarImage.preserveAspect = true;
    }

    private void SetCountryFlag(Sprite flagSprite)
    {
        if (countryFlagImage == null)
        {
            return;
        }

        countryFlagImage.sprite = flagSprite;
        countryFlagImage.enabled = flagSprite != null;
        countryFlagImage.gameObject.SetActive(flagSprite != null);
        countryFlagImage.preserveAspect = true;
    }

    private void EnsureImageReferences()
    {
        if (avatarImage == null)
        {
            avatarImage = FindImageByName("Avatar");
        }

        if (countryFlagImage == null)
        {
            countryFlagImage = FindImageByName("Flag");
        }
    }

    private Image FindImageByName(string objectName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return null;
    }

    public static LeaderboardItemUI CreateRuntimeItem(Transform parent)
    {
        GameObject rowObject = new GameObject("Leaderboard Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(CanvasGroup));
        rowObject.transform.SetParent(parent, false);
        rowObject.layer = parent != null ? parent.gameObject.layer : 5;

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, RuntimeRowHeight);

        Image backgroundImage = rowObject.GetComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0f);
        backgroundImage.raycastTarget = false;

        LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = RuntimeRowHeight;
        layoutElement.preferredHeight = RuntimeRowHeight;
        layoutElement.flexibleWidth = 1f;

        LeaderboardItemUI itemUI = rowObject.AddComponent<LeaderboardItemUI>();
        itemUI.rankText = CreateText("Rank Text", rowObject.transform, "1", 28, TextAlignmentOptions.Center);
        itemUI.avatarImage = CreateImage("Avatar Image", rowObject.transform);
        itemUI.nameText = CreateText("Name Text", rowObject.transform, "Player", 28, TextAlignmentOptions.Left);
        itemUI.pointsText = CreateText("Points Text", rowObject.transform, "0", 28, TextAlignmentOptions.Right);

        SetRect(itemUI.rankText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(72f, 0f));
        SetRect(itemUI.avatarImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(88f, -24f), new Vector2(136f, 24f));
        SetRect(itemUI.nameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(152f, 0f), new Vector2(-220f, 0f));
        SetRect(itemUI.pointsText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-210f, 0f), new Vector2(0f, 0f));

        return itemUI;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = parent.gameObject.layer;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;

        return image;
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
