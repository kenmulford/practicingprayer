using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrayerApp.Services
{
    public interface IDBService
    {
        // SQLite requires parameterless constructors in the models
        Task<List<T>> GetAllAsync<T>() where T : new();
        Task<T> GetByIdAsync<T>(int Id) where T : new();
        Task<int> InsertAsync<T>(T item);
        Task<int> UpdateAsync<T>(T item);
        Task<int> DeleteAsync<T>(T item);
        Task<int> DropTableAsync<T>() where T : new();

        Task SeedDataAsync();

        Task UpdateSchema();
    }
}
