using UnityEngine;
#if ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif
#if UNITY_IAP
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using UnityEngine.Purchasing;
#endif

// Ads between runs + the one-time REMOVE ADS purchase, on phones (Android and iOS share this file; desktop/Steam builds skip it).
// Each half switches on with a scripting define once its package is installed: ADMOB (Google Mobile Ads plugin), UNITY_IAP (In-App Purchasing 5).
public class Monetization : MonoBehaviour
{
    // Google's public test interstitials. Swap in your own ad unit ids from AdMob before release (README: Android release).
    const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
    const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
    public const string RemoveAdsId = "remove_ads"; // non-consumable, same id in Play Console and App Store Connect
    const float MinGap = 180; // at least 3 minutes of play between ads, and never before the first one

    public static bool NoAds { get => PlayerPrefs.GetInt("noads") == 1; private set { PlayerPrefs.SetInt("noads", value ? 1 : 0); PlayerPrefs.Save(); } }
    static string price = ""; // localized, from the store (RM9.99 etc.)
    int lastTape;
    float lastAd;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.isMobilePlatform || Application.isEditor) DontDestroyOnLoad(new GameObject("Monetization").AddComponent<Monetization>().gameObject);
    }

    void Start()
    {
        lastAd = Time.realtimeSinceStartup;
#if UNITY_IAP
        StartIap();
#endif
#if ADMOB
        if (!NoAds) StartAds();
#endif
    }

    // A new tape (run) started: the exit was reached or the entity got you. That's the one place an ad may show.
    void Update()
    {
        if (BackroomsGame.Tape == lastTape) return;
        bool first = lastTape == 0;
        lastTape = BackroomsGame.Tape;
        if (first || NoAds || !BackroomsGame.Started || Time.realtimeSinceStartup - lastAd < MinGap) return;
#if ADMOB
        ShowAd();
#endif
    }

    // Start screen: REMOVE ADS bottom-left, RESTORE bottom-right (App Store review wants a visible restore). Bottom middle is the debug hold.
    void OnGUI()
    {
        if (BackroomsGame.Started || NoAds) return;
        float w = Screen.height * 0.36f, h = Screen.height * 0.09f, m = Screen.height * 0.04f;
        if (GUI.Button(new Rect(m, Screen.height - h - m, w, h), price == "" ? "REMOVE ADS" : "REMOVE ADS " + price)) Buy();
        if (GUI.Button(new Rect(Screen.width - w * 0.7f - m, Screen.height - h - m, w * 0.7f, h), "RESTORE")) RestorePurchases();
    }

#if ADMOB
    InterstitialAd ad;
    bool loading;

    void StartAds()
    {
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        // Google's consent form (EU/UK etc.); elsewhere it doesn't show.
        ConsentInformation.Update(new ConsentRequestParameters(), error =>
        {
            if (error != null) Debug.LogWarning("Consent: " + error.Message);
            ConsentForm.LoadAndShowConsentFormIfRequired(formError => { if (ConsentInformation.CanRequestAds()) MobileAds.Initialize(status => LoadAd()); });
        });
    }

    void LoadAd()
    {
        if (NoAds || loading || ad != null) return;
        loading = true;
        InterstitialAd.Load(Application.platform == RuntimePlatform.IPhonePlayer ? IosInterstitial : AndroidInterstitial, new AdRequest(), (loaded, error) =>
        {
            loading = false;
            if (error != null) { Debug.LogWarning("Ad: " + error.GetMessage()); return; } // tried again at the next run
            ad = loaded;
            ad.OnAdFullScreenContentClosed += Resume;
            ad.OnAdFullScreenContentFailed += _ => Resume();
        });
    }

    void ShowAd()
    {
        if (ad == null || !ad.CanShowAd()) { LoadAd(); return; }
        lastAd = Time.realtimeSinceStartup;
        Time.timeScale = 0; AudioListener.pause = true;
        ad.Show();
    }

    void Resume()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        ad.Destroy(); ad = null;
        LoadAd();
    }
#endif

#if UNITY_IAP
    static StoreController store;

    async void StartIap()
    {
        try
        {
            await UnityServices.InitializeAsync();
            store = UnityIAPServices.StoreController();
            store.OnProductsFetched += products => { price = products.FirstOrDefault(p => p.definition.id == RemoveAdsId)?.metadata.localizedPriceString ?? ""; store.FetchPurchases(); };
            store.OnPurchasesFetched += orders => { if (orders.ConfirmedOrders.Any(IsRemoveAds)) NoAds = true; }; // reinstall / new phone
            store.OnPurchasePending += order => { if (IsRemoveAds(order)) NoAds = true; store.ConfirmPurchase(order); };
            store.OnPurchaseFailed += failed => Debug.LogWarning("Purchase: " + failed.FailureReason);
            await store.Connect();
            store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(RemoveAdsId, ProductType.NonConsumable) });
        }
        catch (System.Exception e) { Debug.LogWarning("IAP: " + e.Message); store = null; }
    }

    static bool IsRemoveAds(Order order) => order.CartOrdered.Items().Any(i => i.Product.definition.id == RemoveAdsId);
#endif

    public static void Buy()
    {
#if UNITY_IAP
        store?.PurchaseProduct(RemoveAdsId);
#endif
    }

    public static void RestorePurchases()
    {
#if UNITY_IAP
        store?.FetchPurchases(); // owned non-consumables come back through OnPurchasesFetched
#endif
    }
}
