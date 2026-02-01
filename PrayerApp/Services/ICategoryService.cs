using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

public interface ICategoryService
{
    Task<IReadOnlyList<PrayerCategory>> GetCategoriesAsync();
}