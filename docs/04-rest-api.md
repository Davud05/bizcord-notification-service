# 04 – REST API

Base URL in the Compose stack: `http://localhost:8080`
Interactive documentation: `/swagger` · OpenAPI document: `/openapi/v1.json`

## Endpoints

### Notifications

| Method | Route | Purpose | Success | Errors |
| --- | --- | --- | --- | --- |
| `POST` | `/api/notifications` | Create a notification | `201 Created` + `Location` | `400` |
| `GET` | `/api/notifications/{id}` | Read one | `200 OK` | `404` |
| `GET` | `/api/notifications` | Read many (filter + page) | `200 OK` | `400` |
| `GET` | `/api/notifications/unread-count?recipientId=` | Badge count | `200 OK` | `400` |
| `PUT` | `/api/notifications/{id}` | Replace the text of an unread notification | `200 OK` | `400`, `404` |
| `POST` | `/api/notifications/{id}/read` | Mark as read (idempotent) | `200 OK` | `404` |
| `DELETE` | `/api/notifications/{id}` | Delete | `204 No Content` | `404` |

Query parameters on the list endpoint: `recipientId`, `channelId`, `status`
(`Unread`/`Read`/`Archived`), `skip` (≥ 0), `take` (1–200, default 50).

### Preferences

| Method | Route | Purpose | Success | Errors |
| --- | --- | --- | --- | --- |
| `GET` | `/api/users/{userId}/notification-preferences` | Read preferences | `200 OK` | — |
| `PUT` | `/api/users/{userId}/notification-preferences` | Replace preferences | `200 OK` | `400` |

`GET` returns the documented defaults for a user who never saved anything, rather than a
`404`: the absence of preferences is not an error, it is a state.

### Health

| Route | Meaning |
| --- | --- |
| `/health/live` | The process is alive. Used by the Docker `HEALTHCHECK`. Never depends on the broker. |
| `/health/ready` | The process is ready, including the broker connection. |
| `/health` | Everything. |

## Examples

Create:

```http
POST /api/notifications
Content-Type: application/json

{
  "recipientId": "3f4c8e2a-1b2d-4c5e-8a9b-0c1d2e3f4a5b",
  "type": "System",
  "title": "Welcome to Bizcord",
  "body": "Say hi in #general",
  "channelId": null,
  "messageId": null,
  "authorId": null
}
```

```http
HTTP/1.1 201 Created
Location: /api/notifications/9f1b7e14-...

{ "id": "9f1b7e14-...", "status": "Unread", "createdAt": "2026-10-20T16:56:13.4Z", ... }
```

Validation failure (RFC 7807):

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
  "title": "Invalid request",
  "status": 400,
  "detail": "Notification title must not be empty."
}
```

Mark as read twice returns `200 OK` both times with the same `readAt`, and
`NotificationReadEvent` is only published once.

## Status code policy

| Situation | Code |
| --- | --- |
| Created | `201` with a `Location` header |
| Read or updated | `200` with the resource |
| Deleted | `204`, no body |
| A broken request (bad JSON, missing field, `take=9000`, a domain rule violated) | `400` with `application/problem+json` |
| The resource does not exist | `404` with `application/problem+json` |
| Anything unexpected | `500` with `application/problem+json` and no internal details |

Mapping happens in one place, `Errors/ApplicationExceptionHandler.cs`, registered as an
`IExceptionHandler`. Controllers never translate exceptions themselves:
`DomainException` → `400`, `NotFoundException` → `404`, everything else → `500`.

## Separation of concerns

```
Presentation   Controllers + Requests (DTO → command) + ProblemDetails + health
    ↓ calls
Application    INotificationAppService / IPreferenceAppService (the use cases)
    ↓ calls
Domain         Notification, NotificationPreference (the rules)
```

* **Controllers** are thin: bind, delegate, return a status code. The longest method body is
  seven lines.
* **Request records** (`CreateNotificationRequest`, `UpdateNotificationRequest`,
  `UpdatePreferencesRequest`) belong to the HTTP layer and have a `ToCommand()` mapper, so
  the application layer never sees an HTTP type.
* **Validation** happens in two layers: `[ApiController]` model binding rejects malformed
  JSON and wrong types with a `400` before our code runs, and the domain rejects everything
  semantic (empty title, a `Mention` without a source, `Guid.Empty` as a recipient). The
  second layer is the one that matters, because it also protects the message consumer, which
  has no model binding at all.
* **Everything is injected.** `Program.cs` composes the service through three extension
  methods: `AddNotificationApplication()`, `AddNotificationInfrastructure(configuration)` and
  `AddMessageClient(configuration, "notification-service")`.
