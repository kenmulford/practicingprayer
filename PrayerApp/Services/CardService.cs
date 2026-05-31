using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Messages;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CardService : ICardService
{
    public const string QuickAddTitle = PrayerCard.TitleQuickAdd;
    public const string SharedWithMeTitle = PrayerCard.TitleSharedWithMe;

    private IReadOnlyList<PrayerCard>? _cache;
    private readonly IMessenger _messenger;
    private readonly ISettings _settings;

    public CardService(IMessenger messenger, ISettings settings)
    {
        _messenger = messenger;
        _settings = settings;
    }

    public async Task<IReadOnlyList<PrayerCard>> GetCardsAsync()
    {
        if (_cache is not null)
            return _cache;

        var list = await PrayerCard.LoadAllAsync();

        var readOnly = new ReadOnlyCollection<PrayerCard>(list.ToList());
        _cache = readOnly;
        return _cache;
    }

    public Task<PrayerCard> GetOrCreateQuickAddCardAsync()
        => GetOrCreateSystemCardAsync(QuickAddTitle, PrayerCard.SystemKeyQuickAdd);

    public Task<PrayerCard> GetOrCreateSharedCardAsync()
        => GetOrCreateSystemCardAsync(SharedWithMeTitle, PrayerCard.SystemKeySharedWithMe);

    private async Task<PrayerCard> GetOrCreateSystemCardAsync(string title, string systemKey)
    {
        var cards = await GetCardsAsync();
        var existing = cards.FirstOrDefault(c => c.IsSystem && c.Title == title);
        if (existing is not null)
            return existing;

        // Look up the System box so new system cards land in the right collection
        var boxes = await CardBox.LoadAllAsync();
        var sysBox = boxes.FirstOrDefault(b => b.SystemKey == CardBox.SystemKeySystem);

        var card = new PrayerCard
        {
            Title = title,
            IsSystem = true,
            SystemKey = systemKey,
            BoxId = sysBox?.Id ?? 0
        };
        await card.SaveAsync();
        _cache = null;
        // Seed-only path: no broadcast.
        return card;
    }

    public async Task<PrayerCard> SaveCardAsync(PrayerCard card, bool publishMessage = true)
    {
        var isNew = card.Id == 0;
        await card.SaveAsync();
        _cache = null;
        if (publishMessage)
            _messenger.Send(new PrayerCardChangedMessage(card.Id, isNew ? ChangeKind.Created : ChangeKind.Updated));
        return card;
    }

    public async Task AssignBoxAsync(PrayerCard card, int boxId)
    {
        var archivedId = _settings.ArchivedFolderId;
        if (archivedId > 0)
        {
            if (boxId == archivedId && card.BoxId != archivedId)
                card.PreArchiveBoxId = card.BoxId;
            else if (card.BoxId == archivedId && boxId != archivedId)
                card.PreArchiveBoxId = 0;
        }

        card.BoxId = boxId;
        await card.SaveAsync();
        _cache = null;
        _messenger.Send(new PrayerCardChangedMessage(card.Id, ChangeKind.Updated));
    }

    public Task ArchiveCardAsync(PrayerCard card)
    {
        if (card.IsSystem || !string.IsNullOrEmpty(card.SystemKey))
            return Task.CompletedTask;

        var archivedId = _settings.ArchivedFolderId;
        if (archivedId <= 0 || card.BoxId == archivedId)
            return Task.CompletedTask;

        return AssignBoxAsync(card, archivedId);
    }

    public async Task UnarchiveCardAsync(PrayerCard card)
    {
        var archivedId = _settings.ArchivedFolderId;
        if (archivedId <= 0 || card.BoxId != archivedId)
            return;

        var targetBoxId = card.PreArchiveBoxId;
        if (targetBoxId > 0)
        {
            var boxes = await CardBox.LoadAllAsync();
            if (boxes.All(b => b.Id != targetBoxId))
                targetBoxId = 0;
        }

        card.PreArchiveBoxId = 0;
        await AssignBoxAsync(card, targetBoxId);
    }

    public async Task DeleteCardAsync(PrayerCard card, bool publishMessage = true)
    {
        var deletedId = card.Id;
        await card.DeleteAsync();
        _cache = null;
        if (publishMessage)
            _messenger.Send(new PrayerCardChangedMessage(deletedId, ChangeKind.Deleted));
    }

    public void InvalidateCache()
    {
        _cache = null;
    }
}
