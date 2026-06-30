using System;
using UnityEngine;

[Serializable]
public class LeaderboardBotProfile
{
    public string userId;
    public string displayName;
    [Tooltip("Avatar id from the main menu catalog, for example cast_<id> or inst_<id>.")]
    public string avatarId;
    [Min(0)] public int startPoints;
    [Min(0)] public int minGainPerRefresh;
    [Min(0)] public int maxGainPerRefresh;

    public LeaderboardBotProfile(
        string userId,
        string displayName,
        int startPoints,
        int minGainPerRefresh,
        int maxGainPerRefresh,
        string avatarId = "")
    {
        this.userId = userId;
        this.displayName = displayName;
        this.avatarId = avatarId;
        this.startPoints = startPoints;
        this.minGainPerRefresh = minGainPerRefresh;
        this.maxGainPerRefresh = maxGainPerRefresh;
    }
}
