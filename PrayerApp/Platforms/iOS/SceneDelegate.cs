using Foundation;
using Microsoft.Maui;

namespace PrayerApp;

// iOS 26 faults at launch without UIScene adoption; iOS 27 SDK promotes it
// to an assert. MauiUISceneDelegate bridges scene callbacks through the
// existing AppDelegate wiring, so no overrides are needed here.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
