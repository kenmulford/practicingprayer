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

    public async Task<IReadOnlyList<PrayerTag>> GetTagsByCardIdAsync(int prayerCardId)
    {
        var cardTags = await PrayerCardTag.LoadByCardIdAsync(prayerCardId);
        var tagIds = cardTags.Select(ct => ct.PrayerTagId).ToHashSet();
        var allTags = await GetTagsAsync();

        var tags = allTags.Where(t => tagIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();
        return new ReadOnlyCollection<PrayerTag>(tags);
    }

    public async Task<int> AddTagToCardAsync(int prayerCardId, int prayerTagId)
    {
        var cardTag = new PrayerCardTag
        {
            PrayerCardId = prayerCardId,
            PrayerTagId = prayerTagId
        };

        var result = await _dbService.InsertAsync(cardTag);
        InvalidateCache();
        return result;
    }

    public async Task<int> RemoveTagFromCardAsync(int prayerCardId, int prayerTagId)
    {
        var cardTags = await PrayerCardTag.LoadByCardIdAsync(prayerCardId);
        var toDelete = cardTags.FirstOrDefault(ct => ct.PrayerTagId == prayerTagId);

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
            var cardTags = await _dbService.GetByTagIdAsync(tagId);
            foreach (var ct in cardTags)
                prayerIds.Add(ct.PrayerCardId);
        }
        return prayerIds.ToList().AsReadOnly();
    }

    private void InvalidateCache()
    {
        _cache = null;
    }
}
