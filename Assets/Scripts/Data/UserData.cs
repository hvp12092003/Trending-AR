using UnityEngine;

/// <summary>
/// Dữ liệu runtime của một người dùng (dùng trong Leaderboard và Profile).
/// </summary>
public class UserData
{
    public string userId       { get; set; }
    public string displayName  { get; set; }
    public string email        { get; set; }
    public int    points       { get; set; }
    public string avatarId     { get; set; }
    public string avatarBase64 { get; set; }
    public Sprite avatarSprite { get; set; }
    public string countryFlagId     { get; set; }
    public Sprite countryFlagSprite { get; set; }
}
