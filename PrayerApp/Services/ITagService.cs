using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ITagService
{
    Task<IReadOnlyList<PrayerTag>> GetTagsAsync();
    Task<IReadOnlyList<PrayerTag>> GetTagsByRequestIdAsync(int prayerRequestId);
    Task<int> AddTagToRequestAsync(int prayerRequestId, int prayerTagId);
    Task<int> RemoveTagFromRequestAsync(int prayerRequestId, int prayerTagId);
    Task<PrayerTag> SaveTagAsync(PrayerTag tag);
    Task DeleteTagAsync(PrayerTag tag);
}
