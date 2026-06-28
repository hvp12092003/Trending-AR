using System;
using UnityEngine;

[Serializable]
public class LeaderboardBotProfile
{
    public string userId;
    public string displayName;
    [Min(0)] public int startPoints;
    [Min(0)] public int minGainPerRefresh;
    [Min(0)] public int maxGainPerRefresh;

    public LeaderboardBotProfile(string userId, string displayName, int startPoints, int minGainPerRefresh, int maxGainPerRefresh)
    {
        this.userId = userId;
        this.displayName = displayName;
        this.startPoints = startPoints;
        this.minGainPerRefresh = minGainPerRefresh;
        this.maxGainPerRefresh = maxGainPerRefresh;
    }
}
