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

    // ── Request-level tag methods (current) ────────────────────────────────

    public async Task<IReadOnlyList<PrayerTag>> GetTagsByRequestIdAsync(int prayerRequestId)
    {
        var requestTags = await _dbService.GetByRequestIdAsync(prayerRequestId);
        var tagIds = requestTags.Select(rt => rt.PrayerTagId).ToHashSet();
        var allTags = await GetTagsAsync();

        var tags = allTags.Where(t => tagIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();
        return new ReadOnlyCollection<PrayerTag>(tags);
    }

    public async Task<int> AddTagToRequestAsync(int prayerRequestId, int prayerTagId)
    {
        var requestTag = new PrayerCardTag
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
        var requestTags = await _dbService.GetByRequestIdAsync(prayerRequestId);
        var toDelete = requestTags.FirstOrDefault(rt => rt.PrayerTagId == prayerTagId);

        if (toDelete is null)
            return 0;

        var result = await _dbService.DeleteAsync(toDelete);
        InvalidateCache();
        return result;
    }

    public async Task<IReadOnlyList<int>> GetRequestIdsByTagIdsAsync(IEnumerable<int> tagIds)
    {
        var requestIds = new HashSet<int>();
        foreach (var tagId in tagIds)
        {
            var rows = await _dbService.GetByTagIdAsync(tagId);
            foreach (var r in rows)
                if (r.PrayerRequestId > 0)
                    requestIds.Add(r.PrayerRequestId);
        }
        return requestIds.ToList().AsReadOnly();
    }

    public async Task<PrayerTag> SaveTagAsync(PrayerTag tag)
    {
        await tag.SaveAsync();
        InvalidateCache();
        return tag;
    }

    public async Task DeleteTagAsync(int tagId)
    {
        // Remove all junction rows first so no orphans remain
        var junctionRows = await PrayerCardTag.LoadByTagIdAsync(tagId);
        foreach (var row in junctionRows)
            await row.DeleteAsync();

        var tag = await PrayerTag.LoadAsync(tagId);
        if (tag is null) return;
        await tag.DeleteAsync();
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        _cache = null;
    }
}
