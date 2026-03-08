using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

public interface ICardService
{
    Task<IReadOnlyList<PrayerCard>> GetCardsAsync();
}
