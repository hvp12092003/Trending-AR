using UnityEngine;
using UnityEngine.SceneManagement;

public enum FirstTimeGuideCompletionScope
{
    PerScene,
    Global,
    CustomKey
}

public static class FirstTimeGuideProgress
{
    private const string KeyPrefix = "FirstTimeGuide.Completed.";
    private const string DefaultGuideId = "Default";

    public static bool IsCompleted(
        string guideId,
        FirstTimeGuideCompletionScope completionScope,
        string customCompletionKey = "")
    {
        return PlayerPrefs.GetInt(BuildKey(guideId, completionScope, customCompletionKey), 0) == 1;
    }

    public static void MarkCompleted(
        string guideId,
        FirstTimeGuideCompletionScope completionScope,
        string customCompletionKey = "")
    {
        PlayerPrefs.SetInt(BuildKey(guideId, completionScope, customCompletionKey), 1);
        PlayerPrefs.Save();
    }

    public static void Reset(
        string guideId,
        FirstTimeGuideCompletionScope completionScope,
        string customCompletionKey = "")
    {
        PlayerPrefs.DeleteKey(BuildKey(guideId, completionScope, customCompletionKey));
        PlayerPrefs.Save();
    }

    public static string BuildKey(
        string guideId,
        FirstTimeGuideCompletionScope completionScope,
        string customCompletionKey = "")
    {
        switch (completionScope)
        {
            case FirstTimeGuideCompletionScope.Global:
                return KeyPrefix + "Global." + Normalize(guideId);

            case FirstTimeGuideCompletionScope.CustomKey:
                return KeyPrefix + "Custom." + Normalize(customCompletionKey);

            case FirstTimeGuideCompletionScope.PerScene:
            default:
                return KeyPrefix + "Scene." + Normalize(SceneManager.GetActiveScene().name) + "." + Normalize(guideId);
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultGuideId;
        }

        return value.Trim().Replace(" ", "_");
    }
}
