using UnityEngine;

public static class CastSlotUnlockManager
{
    public const int BaseUnlockedSlotCount = 4;
    public const int MaxSlotCount = 9;

    private const string UnlockedSlotCountPrefsKey = "UnlockedCastSlotCount";

    public static int GetUnlockedSlotCount(int savedCastCount = 0)
    {
        int storedCount = PlayerPrefs.GetInt(UnlockedSlotCountPrefsKey, BaseUnlockedSlotCount);
        int migratedCount = Mathf.Max(storedCount, Mathf.Clamp(savedCastCount, 0, MaxSlotCount));
        int clampedCount = Mathf.Clamp(migratedCount, BaseUnlockedSlotCount, MaxSlotCount);

        if (storedCount != clampedCount)
        {
            PlayerPrefs.SetInt(UnlockedSlotCountPrefsKey, clampedCount);
            PlayerPrefs.Save();
        }

        return clampedCount;
    }

    public static bool HasFreeUnlockedSlot(int savedCastCount)
    {
        return savedCastCount < GetUnlockedSlotCount(savedCastCount);
    }

    public static bool CanUnlockMore(int savedCastCount = 0)
    {
        return GetUnlockedSlotCount(savedCastCount) < MaxSlotCount;
    }

    public static int GetNextUnlockableSlotNumber(int savedCastCount = 0)
    {
        return Mathf.Clamp(GetUnlockedSlotCount(savedCastCount) + 1, BaseUnlockedSlotCount + 1, MaxSlotCount);
    }

    public static bool TryUnlockNextSlot(int savedCastCount = 0)
    {
        int unlockedSlotCount = GetUnlockedSlotCount(savedCastCount);
        if (unlockedSlotCount >= MaxSlotCount)
        {
            return false;
        }

        PlayerPrefs.SetInt(UnlockedSlotCountPrefsKey, unlockedSlotCount + 1);
        PlayerPrefs.Save();
        return true;
    }
}
