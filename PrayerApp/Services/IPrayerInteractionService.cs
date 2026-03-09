namespace PrayerApp.Services;

public interface IPrayerInteractionService
{
    Task LogInteractionAsync(int prayerId);
}
