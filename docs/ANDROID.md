# Android release (Google Play, ads + REMOVE ADS)

How the game earns: one full-screen ad between runs (when a new TAPE starts, never the first, at most one every 3 minutes of play), and a one-time **REMOVE ADS** purchase (about RM10) that turns ads off forever and comes back on a new phone. All of it is in `Assets/Backrooms/Monetization.cs`, which starts itself on phones; nothing to add to the scene. The start screen gets REMOVE ADS (bottom-left, with the store's price) and RESTORE (bottom-right).

The same file serves iOS. Steam/desktop builds never show ads or the buttons.

## 1. Accounts (once, in a browser)

1. **Google Play Console**: https://play.google.com/console, sign up as an individual developer, pay the one-time USD 25, verify your ID. New personal accounts must run a **closed test with at least 12 testers for 14 days** before Google lets you publish to everyone, so start this early (your brother counts).
2. **Payments profile**: Play Console > Settings > Payments profile. Needed before you can sell REMOVE ADS. Add your bank account so payouts reach you.
3. **AdMob**: https://admob.google.com, sign in with the same Google account, Malaysia, MYR.
   - Apps > Add app > Android > "not published yet" > name *Endless Backrooms*. Copy the **App ID** (`ca-app-pub-...~...`).
   - In that app, Ad units > Add > **Interstitial**, name it *Between runs*. Copy the **ad unit ID** (`ca-app-pub-.../...`).
   - Payments: add your bank details. AdMob pays once your balance passes the threshold (about USD 100).
   - Privacy & messaging > GDPR > create a consent message for the app and publish it (the game shows it automatically to players in Europe).

## 2. Unity packages (once, in your Unity project)

1. **Google Mobile Ads**: download the latest `GoogleMobileAds-v*.unitypackage` from https://github.com/googleads/googleads-mobile-unity/releases, then Assets > Import Package > Custom Package. Say yes when it asks to enable Android auto-resolution.
2. Assets > Google Mobile Ads > Settings: paste the **Android App ID** (iOS App ID goes here too later).
3. **In-App Purchasing**: Window > Package Management > Package Manager > Unity Registry > *In-App Purchasing* > Install (version 5.x).
4. Edit > Project Settings > Player > Android tab > Other Settings > **Scripting Define Symbols**: add `ADMOB` and `UNITY_IAP`, Apply. (Until you do, the game builds fine with no ads and no purchase.)
5. Open `Assets/Backrooms/Monetization.cs` and replace `AndroidInterstitial` with your ad unit ID. Leave Google's test id in while you are testing on your own phone; tapping your own real ads can get the AdMob account banned.

## 3. Android build settings

File > Build Profiles > Android > Switch Platform, then Player Settings:

| Setting | Value |
|---|---|
| Company Name / Product Name | `syafiq` / `Endless Backrooms` |
| Package Name | `com.syafiqdev.endlessbackrooms` (permanent once uploaded) |
| Version / Bundle Version Code | `1.0` / `1`, add 1 to the code for every upload |
| Default Orientation | Landscape Left |
| Scripting Backend | IL2CPP |
| Target Architectures | ARM64 (Play requires 64-bit) |
| Minimum API Level | Android 7.0 (API 24) |
| Target API Level | Automatic (highest installed) |
| Publishing Settings > Keystore Manager | Create a new keystore, keep the file and passwords somewhere safe (cloud drive). You need it for every update. |
| Build Profiles > Build App Bundle (Google Play) | On, so Build makes an `.aab` |

If the ads plugin complains about Gradle, tick **Custom Main Gradle Template** and **Custom Gradle Properties Template** in Publishing Settings and run Assets > External Dependency Manager > Android Resolver > Force Resolve.

## 4. Play Console listing

1. Create app > *Endless Backrooms*, Game, Free.
2. Monetize > Products > In-app products > Create: Product ID **`remove_ads`** (must match the code), name *Remove ads*, price RM 9.99. Activate.
3. App content: Privacy policy URL `https://<your vercel site>/privacy.html` (fill in the contact email in `public/privacy.html` first). Ads: **Yes, contains ads**. Target audience: 13+ (keeps you out of the stricter kids' rules; it's a horror game anyway). Content rating questionnaire: horror/fear themes, no violence or blood. Data safety: device or other IDs, collected by the AdMob SDK, for advertising and analytics, shared with Google; no account, no other personal data.
4. Store listing: icon 512x512, feature graphic 1024x500, at least 2 landscape screenshots, short and full description.
5. `app-ads.txt`: once the Play listing names your website, put a file at `public/app-ads.txt` with the line AdMob shows you (Apps > app-ads.txt), so ad buyers trust your inventory.

## 5. Test, then release

1. Testing > **Internal testing** > upload the `.aab`, add your and your brother's Gmail as testers, open the opt-in link on your phones. The purchase only works from a Play-installed build, not a USB Build And Run.
2. Settings > **License testing**: add the same Gmail addresses, so buying REMOVE ADS uses a test card and costs nothing.
3. Check: an ad shows after a run once 3 minutes have passed; REMOVE ADS shows the RM price and buying it removes the buttons and the ads; uninstall, reinstall, tap RESTORE (or just wait, it restores on launch).
4. Testing > **Closed testing**: 12+ testers for 14 days, then apply for production in the dashboard.
5. Before production: real ad unit ID in `Monetization.cs`, Bundle Version Code bumped, upload, roll out.
