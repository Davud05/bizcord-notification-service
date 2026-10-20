# 09 – Assignment checklist

Where each required piece of work lives. Course: System Integration (E2026), project
**Bizcord**, bounded context **Notifications** (Track B: consumer).

## Task 1 – Analysis & design

| Requirement | Where |
| --- | --- |
| Identify the domains of the system | [01 – Analysis & design §2](01-analysis-and-design.md#2-domains-in-bizcord) |
| Responsibility of our microservice | [§3 Our boundary](01-analysis-and-design.md#3-our-boundary) |
| Service boundaries (in and out of scope) | [§3](01-analysis-and-design.md#3-our-boundary) |
| Data ownership | [§4](01-analysis-and-design.md#4-data-ownership) |
| Interactions with other services | [§5](01-analysis-and-design.md#5-interactions), with sequence diagrams |
| Documented in a `docs` folder | this folder |

## Task 2 – Initial setup & messaging

| Requirement | Where |
| --- | --- |
| Monorepo layout `apps/[domain]-microservice` with `src`, `tests`, `Dockerfile`, `README.md` | `apps/notification-microservice/` |
| Shared packages | `packages/Shared.Contracts`, `packages/MessageClient` |
| Short-lived branches merged into `main` (`chore/…`, `feat/…`, `fix/…`) | the git history of this repository |
| Message client abstraction over EasyNetQ with publish and subscribe | `packages/MessageClient/src/Bizcord.MessageClient/` |
| Dependency-injection extension | `MessageClientServiceCollectionExtensions.AddMessageClient(...)` |
| RabbitMQ hidden behind the abstraction | only `EasyNetQMessageClient` references EasyNetQ — see [05 – Messaging](05-messaging.md) |

## Task 3 – Inter-service communication

| Requirement | Where |
| --- | --- |
| Domain model: entities, value objects, domain rules | `…Domain/`, documented in [02 – Domain model](02-domain-model.md) |
| Shared model exposing only integration-relevant data | `packages/Shared.Contracts`, documented in [03 – Shared model](03-shared-model.md) |
| What is deliberately hidden | [03 §What we hide](03-shared-model.md#what-we-hide-and-why) |
| REST API with POST, GET (one), GET (many), PUT, DELETE | `…Presentation/Controllers/NotificationsController.cs` |
| Business logic separated from presentation logic | Presentation → Application → Domain, see [04 §Separation of concerns](04-rest-api.md#separation-of-concerns) |
| Dependency injection | `Program.cs` + three `Add…` extension methods |
| Validation and correct status codes | `[ApiController]` + domain rules + `ApplicationExceptionHandler`, see [04 – REST API](04-rest-api.md) |

## Task 4 – Deployment

| Requirement | Where |
| --- | --- |
| Multi-stage Dockerfile (base, build, publish, final) | `apps/notification-microservice/Dockerfile` |
| `.csproj` copied first, monorepo structure preserved | the `build` stage |
| `dotnet publish … /p:UseAppHost=false` | the `publish` stage |
| `ENTRYPOINT` in the final stage | `ENTRYPOINT ["dotnet", "Bizcord.NotificationService.Presentation.dll"]` |
| Compose file with `rabbitmq:3-management` on 5672 and 15672 | `docker-compose.yml` |
| Health check on RabbitMQ | `rabbitmq-diagnostics -q ping` |
| `depends_on` with `condition: service_healthy` | the `notification-service` service |
| `RABBITMQ_HOST` passed to the service | `environment:` |
| It actually runs | [06 §Verified behaviour](06-deployment.md#verified-behaviour) |

## Task 5 – First feature and testing (Track B)

| Requirement | Where |
| --- | --- |
| Consume `MessagePostedEvent` via `IMessageHandler<T>` | `…Application/Mentions/MessagePostedHandler.cs` |
| Do something domain specific | Mention detection → preference evaluation → notification |
| Publish a result event that keeps `MessageId` for traceability | `MentionDetectedEvent` |
| Unit test of the handler with a fake message client | `UnitTests/Application/MessagePostedHandlerTests.cs` + `TestSupport/FakeMessageClient.cs` (`SinglePublished<T>()`) |
| Consumer contract test `Handler_CanConsume_MinimumValidContract` | `UnitTests/Application/MessagePostedHandlerTests.cs` |
| Integration test over a real broker with `MessageCapture<T>` and `WaitForMessageAsync` | `IntegrationTests/MessagingIntegrationTests.cs` |
| xUnit + Moq (week 40, video 1) | throughout `UnitTests`; see [07 §The test double the videos describe](07-testing.md#the-test-double-the-videos-describe) |
| xUnit + WireMock (week 40, video 2) | `ServiceTests/` (`DirectoryClientTests`, `ServiceTestFactory`) and the `teammate-services` container |

## Test results

| Project | Tests | Result |
| --- | --- | --- |
| `Bizcord.NotificationService.UnitTests` | 95 | pass |
| `Bizcord.NotificationService.ServiceTests` | 28 | pass |
| `Bizcord.NotificationService.IntegrationTests` | 4 | pass against RabbitMQ in Docker (skipped without a broker) |

## Sources used

* RabbitMQ tutorials — <https://www.rabbitmq.com/tutorials>
* EasyNetQ Quick Start — <https://github.com/EasyNetQ/EasyNetQ/wiki/Quick-Start>
* Multi-container tutorial — <https://learn.microsoft.com/en-us/visualstudio/containers/tutorial-multicontainer>
* Sam Newman, *Building Microservices* — ch. 5 (communication styles), ch. 8 (deployment),
  ch. 9 (testing), with ch. 4 and 12 for integration styles and resiliency
