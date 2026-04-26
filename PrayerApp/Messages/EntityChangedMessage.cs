namespace PrayerApp.Messages;

/// <summary>
/// Discrete kinds of entity-state change a service can broadcast.
/// </summary>
public enum ChangeKind
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Published by <see cref="Services.ICardService"/> after Save/Delete/AssignBox.
/// </summary>
public sealed record PrayerCardChangedMessage(int CardId, ChangeKind Kind);

/// <summary>
/// Published by <see cref="Services.IPrayerService"/> after Save/Delete.
/// <paramref name="CardId"/> is captured before the mutation completes so deleted
/// prayers still carry their owning-card context for subscribers.
/// </summary>
public sealed record PrayerChangedMessage(int PrayerId, int CardId, ChangeKind Kind);

/// <summary>
/// Published by <see cref="Services.ITagService"/> after Save/Delete of a tag itself.
/// Junction-table mutations (AddTagToRequestAsync / RemoveTagFromRequestAsync) do
/// not publish; the parent prayer's save publishes a <see cref="PrayerChangedMessage"/>
/// that already implies the tag set may have changed.
/// </summary>
public sealed record TagChangedMessage(int TagId, ChangeKind Kind);

/// <summary>
/// Published by <see cref="Services.IBoxService"/> after Save/Delete of a box itself.
/// Cascade or unassign side-effects on cards are signalled via <see cref="BulkChangedMessage"/>.
/// </summary>
public sealed record CardBoxChangedMessage(int BoxId, ChangeKind Kind);

/// <summary>
/// Single summary message for any operation that mutates many entities at once
/// (Backup restore, DeepLink import, Tag color reassign, Box delete with cascade).
/// Subscribers should respond with a full re-sync of their state rather than
/// trying to interpret which specific entities changed. Bulk-operation publishers
/// must NOT also fire granular per-entity messages alongside this; those would
/// only cause subscribers to resync N+1 times for one logical change.
/// </summary>
public sealed record BulkChangedMessage();
