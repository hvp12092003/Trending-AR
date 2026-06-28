using UnityEngine;

/// <summary>
/// Tracks whether a pre-spawned Cast has actually been dropped into the world.
/// Detaching from a pedestal happens at drag start, so parent == null is not
/// enough to tell gameplay systems that placement is complete.
/// </summary>
public class CastPlacementState : MonoBehaviour
{
    public bool IsPlaced { get; private set; }

    public void MarkPlaced()
    {
        IsPlaced = true;
    }

    public void MarkUnplaced()
    {
        IsPlaced = false;
    }
}
