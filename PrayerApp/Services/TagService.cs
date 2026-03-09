using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class TagService : ITagService
{
    private IReadOnlyList<PrayerTag>? _cache;
    private readonly IDBService _dbService;

    public TagService(IDBService dbService)
    {
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
    }

    public async Task<IReadOnlyList<PrayerTag>> GetTagsAsync()
    {
        if (_cache is not null)
            return _cache;

        var list = await PrayerTag.LoadAllAsync();
        var sorted = list.OrderBy(t => t.Name).ToList();

        var readOnly = new ReadOnlyCollection<PrayerTag>(sorted);
        _cache = readOnly;
        return _cache;
    }

    public async Task<IReadOnlyList<PrayerTag>> GetTagsByRequestIdAsync(int prayerRequestId)
    {
        var requestTags = await PrayerRequestTag.LoadByRequestIdAsync(prayerRequestId);
        var tagIds = requestTags.Select(rt => rt.PrayerTagId).ToHashSet();
        var allTags = await GetTagsAsync();

        var tags = allTags.Where(t => tagIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();
        return new ReadOnlyCollection<PrayerTag>(tags);
    }

    public async Task<int> AddTagToRequestAsync(int prayerRequestId, int prayerTagId)
    {
        var requestTag = new PrayerRequestTag
        {
            PrayerRequestId = prayerRequestId,
            PrayerTagId = prayerTagId
        };

        var result = await _dbService.InsertAsync(requestTag);
        InvalidateCache();
        return result;
    }

    public async Task<int> RemoveTagFromRequestAsync(int prayerRequestId, int prayerTagId)
    {
        var requestTags = await PrayerRequestTag.LoadByRequestIdAsync(prayerRequestId);
        var toDelete = requestTags.FirstOrDefault(rt => rt.PrayerTagId == prayerTagId);

        if (toDelete is null)
            return 0;

        var result = await _dbService.DeleteAsync(toDelete);
        InvalidateCache();
        return result;
    }

    public async Task<PrayerTag> SaveTagAsync(PrayerTag tag)
    {
        await tag.SaveAsync();
        _cache = null;
        return tag;
    }

    public async Task DeleteTagAsync(PrayerTag tag)
    {
        await tag.DeleteAsync();
        _cache = null;
    }

    public async Task<IReadOnlyList<int>> GetPrayerIdsByTagIdsAsync(IEnumerable<int> tagIds)
    {
        var prayerIds = new HashSet<int>();
        foreach (var tagId in tagIds)
        {
            var requestTags = await _dbService.GetByTagIdAsync(tagId);
            foreach (var rt in requestTags)
                prayerIds.Add(rt.PrayerRequestId);
        }
        return prayerIds.ToList().AsReadOnly();
    }

    private void InvalidateCache()
    {
        _cache = null;
    }
}
