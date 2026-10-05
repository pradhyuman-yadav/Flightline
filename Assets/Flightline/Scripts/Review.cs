using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Flightline
{
    // Native store review card (Google Play on Android, StoreKit on iOS) + store / privacy links.
    // Policy: never ask "do you like the game?" first, never gate on the answer. Google decides whether the card shows.
    public static class Review
    {
        public const string PrivacyUrl = "https://thepk.in/privacy";
        // Fill in after creating the app in App Store Connect (App Information > Apple ID, digits only).
        public const string AppStoreId = "6819109667";
#if UNITY_IOS
        public static bool HasStorePage => AppStoreId.Length > 0;
        public static string StoreUrl => $"https://apps.apple.com/app/id{AppStoreId}?action=write-review";
#else
        public static bool HasStorePage => true;
        public static string StoreUrl => "https://play.google.com/store/apps/details?id=" + Application.identifier;
#endif

        const int MinFlights = 5, MaxAsks = 3, CooldownDays = 30;

        public static void OpenStore() { Save.D.rated = true; Save.Write(); Application.OpenURL(StoreUrl); }
        static bool askedThisSession;
        public static void OpenPrivacy() => Application.OpenURL(PrivacyUrl);

        // Call on a high point (new best or first arrival in a zone), on the game-over screen.
        public static void MaybeAsk(bool highPoint)
        {
            var d = Save.D;
            if (!highPoint || d.rated || d.runs < MinFlights || d.reviewAsks >= MaxAsks) return;
            if (d.reviewAsks > 0 && DateTime.UtcNow.Ticks - d.lastReviewAsk < TimeSpan.FromDays(CooldownDays).Ticks) return;
            d.reviewAsks++; d.lastReviewAsk = DateTime.UtcNow.Ticks; Save.Write(); askedThisSession = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            Launch();
#elif UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview(); // Apple shows it at most 3 times a year
#else
            Debug.Log($"Flightline: Play review card would show here (ask {d.reviewAsks}/{MaxAsks}).");
#endif
        }

        // Personal "please rate" card: Android only (Apple 5.6.1 bans custom review prompts), 8+ flights,
        // never in the same session as the system card, at most twice, 45 days apart, never after the player opened the store.
        public static bool ShouldShowCard()
        {
#if UNITY_IOS
            return false;
#else
            var d = Save.D;
            if (d.rated || askedThisSession || d.runs < 8 || d.rateCardShown >= 2) return false;
            return d.rateCardShown == 0 || DateTime.UtcNow.Ticks - d.rateCardLast >= TimeSpan.FromDays(45).Ticks;
#endif
        }

        public static void MarkCardShown() { var d = Save.D; d.rateCardShown++; d.rateCardLast = DateTime.UtcNow.Ticks; askedThisSession = true; Save.Write(); }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject manager, activity;
        static Done listener; // keep the proxy referenced until Play calls back

        static void Launch()
        {
            try
            {
                activity = UnityEngine.Android.AndroidApplication.currentActivity;
                manager = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory").CallStatic<AndroidJavaObject>("create", activity);
                var task = manager.Call<AndroidJavaObject>("requestReviewFlow");
                task.Call<AndroidJavaObject>("addOnCompleteListener", listener = new Done(t =>
                {
                    if (!t.Call<bool>("isSuccessful")) return;
                    var info = t.Call<AndroidJavaObject>("getResult");
                    manager.Call<AndroidJavaObject>("launchReviewFlow", activity, info);
                }));
            }
            catch (Exception e) { Debug.LogWarning("Flightline review: " + e.Message); }
        }

        [Preserve]
        class Done : AndroidJavaProxy
        {
            readonly Action<AndroidJavaObject> cb;
            public Done(Action<AndroidJavaObject> cb) : base("com.google.android.gms.tasks.OnCompleteListener") { this.cb = cb; }
            [Preserve] public void onComplete(AndroidJavaObject task)
            {
                try { cb(task); } catch (Exception e) { Debug.LogWarning("Flightline review: " + e.Message); }
            }
        }
#endif
    }
}
