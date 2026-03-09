using PrayerApp.Models;

namespace PrayerApp.Services;

public class PrayerInteractionService : IPrayerInteractionService
{
    public async Task LogInteractionAsync(int prayerId)
    {
        var interaction = new PrayerInteraction
        {
            PrayerId = prayerId,
            InteractionType = "Prayed"
        };
        await interaction.SaveAsync();
    }
}
