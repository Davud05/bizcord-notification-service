# Bizcord.Shared.Contracts

The public surface of Bizcord: the integration events and data models that services are
allowed to depend on. **Data contracts only** — no logic, no dependencies, no infrastructure.

If it is in here, changing it is a cross-team conversation. If it is not, it is somebody's
implementation detail.

## Contents

| Contract | Owner | Direction |
| --- | --- | --- |
| `MessagePostedEvent` | Messaging | published by Messaging, consumed by Notifications, Search, … |
| `MentionDetectedEvent` | Notifications | published by Notifications |
| `NotificationReadEvent` | Notifications | published by Notifications |
| `NotificationDto` | Notifications | returned by the Notification REST API |
| `NotificationPreferenceDto` | Notifications | returned by the Notification REST API |
| `NotificationTypes`, `NotificationStatuses` | Notifications | the known string values |

## Rules for this project

1. **Data only.** No behaviour, no validation, no `using` beyond the base class library. A
   shared project with logic in it turns a monorepo into a distributed monolith.
2. **Additive changes only.** Adding an optional field is safe. Renaming or removing one
   breaks every consumer; that needs a new type (`…EventV2`) published alongside the old one.
3. **Strings, not enums, for enumerations.** Enum numbering is a versioning trap. The known
   values are exposed as constants, and an unknown value must be handled as "something new"
   rather than as a crash.
4. **Only what integration needs.** Internal value objects, state machines and business rules
   stay inside their service. See
   [docs/03 – Shared model](../../docs/03-shared-model.md#what-we-hide-and-why).
5. **Every event we publish carries `MessageId`** where one exists, so a single user action
   can be traced across services.
