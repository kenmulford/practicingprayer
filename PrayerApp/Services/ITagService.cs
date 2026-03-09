using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ITagService
{
    Task<IReadOnlyList<PrayerTag>> GetTagsAsync();
    Task<IReadOnlyList<PrayerTag>> GetTagsByCardIdAsync(int prayerCardId);
    Task<int> AddTagToCardAsync(int prayerCardId, int prayerTagId);
    Task<int> RemoveTagFromCardAsync(int prayerCardId, int prayerTagId);
    Task<PrayerTag> SaveTagAsync(PrayerTag tag);
    Task DeleteTagAsync(PrayerTag tag);
    Task<IReadOnlyList<int>> GetPrayerIdsByTagIdsAsync(IEnumerable<int> tagIds);
}
