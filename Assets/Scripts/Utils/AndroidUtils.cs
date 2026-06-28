using UnityEngine;

public class AndroidUtils : MonoBehaviour
{
    private const string VibrationPrefKey = "Setting_Vibration";

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject s_Vibrator;
    private static AndroidJavaClass s_VibrationEffectClass;
    private static int s_SdkInt = -1;
    private static int s_DefaultAmplitude = -1;
    private static bool s_HasVibrator = true;
#endif

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public static void WarmUpVibration()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (PlayerPrefs.GetInt(VibrationPrefKey, 1) == 0)
        {
            return;
        }

        try
        {
            EnsureAndroidVibrator();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AndroidUtils] Vibration warm-up failed: {ex.Message}");
        }
#endif
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
            if (!EnsureAndroidVibrator())
            {
                Debug.Log("[AndroidUtils] Device does not support vibration.");
                return;
            }

            if (s_SdkInt >= 26)
            {
                AndroidJavaObject effect = s_VibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, s_DefaultAmplitude);
                s_Vibrator.Call("vibrate", effect);
                effect.Dispose();
            }
            else
            {
                s_Vibrator.Call("vibrate", milliseconds);
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

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool EnsureAndroidVibrator()
    {
        if (s_Vibrator != null)
        {
            return s_HasVibrator;
        }

        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject context = currentActivity != null ? currentActivity.Call<AndroidJavaObject>("getApplicationContext") : null;
        s_Vibrator = context != null ? context.Call<AndroidJavaObject>("getSystemService", "vibrator") : null;

        unityPlayer.Dispose();
        if (currentActivity != null) currentActivity.Dispose();
        if (context != null) context.Dispose();

        if (s_Vibrator == null)
        {
            s_HasVibrator = false;
            return false;
        }

        s_HasVibrator = s_Vibrator.Call<bool>("hasVibrator");
        if (!s_HasVibrator)
        {
            s_Vibrator.Dispose();
            s_Vibrator = null;
            return false;
        }

        if (s_SdkInt < 0)
        {
            AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION");
            s_SdkInt = versionClass.GetStatic<int>("SDK_INT");
            versionClass.Dispose();
        }

        if (s_SdkInt >= 26 && s_VibrationEffectClass == null)
        {
            s_VibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            s_DefaultAmplitude = s_VibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
        }

        return true;
    }
#endif

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
