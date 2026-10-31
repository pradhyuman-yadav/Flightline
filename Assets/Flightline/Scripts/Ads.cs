using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Flightline
{
    // AdMob. Rewarded ads the player chooses to watch (keep flying after a crash, double the coins), plus an occasional
    // interstitial when leaving the game-over screen, capped so it never feels like every flight ends in an ad.
    // Consent comes first (Google's UMP form; it shows only where the law asks for it, and on iOS it also raises
    // Apple's tracking prompt once the IDFA message is set up in AdMob). One rewarded ad is kept loaded at all times.
    public static class Ads
    {
        // Our ad units (AdMob > Apps > Ad units; each ID must match its format). Empty means not made yet.
        // The app IDs live in Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.
#if UNITY_IOS
        const string LiveRewardedId = "ca-app-pub-3495212573386063/4881387972";
        const string TestRewardedId = "ca-app-pub-3940256099942544/1712485313";
        const string LiveInterstitialId = "ca-app-pub-3495212573386063/2829939706";
        const string TestInterstitialId = "ca-app-pub-3940256099942544/4411468910";
#else
        const string LiveRewardedId = "ca-app-pub-3495212573386063/4811611920";
        const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        const string LiveInterstitialId = "ca-app-pub-3495212573386063/1879373005";
        const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
#endif
        // Development builds and the editor always get Google's test ads (they fill and never pay): tapping our own
        // live ads counts as invalid traffic and can get the AdMob account suspended.
        static string RewardedId => Debug.isDebugBuild || LiveRewardedId == "" ? TestRewardedId : LiveRewardedId;
        static string InterstitialId => Debug.isDebugBuild || LiveInterstitialId == "" ? TestInterstitialId : LiveInterstitialId;

        // Interstitial cap: none in a player's first few flights, then at most one every 3 finished flights and never
        // within 2 minutes of any other ad (a revive or double-coins ad counts).
        const int FreeRuns = 3, FlightsPerInterstitial = 3;
        const float AdGap = 120f;
        static int flightsSinceAd;
        static float lastAdAt = -999f;
        static InterstitialAd interstitial;
        static bool loadingInterstitial;

        static bool started, initialized, loading, showing;
        static RewardedAd rewarded;
        static float retryAt;

        public static bool RewardedReady => rewarded != null && rewarded.CanShowAd() && !showing;
        // EU/UK players must be able to change their choice later (Google policy): show the button only when that applies.
        public static bool PrivacyOptionsRequired => started && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public static void Init()
        {
            if (started) return; started = true;
            MobileAds.RaiseAdEventsOnUnityMainThread = true; // callbacks touch the game and UI
            ConsentInformation.Update(new ConsentRequestParameters(), err =>
            {
                if (err != null) Debug.LogWarning("Flightline ads: consent update failed: " + err.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formErr =>
                {
                    if (formErr != null) Debug.LogWarning("Flightline ads: consent form failed: " + formErr.Message);
                    StartSdk();
                });
            });
            StartSdk(); // a returning player who already answered can load ads straight away
        }

        static void StartSdk()
        {
            if (initialized || !ConsentInformation.CanRequestAds()) return;
            initialized = true;
            MobileAds.Initialize(_ => Load());
        }

        // Called every frame by the game: retries a failed load after a pause.
        public static void Tick()
        {
            if (initialized && (rewarded == null || interstitial == null) && Time.unscaledTime >= retryAt) Load();
        }

        static void Load()
        {
            if (!loading && rewarded == null)
            {
                loading = true;
                RewardedAd.Load(RewardedId, new AdRequest(), (ad, err) =>
                {
                    loading = false;
                    if (err != null || ad == null) { retryAt = Time.unscaledTime + 30f; return; }
                    rewarded = ad;
                });
            }
            if (!loadingInterstitial && interstitial == null)
            {
                loadingInterstitial = true;
                InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, err) =>
                {
                    loadingInterstitial = false;
                    if (err != null || ad == null) { retryAt = Time.unscaledTime + 30f; return; }
                    interstitial = ad;
                });
            }
        }

        // done(true) once the player has earned the reward and the ad has closed; done(false) if it was skipped or failed.
        public static void ShowRewarded(Action<bool> done)
        {
            if (!RewardedReady) { done(false); return; }
            var ad = rewarded; rewarded = null; showing = true;
            bool earned = false, finished = false;
            void Finish()
            {
                if (finished) return; finished = true; showing = false; lastAdAt = Time.unscaledTime;
                ad.Destroy(); Load();
                AudioListener.pause = false;
                done(earned);
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            AudioListener.pause = true; // the ad brings its own sound
            ad.Show(_ => earned = true);
        }

        // Called when the player leaves the game-over screen; `then` runs afterwards whether or not an ad showed.
        public static void AfterFlight(Action then)
        {
            flightsSinceAd++;
            bool due = Save.D.runs >= FreeRuns && flightsSinceAd >= FlightsPerInterstitial && Time.unscaledTime - lastAdAt >= AdGap;
            if (!due || showing || interstitial == null || !interstitial.CanShowAd()) { then(); return; }
            var ad = interstitial; interstitial = null; showing = true;
            bool finished = false;
            void Finish()
            {
                if (finished) return; finished = true; showing = false;
                flightsSinceAd = 0; lastAdAt = Time.unscaledTime;
                ad.Destroy(); Load();
                AudioListener.pause = false;
                then();
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            AudioListener.pause = true;
            ad.Show();
        }

        public static void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(err => { if (err != null) Debug.LogWarning("Flightline ads: privacy options failed: " + err.Message); StartSdk(); });
        }
    }
}
