using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ITagService
{
    Task<IReadOnlyList<PrayerTag>> GetTagsAsync();

    // Request-level tag methods (current)
    Task<IReadOnlyList<PrayerTag>> GetTagsByRequestIdAsync(int prayerRequestId);
    Task<int> AddTagToRequestAsync(int prayerRequestId, int prayerTagId);
    Task<int> RemoveTagFromRequestAsync(int prayerRequestId, int prayerTagId);
    Task<IReadOnlyList<int>> GetRequestIdsByTagIdsAsync(IEnumerable<int> tagIds);

    Task<PrayerTag> SaveTagAsync(PrayerTag tag);
    Task DeleteTagAsync(int tagId);
}
