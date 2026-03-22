using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class TagService : ITagService
{
    internal const string RecentlyNotifiedTagName = "Recently Notified";
    private const string RecentlyNotifiedTagColor = "#505050";

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
        var sorted = list
            .OrderByDescending(t => t.IsSystem)  // system tags first
            .ThenBy(t => t.Name)
            .ToList();

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

        // allTags is pre-sorted (system first, then alpha); Where preserves order
        var tags = allTags.Where(t => tagIds.Contains(t.Id)).ToList();
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
        var tag = await PrayerTag.LoadAsync(tagId);
        if (tag is null || tag.IsSystem) return; // System tags cannot be deleted

        // Remove all junction rows first so no orphans remain
        await ClearAllAssignmentsForTagAsync(tagId);
        await tag.DeleteAsync();
        InvalidateCache();
    }

    public async Task ReassignColorAsync(string oldColorHex, string newColorHex)
    {
        var allTags = await GetTagsAsync();
        foreach (var tag in allTags)
        {
            if (string.Equals(tag.Color, oldColorHex, StringComparison.OrdinalIgnoreCase))
            {
                tag.Color = newColorHex;
                await tag.SaveAsync();
            }
        }
        InvalidateCache();
    }

    public async Task ClearAllAssignmentsForTagAsync(int tagId)
    {
        await _dbService.DeleteByTagIdAsync(tagId);
        InvalidateCache();
    }

    public async Task SeedSystemTagsAsync()
    {
        var allTags = await PrayerTag.LoadAllAsync();
        var exists = allTags.Any(t =>
            string.Equals(t.Name, RecentlyNotifiedTagName, StringComparison.OrdinalIgnoreCase));

        if (exists) return;

        var tag = new PrayerTag
        {
            Name = RecentlyNotifiedTagName,
            IsSystem = true,
            Color = RecentlyNotifiedTagColor
        };
        await tag.SaveAsync();
        InvalidateCache();
    }

    public async Task<PrayerTag?> GetSystemTagAsync(string name)
    {
        var allTags = await GetTagsAsync();
        return allTags.FirstOrDefault(t =>
            t.IsSystem && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void InvalidateCache()
    {
        _cache = null;
    }
}
