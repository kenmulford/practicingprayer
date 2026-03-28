using Xunit;

namespace PrayerApp.UITests.Infrastructure;

/// <summary>
/// xUnit collection definition that shares a single AppiumSetup fixture
/// across all test classes in the collection. This means one app launch
/// for the entire test run, not per-class.
/// </summary>
[CollectionDefinition("Appium")]
public class AppiumCollection : ICollectionFixture<AppiumSetup>
{
}
