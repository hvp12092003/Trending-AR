using System;
using System.Collections;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

public class RewardedAdService : MonoBehaviour
{
    public const string AndroidDemoAppId = "ca-app-pub-3940256099942544~3347511713";
    public const string IOSDemoAppId = "ca-app-pub-3940256099942544~1458002511";
    public const string AndroidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    public const string IOSRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

    public static RewardedAdService Instance { get; private set; }

    [Header("Demo Fallback")]
    [SerializeField] private bool simulateWhenAdMobSdkUnavailable = true;
    [SerializeField] private float simulatedAdDurationSeconds = 2.5f;

    private bool _isShowing;

#if GOOGLE_MOBILE_ADS
    private RewardedAd _rewardedAd;
    private bool _sdkInitialized;
    private bool _isLoading;
    private bool _activeRewardEarned;
    private Action<bool, string> _activeCallback;
#endif

    public string RewardedAdUnitId
    {
        get
        {
#if UNITY_IOS
            return IOSRewardedAdUnitId;
#else
            return AndroidRewardedAdUnitId;
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if GOOGLE_MOBILE_ADS
        InitializeAdMob();
#endif
    }

    public static RewardedAdService GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject serviceObject = new GameObject("RewardedAdService");
        return serviceObject.AddComponent<RewardedAdService>();
    }

    public void ShowRewardedAd(Action<bool, string> onComplete)
    {
        if (_isShowing)
        {
            onComplete?.Invoke(false, "Rewarded ad is already showing.");
            return;
        }

#if GOOGLE_MOBILE_ADS
        ShowGoogleMobileAdsRewardedAd(onComplete);
#else
        if (!simulateWhenAdMobSdkUnavailable)
        {
            onComplete?.Invoke(false, "Google Mobile Ads SDK is not installed.");
            return;
        }

        StartCoroutine(SimulateRewardedAd(onComplete));
#endif
    }

    private IEnumerator SimulateRewardedAd(Action<bool, string> onComplete)
    {
        _isShowing = true;
        Debug.Log("[RewardedAdService] Simulating AdMob rewarded ad with test unit: " + RewardedAdUnitId);

        float duration = Mathf.Max(0.1f, simulatedAdDurationSeconds);
        yield return new WaitForSecondsRealtime(duration);

        _isShowing = false;
        onComplete?.Invoke(true, "Demo rewarded ad completed.");
    }

#if GOOGLE_MOBILE_ADS
    private void InitializeAdMob()
    {
        if (_sdkInitialized)
        {
            return;
        }

        _sdkInitialized = true;
        MobileAds.Initialize(_ => LoadRewardedAd());
    }

    private void LoadRewardedAd()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        DestroyLoadedRewardedAd();

        RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            _isLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning("[RewardedAdService] Failed to load AdMob rewarded ad: " + error);
                return;
            }

            _rewardedAd = ad;
            RegisterRewardedAdEvents(_rewardedAd);
            Debug.Log("[RewardedAdService] Loaded AdMob rewarded ad: " + RewardedAdUnitId);
        });
    }

    private void ShowGoogleMobileAdsRewardedAd(Action<bool, string> onComplete)
    {
        InitializeAdMob();

        if (_rewardedAd == null || !_rewardedAd.CanShowAd())
        {
            LoadRewardedAd();

            if (simulateWhenAdMobSdkUnavailable)
            {
                StartCoroutine(SimulateRewardedAd(onComplete));
                return;
            }

            onComplete?.Invoke(false, "AdMob rewarded ad is not ready.");
            return;
        }

        _isShowing = true;
        _activeRewardEarned = false;
        _activeCallback = onComplete;

        _rewardedAd.Show(_ =>
        {
            _activeRewardEarned = true;
        });
    }

    private void RegisterRewardedAdEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            CompleteActiveAd(_activeRewardEarned, _activeRewardEarned ? "AdMob reward earned." : "Rewarded ad closed before reward.");
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            CompleteActiveAd(false, "AdMob rewarded ad failed to show: " + error);
            LoadRewardedAd();
        };
    }

    private void CompleteActiveAd(bool success, string message)
    {
        _isShowing = false;
        Action<bool, string> callback = _activeCallback;
        _activeCallback = null;
        _activeRewardEarned = false;
        callback?.Invoke(success, message);
    }

    private void DestroyLoadedRewardedAd()
    {
        if (_rewardedAd == null)
        {
            return;
        }

        _rewardedAd.Destroy();
        _rewardedAd = null;
    }
#endif
}
