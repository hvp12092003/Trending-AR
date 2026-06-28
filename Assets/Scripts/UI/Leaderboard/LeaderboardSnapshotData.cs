using System.Collections.Generic;

public class LeaderboardSnapshotData
{
    public string currentUserId;
    public List<UserData> previousLeaderboard;
    public List<UserData> currentLeaderboard;
    public int previousCurrentUserRank;
    public int currentUserRank;
    public bool isFirstView;
}
