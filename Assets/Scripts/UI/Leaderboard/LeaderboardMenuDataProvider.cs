using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class LeaderboardMenuDataProvider : MonoBehaviour
{
    public const string CurrentUserId = "offline_user_id";

    [Header("Player Prefs")]
    [SerializeField] private string playerNamePrefsKey = "OfflineUserName";
    [SerializeField] private string playerPointsPrefsKey = "OfflineUserPoints";
    [SerializeField] private string lastPlayerPointsPrefsKey = "LeaderboardLastUserPoints";
    [SerializeField] private string botPointsPrefsPrefix = "LeaderboardBotPoints_";
    [SerializeField] private string defaultPlayerAvatarId = "cast_20_MINH HAT";

    [Header("Bot Score Simulation")]
    [SerializeField] private bool updateBotsOnRefresh = true;
    [SerializeField] private int maxBotCatchUpBonus = 80;
    [SerializeField] private int botSafetyGapBelowPlayer = 1;
    [SerializeField] private float botUpdateIntervalMinutes = 5f;
    [SerializeField] private string lastBotUpdateTimePrefsKey = "LeaderboardLastBotUpdateTime";
    [SerializeField] private List<LeaderboardBotProfile> bots = new List<LeaderboardBotProfile>();

    private Sprite _cachedCurrentUserAvatarSprite;
    private Texture2D _cachedCurrentUserAvatarTexture;
    private string _cachedCurrentUserAvatarPath;
    private DateTime _cachedCurrentUserAvatarWriteTimeUtc;

    private void Reset()
    {
        EnsureDefaultBots();
    }

    private void Awake()
    {
        EnsureDefaultBots();
    }

    private void OnDestroy()
    {
        ClearCachedCurrentUserAvatar();
    }

    public Task<LeaderboardSnapshotData> GetSnapshotAsync()
    {
        EnsureDefaultBots();

        int currentUserPoints = PlayerPrefs.GetInt(playerPointsPrefsKey, 0);
        bool hasPreviousUserPoints = PlayerPrefs.HasKey(lastPlayerPointsPrefsKey);
        int previousUserPoints = hasPreviousUserPoints
            ? PlayerPrefs.GetInt(lastPlayerPointsPrefsKey, currentUserPoints)
            : currentUserPoints;

        List<UserData> previousBots = LoadBotUsers();
        List<UserData> previousLeaderboard = BuildLeaderboard(previousUserPoints, previousBots);

        if (updateBotsOnRefresh)
        {
            bool shouldUpdate = false;
            string lastUpdateStr = PlayerPrefs.GetString(lastBotUpdateTimePrefsKey, "");
            if (string.IsNullOrEmpty(lastUpdateStr))
            {
                shouldUpdate = true;
            }
            else
            {
                if (System.DateTime.TryParse(lastUpdateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out System.DateTime lastUpdateTime))
                {
                    System.TimeSpan elapsed = System.DateTime.UtcNow - lastUpdateTime;
                    if (elapsed.TotalMinutes >= botUpdateIntervalMinutes)
                    {
                        shouldUpdate = true;
                    }
                }
                else
                {
                    shouldUpdate = true;
                }
            }

            if (shouldUpdate)
            {
                AdvanceBotScores(currentUserPoints);
                PlayerPrefs.SetString(lastBotUpdateTimePrefsKey, System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        List<UserData> currentLeaderboard = BuildLeaderboard(currentUserPoints, LoadBotUsers());

        PlayerPrefs.SetInt(lastPlayerPointsPrefsKey, currentUserPoints);
        PlayerPrefs.Save();

        return Task.FromResult(new LeaderboardSnapshotData
        {
            currentUserId = CurrentUserId,
            previousLeaderboard = previousLeaderboard,
            currentLeaderboard = currentLeaderboard,
            previousCurrentUserRank = GetRank(previousLeaderboard, CurrentUserId),
            currentUserRank = GetRank(currentLeaderboard, CurrentUserId),
            isFirstView = !hasPreviousUserPoints
        });
    }

    public Task<List<UserData>> GetCurrentLeaderboardAsync()
    {
        EnsureDefaultBots();

        int currentUserPoints = PlayerPrefs.GetInt(playerPointsPrefsKey, 0);
        return Task.FromResult(BuildLeaderboard(currentUserPoints, LoadBotUsers()));
    }

    private List<UserData> BuildLeaderboard(int currentUserPoints, List<UserData> botUsers)
    {
        string currentUserAvatarId = GetCurrentUserAvatarId();
        List<UserData> list = new List<UserData>
        {
            new UserData
            {
                userId = CurrentUserId,
                displayName = GetCurrentUserName(),
                points = currentUserPoints,
                avatarId = currentUserAvatarId,
                avatarSprite = ResolveCurrentUserAvatarSprite(currentUserAvatarId)
            }
        };

        if (botUsers != null)
        {
            for (int i = 0; i < botUsers.Count; i++)
            {
                UserData bot = botUsers[i];
                if (bot == null)
                {
                    continue;
                }

                list.Add(new UserData
                {
                    userId = bot.userId,
                    displayName = bot.displayName,
                    points = bot.points,
                    avatarId = bot.avatarId,
                    avatarBase64 = bot.avatarBase64,
                    avatarSprite = bot.avatarSprite
                });
            }
        }

        SortLeaderboard(list);
        return list;
    }

    private List<UserData> LoadBotUsers()
    {
        List<UserData> botUsers = new List<UserData>();
        bool changed = false;

        for (int i = 0; i < bots.Count; i++)
        {
            LeaderboardBotProfile bot = bots[i];
            if (bot == null || string.IsNullOrEmpty(bot.userId))
            {
                continue;
            }

            string key = GetBotPointsKey(bot.userId);
            int points = PlayerPrefs.GetInt(key, -1);
            if (points < 0)
            {
                points = Mathf.Max(0, bot.startPoints);
                PlayerPrefs.SetInt(key, points);
                changed = true;
            }

            botUsers.Add(new UserData
            {
                userId = bot.userId,
                displayName = string.IsNullOrEmpty(bot.displayName) ? bot.userId : bot.displayName,
                points = points,
                avatarId = bot.avatarId,
                avatarSprite = ResolveBotAvatarSprite(bot)
            });
        }

        if (changed)
        {
            PlayerPrefs.Save();
        }

        return botUsers;
    }

    private void AdvanceBotScores(int currentUserPoints)
    {
        for (int i = 0; i < bots.Count; i++)
        {
            LeaderboardBotProfile bot = bots[i];
            if (bot == null || string.IsNullOrEmpty(bot.userId))
            {
                continue;
            }

            string key = GetBotPointsKey(bot.userId);
            int currentPoints = PlayerPrefs.GetInt(key, Mathf.Max(0, bot.startPoints));
            int minGain = Mathf.Max(0, bot.minGainPerRefresh);
            int maxGain = Mathf.Max(minGain, bot.maxGainPerRefresh);
            int gain = UnityEngine.Random.Range(minGain, maxGain + 1);

            int gapBehindPlayer = currentUserPoints - currentPoints;
            if (gapBehindPlayer > 220)
            {
                gain += Mathf.Min(maxBotCatchUpBonus, gapBehindPlayer / 8);
            }

            if (currentPoints > currentUserPoints + 450)
            {
                gain = Mathf.Min(gain, 4);
            }

            int nextPoints = Mathf.Max(0, currentPoints + gain);

            if (currentPoints < currentUserPoints)
            {
                nextPoints = Mathf.Min(nextPoints, Mathf.Max(0, currentUserPoints - botSafetyGapBelowPlayer));
            }

            PlayerPrefs.SetInt(key, nextPoints);
        }
    }

    private string GetCurrentUserName()
    {
        if (AuthManager.Instance != null)
        {
            string loggedInUser = AuthManager.Instance.GetLoggedInUser();
            if (!string.IsNullOrEmpty(loggedInUser))
            {
                return loggedInUser;
            }
        }

        string playerName = PlayerPrefs.GetString(playerNamePrefsKey, "");
        if (string.IsNullOrEmpty(playerName) || playerName == "Offline User")
        {
            return "Offline Player";
        }

        return playerName;
    }

    private string GetBotPointsKey(string userId)
    {
        return botPointsPrefsPrefix + userId;
    }

    private string GetCurrentUserAvatarId()
    {
        return PlayerPrefs.GetString("SelectedAvatarId", "");
    }

    private Sprite ResolveCurrentUserAvatarSprite(string avatarId)
    {
        string resolvedAvatarId = string.IsNullOrEmpty(avatarId) ? defaultPlayerAvatarId : avatarId;
        Sprite presetSprite = ResolvePresetAvatarSprite(resolvedAvatarId);
        if (presetSprite != null)
        {
            return presetSprite;
        }

        return LoadCachedCurrentUserAvatarSprite();
    }

    private Sprite ResolveBotAvatarSprite(LeaderboardBotProfile bot)
    {
        if (bot == null)
        {
            return null;
        }

        return ResolvePresetAvatarSprite(bot.avatarId);
    }

    private Sprite ResolvePresetAvatarSprite(string avatarId)
    {
        if (string.IsNullOrEmpty(avatarId) || MainMenuDataManager.Instance == null)
        {
            return null;
        }

        if (avatarId.StartsWith("cast_"))
        {
            return MainMenuDataManager.Instance.GetCharacterAvatarSprite(avatarId.Substring("cast_".Length));
        }

        if (avatarId.StartsWith("inst_"))
        {
            return MainMenuDataManager.Instance.GetInstrumentAvatarSprite(avatarId.Substring("inst_".Length));
        }

        Sprite characterSprite = MainMenuDataManager.Instance.GetCharacterAvatarSprite(avatarId);
        if (characterSprite != null)
        {
            return characterSprite;
        }

        return MainMenuDataManager.Instance.GetInstrumentAvatarSprite(avatarId);
    }

    private Sprite LoadCachedCurrentUserAvatarSprite()
    {
        string localPath = Path.Combine(Application.persistentDataPath, "avatar_cache.jpg");
        if (!File.Exists(localPath))
        {
            return null;
        }

        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(localPath);
        if (_cachedCurrentUserAvatarSprite != null &&
            _cachedCurrentUserAvatarPath == localPath &&
            _cachedCurrentUserAvatarWriteTimeUtc == writeTimeUtc)
        {
            return _cachedCurrentUserAvatarSprite;
        }

        ClearCachedCurrentUserAvatar();

        try
        {
            byte[] bytes = File.ReadAllBytes(localPath);
            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            _cachedCurrentUserAvatarTexture = texture;
            _cachedCurrentUserAvatarSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            _cachedCurrentUserAvatarPath = localPath;
            _cachedCurrentUserAvatarWriteTimeUtc = writeTimeUtc;

            return _cachedCurrentUserAvatarSprite;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LeaderboardMenuDataProvider] Failed to load cached avatar: {ex.Message}");
            ClearCachedCurrentUserAvatar();
            return null;
        }
    }

    private void ClearCachedCurrentUserAvatar()
    {
        if (_cachedCurrentUserAvatarSprite != null)
        {
            Destroy(_cachedCurrentUserAvatarSprite);
            _cachedCurrentUserAvatarSprite = null;
        }

        if (_cachedCurrentUserAvatarTexture != null)
        {
            Destroy(_cachedCurrentUserAvatarTexture);
            _cachedCurrentUserAvatarTexture = null;
        }

        _cachedCurrentUserAvatarPath = "";
        _cachedCurrentUserAvatarWriteTimeUtc = default(DateTime);
    }

    private void EnsureDefaultBots()
    {
        if (bots == null)
        {
            bots = new List<LeaderboardBotProfile>();
        }

        if (bots.Count > 0)
        {
            return;
        }

        bots.Add(new LeaderboardBotProfile("bot_01", "Nova Beat", 1320, 4, 14, "inst_1_BunnyBeat"));
        bots.Add(new LeaderboardBotProfile("bot_02", "Sunny Beats", 1160, 5, 16, "cast_9_BOLT"));
        bots.Add(new LeaderboardBotProfile("bot_03", "Neon Dancer", 980, 4, 15, "cast_28_PIX"));
        bots.Add(new LeaderboardBotProfile("bot_04", "AR Groover", 830, 3, 14, "inst_6_DrumBot"));
        bots.Add(new LeaderboardBotProfile("bot_05", "Luna Remix", 700, 4, 13, "cast_17_LUNA"));
        bots.Add(new LeaderboardBotProfile("bot_06", "Pixel Pop", 585, 3, 12, "inst_0_AquaKeyboard"));
        bots.Add(new LeaderboardBotProfile("bot_07", "Echo Star", 470, 3, 11, "cast_14_ECHO"));
        bots.Add(new LeaderboardBotProfile("bot_08", "Beat Runner", 360, 2, 10, "inst_7_LeopardSpeak"));
        bots.Add(new LeaderboardBotProfile("bot_09", "Melo Rush", 280, 2, 9, "inst_11_PeacockBot"));
        bots.Add(new LeaderboardBotProfile("bot_10", "Rhythm Ace", 215, 2, 8, "inst_10_PandaSerenade"));
        bots.Add(new LeaderboardBotProfile("bot_11", "Kiki Loop", 165, 1, 8, "cast_18_MIMI"));
        bots.Add(new LeaderboardBotProfile("bot_12", "Jazzy Kid", 120, 1, 7, "cast_15_FINN"));
        bots.Add(new LeaderboardBotProfile("bot_13", "Chill Spark", 85, 1, 6, "cast_24_NIA"));
        bots.Add(new LeaderboardBotProfile("bot_14", "Mini Mix", 55, 1, 6, "cast_12_COCO"));
        bots.Add(new LeaderboardBotProfile("bot_15", "Tempo Tin", 35, 1, 5, "inst_8_Meowbone"));
        bots.Add(new LeaderboardBotProfile("bot_16", "Rookie Jam", 20, 1, 4, "cast_10_BUNNY"));
        bots.Add(new LeaderboardBotProfile("bot_17", "Fresh Step", 10, 0, 4, "cast_16_FOX"));
        bots.Add(new LeaderboardBotProfile("bot_18", "Iron man", 3, 0, 3, "cast_22_MONSTER"));
        bots.Add(new LeaderboardBotProfile("bot_19", "Mr Pazou", 1, 0, 3, "cast_27_PANDA"));
    }

    public static int GetRank(List<UserData> leaderboard, string userId)
    {
        if (leaderboard == null)
        {
            return 0;
        }

        for (int i = 0; i < leaderboard.Count; i++)
        {
            if (leaderboard[i] != null && leaderboard[i].userId == userId)
            {
                return i + 1;
            }
        }

        return 0;
    }

    public static void SortLeaderboard(List<UserData> leaderboard)
    {
        if (leaderboard == null)
        {
            return;
        }

        leaderboard.Sort((a, b) =>
        {
            int pointCompare = b.points.CompareTo(a.points);
            if (pointCompare != 0)
            {
                return pointCompare;
            }

            bool aIsCurrentUser = a.userId == CurrentUserId;
            bool bIsCurrentUser = b.userId == CurrentUserId;
            if (aIsCurrentUser != bIsCurrentUser)
            {
                return aIsCurrentUser ? -1 : 1;
            }

            return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
        });
    }
}
