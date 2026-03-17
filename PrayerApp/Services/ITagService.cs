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

    // Deprecated card-level methods — kept for any remaining callers during transition
    [Obsolete("Tags are now per-request. Use GetTagsByRequestIdAsync instead.")]
    Task<IReadOnlyList<PrayerTag>> GetTagsByCardIdAsync(int prayerCardId);
    [Obsolete("Tags are now per-request. Use AddTagToRequestAsync instead.")]
    Task<int> AddTagToCardAsync(int prayerCardId, int prayerTagId);
    [Obsolete("Tags are now per-request. Use RemoveTagFromRequestAsync instead.")]
    Task<int> RemoveTagFromCardAsync(int prayerCardId, int prayerTagId);
    [Obsolete("Tags are now per-request. Use GetRequestIdsByTagIdsAsync instead.")]
    Task<IReadOnlyList<int>> GetPrayerIdsByTagIdsAsync(IEnumerable<int> tagIds);

    Task<PrayerTag> SaveTagAsync(PrayerTag tag);
    Task DeleteTagAsync(int tagId);
}
