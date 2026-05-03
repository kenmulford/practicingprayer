#if IOS
using Foundation;
using PrayerApp.Services;
using PrayerApp.Shared;

namespace PrayerApp.Platforms.iOS;

public sealed class NsFileManagerAppGroupContainerProvider : IAppGroupContainerProvider
{
    public string? ResolveContainerPath()
    {
        var url = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConstants.AppGroupId);
        return url?.Path;
    }
}
#endif
