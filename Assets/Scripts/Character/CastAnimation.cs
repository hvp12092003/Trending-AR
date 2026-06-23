using UnityEngine;

[System.Serializable]
public struct CastAnimation
{
    [Tooltip("Animation clip")]
    public AnimationClip animation;

    [Tooltip("Animation avatar displayed in the UI")]
    public Sprite sprite;

    [Tooltip("Animation display name in the UI")]
    public string animName;
}
