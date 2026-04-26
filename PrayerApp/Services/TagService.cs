using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Messages;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class TagService : ITagService
{
    internal const string RecentlyNotifiedTagName = "Recently Notified";
    private const string RecentlyNotifiedTagColor = "#505050";

    private IReadOnlyList<PrayerTag>? _cache;
    private readonly IDBService _dbService;
    private readonly IMessenger _messenger;

    public TagService(IDBService dbService, IMessenger messenger)
    {
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
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
        var rows = await _dbService.GetByTagIdsAsync(tagIds);
        return rows
            .Where(r => r.PrayerRequestId > 0)
            .Select(r => r.PrayerRequestId)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    public async Task<PrayerTag> SaveTagAsync(PrayerTag tag)
    {
        var isNew = tag.Id == 0;
        await tag.SaveAsync();
        InvalidateCache();
        _messenger.Send(new TagChangedMessage(tag.Id, isNew ? ChangeKind.Created : ChangeKind.Updated));
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
        _messenger.Send(new TagChangedMessage(tagId, ChangeKind.Deleted));
    }

    public async Task ReassignColorAsync(string oldColorHex, string newColorHex)
    {
        // Snapshot tag IDs before invalidating, so we don't mutate cached objects
        var allTags = await GetTagsAsync();
        var idsToUpdate = allTags
            .Where(t => string.Equals(t.Color, oldColorHex, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Id)
            .ToList();

        if (idsToUpdate.Count == 0) return;

        InvalidateCache(); // Invalidate first so concurrent readers get fresh data

        foreach (var id in idsToUpdate)
        {
            var tag = await PrayerTag.LoadAsync(id);
            if (tag is null) continue;
            tag.Color = newColorHex;
            await tag.SaveAsync();
        }

        // Many tags changed at once → single summary signal so subscribers do one resync.
        _messenger.Send(new BulkChangedMessage());
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

    public void InvalidateCache()
    {
        _cache = null;
    }
}
