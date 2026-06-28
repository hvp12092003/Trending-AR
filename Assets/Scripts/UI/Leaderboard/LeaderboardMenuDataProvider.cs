using System.Collections.Generic;
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

    [Header("Bot Score Simulation")]
    [SerializeField] private bool updateBotsOnRefresh = true;
    [SerializeField] private int maxBotCatchUpBonus = 80;
    [SerializeField] private int botSafetyGapBelowPlayer = 1;
    [SerializeField] private float botUpdateIntervalMinutes = 5f;
    [SerializeField] private string lastBotUpdateTimePrefsKey = "LeaderboardLastBotUpdateTime";
    [SerializeField] private List<LeaderboardBotProfile> bots = new List<LeaderboardBotProfile>();

    private void Reset()
    {
        EnsureDefaultBots();
    }

    private void Awake()
    {
        EnsureDefaultBots();
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
        List<UserData> list = new List<UserData>
        {
            new UserData
            {
                userId = CurrentUserId,
                displayName = GetCurrentUserName(),
                points = currentUserPoints
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
                    points = bot.points
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
                points = points
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
            int gain = Random.Range(minGain, maxGain + 1);

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

        bots.Add(new LeaderboardBotProfile("bot_01", "Alex Parker", 1320, 4, 14));
        bots.Add(new LeaderboardBotProfile("bot_02", "Emma Brooks", 1160, 5, 16));
        bots.Add(new LeaderboardBotProfile("bot_03", "Ryan Cooper", 980, 4, 15));
        bots.Add(new LeaderboardBotProfile("bot_04", "Mia Johnson", 830, 3, 14));
        bots.Add(new LeaderboardBotProfile("bot_05", "Liam Carter", 700, 4, 13));
        bots.Add(new LeaderboardBotProfile("bot_06", "Sophie Reed", 585, 3, 12));
        bots.Add(new LeaderboardBotProfile("bot_07", "Noah Bennett", 470, 3, 11));
        bots.Add(new LeaderboardBotProfile("bot_08", "Olivia Hayes", 360, 2, 10));
        bots.Add(new LeaderboardBotProfile("bot_09", "Lucas Gray", 280, 2, 9));
        bots.Add(new LeaderboardBotProfile("bot_10", "Ava Mitchell", 215, 2, 8));
        bots.Add(new LeaderboardBotProfile("bot_11", "Ethan Walker", 165, 1, 8));
        bots.Add(new LeaderboardBotProfile("bot_12", "Chloe Morgan", 120, 1, 7));
        bots.Add(new LeaderboardBotProfile("bot_13", "Mateo Ruiz", 85, 1, 6));
        bots.Add(new LeaderboardBotProfile("bot_14", "Camille Laurent", 55, 1, 6));
        bots.Add(new LeaderboardBotProfile("bot_15", "Hana Tanaka", 35, 1, 5));
        bots.Add(new LeaderboardBotProfile("bot_16", "Min Seo Kim", 20, 1, 4));
        bots.Add(new LeaderboardBotProfile("bot_17", "Anh Nguyen", 10, 0, 4));
        bots.Add(new LeaderboardBotProfile("bot_18", "Diego Santos", 0, 0, 3));
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
