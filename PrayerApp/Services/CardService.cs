using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CardService : ICardService
{
    private IReadOnlyList<PrayerCard>? _cache;

    public async Task<IReadOnlyList<PrayerCard>> GetCardsAsync()
    {
        if (_cache is not null)
            return _cache;

        var list = await PrayerCard.LoadAllAsync();

        var readOnly = new ReadOnlyCollection<PrayerCard>(list.ToList());
        _cache = readOnly;
        return _cache;
    }
}
