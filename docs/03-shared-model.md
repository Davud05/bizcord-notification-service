# 03 – Shared model

`packages/Shared.Contracts` is the only thing other teams compile against. It contains data
contracts and nothing else: no logic, no dependencies, no infrastructure.

```
packages/Shared.Contracts/src/Bizcord.Shared.Contracts/
├── Events/
│   ├── MessagePostedEvent.cs      (owned by Messaging, consumed by us)
│   ├── MentionDetectedEvent.cs    (owned by us)
│   └── NotificationReadEvent.cs   (owned by us)
└── Models/
    ├── NotificationDto.cs
    ├── NotificationPreferenceDto.cs
    └── NotificationTypes.cs       (+ NotificationStatuses)
```

## Events

### `MessagePostedEvent` – the system-wide contract we consume

```csharp
public class MessagePostedEvent
{
    public Guid MessageId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid AuthorId { get; set; }
    public required string Content { get; set; }
    public DateTime PostedAt { get; set; }
}
```

Every field is used by us: `Content` for mention detection, `ChannelId` for preference
evaluation, `AuthorId` to avoid self-notification and to build the title, `MessageId` for
traceability, `PostedAt` for the stored reference. This is asserted in a consumer contract
test (see [07 – Testing](07-testing.md)).

### `MentionDetectedEvent` – what we publish

```csharp
public class MentionDetectedEvent
{
    public Guid MessageId { get; set; }       // carried over for traceability
    public Guid NotificationId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid RecipientId { get; set; }
    public required string MentionKind { get; set; }  // "User" | "Here" | "Everyone"
    public required string Preview { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

Designed for the services that will consume it:

* A **push gateway** needs `RecipientId`, `Preview` and `NotificationId` (deep link).
* **Analytics** needs `ChannelId`, `AuthorId` and `ProcessedAt`.
* **Everyone** needs `MessageId` so one user action can be traced through the whole system.

### `NotificationReadEvent`

Published the first time a notification is read. Useful for Presence ("last active"),
Analytics (engagement) and a push gateway (clear the badge on other devices).

## Shared models

`NotificationDto` is the only shape of a notification that leaves the service, and it is
what the REST API returns.

```csharp
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public Guid? ChannelId { get; set; }
    public Guid? MessageId { get; set; }
    public required string Type { get; set; }    // NotificationTypes
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Status { get; set; }  // NotificationStatuses
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
```

## What we hide, and why

| Internal detail | Why it stays internal |
| --- | --- |
| `NotificationType` / `NotificationStatus` enums | Exposed as strings. Adding an internal type must not break a consumer's deserialization, and an enum's numeric value is a terrible thing to version. |
| `NotificationContent`, `Recipient`, `MessageReference` value objects | They carry our invariants. Consumers get flat fields instead of our object graph. |
| `NotificationPreference.Decide` and `DeliveryDecision` | *How* we decide whether to notify is our business logic. Consumers only ever see the outcome: an event exists, or it does not. |
| Mute bookkeeping, quiet-hour evaluation | `NotificationPreferenceDto` exposes the flags and the muted channel ids, not the evaluation. |
| The repository, the store, the broker configuration | Implementation. |

The same principle the assignment describes for roles applies here: internally the decision
is a small rule engine, externally it is three booleans and a list.

## Versioning policy

1. **Additive changes only** on published events: a new optional field is fine, renaming or
   removing one is not.
2. **Strings for enumerations** in contracts, with the known values as constants
   (`NotificationTypes`, `NotificationStatuses`). An unknown value must be treated as
   "something new", not as a crash.
3. **Breaking changes get a new type** (`MentionDetectedEventV2`) published alongside the
   old one until consumers have migrated.
4. **The shape is enforced by the compiler, the values by contract tests.** The shared
   project means a removed field breaks the build; a field that is present but always
   `Guid.Empty` is what the contract tests in `tests/.../ServiceTests` and
   `tests/.../IntegrationTests` catch.
5. `MessageId` is mandatory in every event we publish, so a single user action stays
   traceable across services.
