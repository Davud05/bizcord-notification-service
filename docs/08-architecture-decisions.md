# 08 – Architecture decisions

Short records of the decisions that shaped the code, including what we rejected.

## ADR-1 – The Notification service is our bounded context

**Status:** accepted

**Context.** The assignment lets us pick one domain of Bizcord. The candidates were
Messaging (the obvious core), Channels, Presence, Search and Notifications.

**Decision.** Build the Notification service, as a Track B consumer of `MessagePostedEvent`.

**Why.** It is downstream of the shared contract, so it exercises inter-service
communication from day one instead of only publishing into the void. It has real business
rules (mention kinds, mutes, quiet hours), which gives us a domain model worth designing.
And it is the one service every real system has.

**Consequences.** We depend on three other teams' work (Messaging, Users, Channels), so we
had to decide what happens when they are absent — which is the interesting part of the
course. WireMock stands in for them.

## ADR-2 – In-memory repositories behind a port

**Status:** accepted, with a known follow-up

**Context.** The assignment does not require persistence, and a database would add migration
and container complexity that is not being assessed.

**Decision.** `INotificationRepository` and `INotificationPreferenceRepository` live in the
domain; `Infrastructure` implements them with `ConcurrentDictionary`.

**Why.** The boundary is what the design is about; the storage technology is not. Keeping
the port means swapping in EF Core and PostgreSQL is a single project change with no effect
on the domain, the application layer or the tests.

**Consequences.** Data is lost on restart, and running two replicas would give each its own
notifications. Both are acceptable for the exercise and documented rather than hidden.

## ADR-3 – Events for Messaging → Notification, REST for lookups

**Status:** accepted

**Context.** We need message content (large, high volume, produced by another team) and user
and channel details (small, needed immediately).

**Decision.** Consume `MessagePostedEvent` asynchronously over RabbitMQ; read users and
channels synchronously over HTTP.

**Why.** Posting a message must never wait for, or fail because of, notification logic, and a
durable queue lets us be offline without losing anything. Conversely, a display name is
needed *inside* the handler, and subscribing to a stream of user updates just to render a
title would mean duplicating the Users team's data.

**Consequences.** Two integration styles to operate and test. Our handler is only as fast as
the slowest lookup, which is why the HTTP clients have timeouts, retries and a circuit
breaker.

## ADR-4 – `IMessageClient` instead of using EasyNetQ directly

**Status:** accepted

**Context.** The assignment requires that the broker can be replaced. EasyNetQ's `IBus` is
also awkward to test.

**Decision.** A two-interface abstraction (`IMessageClient`, `IMessageHandler<T>`) in its own
package, with `EasyNetQMessageClient` as the only type that references EasyNetQ.

**Why.** Kafka would be a new adapter, not a refactor. Tests get a `FakeMessageClient`
instead of a running broker. And the week-40 Moq errors (*unsupported expression*, *cannot
instantiate proxy class*) simply cannot occur when the seam is an interface.

**Consequences.** The abstraction hides broker features (dead-letter configuration, manual
acknowledgement, priorities). If we ever need them, they get added as explicit options
rather than by leaking `IBus`.

## ADR-5 – Mentions in Discord markup, not plain `@name`

**Status:** accepted

**Decision.** `<@{userId}>` for users, `@here` and `@everyone` for channels.

**Why.** Ids survive a rename, parsing is unambiguous, and `davud@here.com` is not a
mention. Matching display names would mean asking the User service for every word in every
message.

**Consequences.** The client must produce the markup, and notification bodies must translate
it back to names before a human reads it — which is what `MentionMarkup.Humanize` is for.

## ADR-6 – Fail open on the User service, fail closed on the Channel service

**Status:** accepted

**Context.** Both lookups can fail, but the consequences differ.

**Decision.** If the User service cannot be reached, create the notification anyway with the
title *"Someone mentioned you"*. If the Channel service cannot be reached, do not create
channel-wide notifications at all.

**Why.** A missing display name is cosmetic, and dropping a mention is the actual failure. A
missing member list is different: there is nobody to notify, and inventing recipients would
notify the wrong people. Note the distinction between "not found" and "unavailable" —
`UserLookupResult` models them separately, because a user that does not exist should be
skipped while a user we *cannot reach* should not be.

**Consequences.** Users can see a slightly generic notification during an outage, and
`@everyone` is silently skipped (logged as a warning) during a Channel outage.

## ADR-7 – Preferences are evaluated in the domain, not in the handler

**Status:** accepted

**Decision.** `NotificationPreference.Decide(mention, channelId, now)` returns a
`DeliveryDecision(ShouldNotify, Reason)`.

**Why.** The decision *is* the business logic of this service. In the handler it would be an
untestable pile of `if`s that drifts away from the REST path; in the entity it is testable
without a host and used identically by every caller. Returning a reason makes suppression
visible in logs and readable in tests.

## ADR-8 – `TimeProvider` everywhere instead of `DateTime.UtcNow`

**Status:** accepted

**Decision.** The application layer injects `TimeProvider`; the domain accepts timestamps as
parameters.

**Why.** Quiet hours are time-dependent rules, and a test that passes only outside 22:00–07:00
is worse than no test.

## ADR-9 – Strings for enumerations in public contracts

**Status:** accepted

**Decision.** `NotificationType`/`NotificationStatus` are enums internally and strings in the
shared contracts, with the known values exposed as constants.

**Why.** Enum numbering is a versioning trap: inserting a value renumbers the rest, and a
consumer on an older build silently misreads it. A string is self-describing, and an unknown
value can be handled as "something new" instead of crashing.

## ADR-10 – A monorepo with shared packages

**Status:** accepted (prescribed by the assignment, and we agree)

**Decision.** `apps/*-microservice` for services, `packages/*` for the shared contracts and
the message client.

**Why.** One repository means the shared contract is compiled by every consumer in the same
build, so a breaking change cannot pass CI unnoticed. Independent deployability is preserved
because each service has its own Dockerfile and is published separately.

**Consequences.** Discipline is required: `Shared.Contracts` must stay free of logic, or the
monorepo becomes a distributed monolith. That is why it contains nothing but data contracts.
