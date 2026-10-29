# iOS release (App Store, free with ads + "Remove ads" RM 9.90)

Same model as Android: free, ads between runs, one-time purchase removes them forever. The ads and purchase code is shared with Android (see the Android notes); this page is only what's different on iPhone.

## 1. What you need (one time)

- **Mac with Xcode** (free, Mac App Store). iOS builds can only be made on a Mac.
- **Apple Developer Program**, US$99 a year, at developer.apple.com/programs. Needed for TestFlight and the App Store. Sign up as an individual; approval can take a day or two.
- **An iPhone** to test on. Without one, use TestFlight on a friend's or family member's iPhone.
- **Unity Hub**: install Unity 6 LTS with the **iOS Build Support** module.

## 2. App Store Connect (appstoreconnect.apple.com)

1. **Business > Agreements**: sign the **Paid Apps** agreement and fill in bank + tax. Purchases (even test ones) don't work until this is done.
2. **Apps > + New App**: name "Endless Backrooms", bundle ID `com.syafiqdev.endlessbackrooms` (create it under Certificates, Identifiers & Profiles first; same ID goes in Unity).
3. In the app, **Monetization > In-App Purchases > +**: type **Non-Consumable**, product ID `remove_ads` (must match the code), price **RM 9.90**, name "Remove Ads". Add a screenshot of the button for review.
4. **App Privacy**: follow Google's AdMob guide for the App Store privacy form ("Google Mobile Ads SDK data disclosure"). In short: Identifiers (Device ID), Usage Data (Product Interaction, Advertising Data), Diagnostics, Location (coarse), used for Third-Party Advertising and Analytics. A **privacy policy URL** is required (a short page on the Vercel site is fine).
5. **Age rating**: horror themes, no violence shown, so expect 12+.

## 3. Unity settings (File > Build Profiles > iOS > Switch Platform, then Player Settings)

- Company Name, Product Name "Endless Backrooms", Bundle Identifier `com.syafiqdev.endlessbackrooms`, Version `1.0`, Build `1` (raise Build every upload).
- **Target Device: iPhone Only**, so App Review doesn't ask for iPad screenshots.
- **Signing Team ID**: your Apple team ID (developer.apple.com > Membership), **Automatically Sign** on.
- **Requires Persistent WiFi** off, orientation **Landscape Left + Right**.
- Ads are Google AdMob (shared with Android). In admob.google.com add an **iOS app** (separate from Android) and an interstitial ad unit. Put the iOS **App ID** in **Assets > Google Mobile Ads > Settings** and the ad unit ID in `IosInterstitial` at the top of `Monetization.cs`. Until then Google's test ads show.

`Assets/Backrooms/Editor/IosPostBuild.cs` sets the no-encryption flag in Info.plist after every build (skips the export question on each upload). The AdMob plugin adds its own App ID and SKAdNetwork IDs.

**No tracking prompt (ATT).** In the AdMob dashboard, leave the "IDFA explainer" message off. The game doesn't ask "Allow tracking?", so iPhone ads are non-personalised. That's simpler and passes review; it pays a bit less per ad. Can be added later if ad income matters.

## 4. Build and test

1. Unity **Build** into a folder (e.g. `Builds/iOS`). It makes an Xcode project.
2. Open `Unity-iPhone.xcodeproj`, plug in the iPhone, pick it at the top, press **Run**. First time: on the phone, Settings > General > VPN & Device Management > trust your developer profile.
3. Test the purchase with a **Sandbox account** (App Store Connect > Users and Access > Sandbox). On the iPhone: Settings > App Store > Sandbox Account. Buy, delete the app, reinstall, tap **RESTORE**: ads must stay off.

## 5. Upload and submit

1. Xcode: **Product > Archive**, then **Distribute App > App Store Connect > Upload**.
2. It shows up under **TestFlight** after 10-30 minutes. Add yourself (and your brother) as testers.
3. When it's good: on the app page fill description, keywords, support URL, screenshots (6.9" iPhone, landscape is fine), pick the build, attach the `remove_ads` purchase, **Submit for Review**. Review usually takes 1-2 days.

## App Review checklist

- The **RESTORE** button is on the start screen (Apple rejects non-consumable purchases without one).
- The purchase is attached to the version you submit.
- Privacy policy URL works.
- No mentions of Android or Google Play anywhere in the app or screenshots.
