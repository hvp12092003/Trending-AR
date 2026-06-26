using UnityEngine;

public class AndroidUtils : MonoBehaviour
{
    private const string VibrationPrefKey = "Setting_Vibration";

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public static void Vibrate(long milliseconds = 3000)
    {
        if (PlayerPrefs.GetInt(VibrationPrefKey, 1) == 0)
        {
            Debug.Log("[AndroidUtils] Vibration skipped because it is disabled in settings.");
            return;
        }

        milliseconds = milliseconds <= 0 ? 1 : milliseconds;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaObject currentActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");
            AndroidJavaObject vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
            {
                Debug.Log("[AndroidUtils] Device does not support vibration.");
                return;
            }

            int sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
            if (sdkInt >= 26)
            {
                AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                int defaultAmplitude = vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
                AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, defaultAmplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AndroidUtils] Vibration failed: {ex.Message}");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#else
        Debug.Log($"[AndroidUtils] Simulate vibration for {milliseconds}ms.");
#endif
    }

    public static void ShowToast(string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject currentActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            new AndroidJavaClass("android.widget.Toast").CallStatic<AndroidJavaObject>("makeText", currentActivity.Call<AndroidJavaObject>("getApplicationContext"), new AndroidJavaObject("java.lang.String", message), 0).Call("show");
        }));
#else
        Debug.Log($"[Toast Simulation] {message}");
#endif
    }
}
