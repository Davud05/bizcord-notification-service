using Bizcord.MessageClient;
using Bizcord.NotificationService.Application;
using Bizcord.NotificationService.Application.Mentions;
using Bizcord.NotificationService.Infrastructure;
using Bizcord.NotificationService.Presentation.Diagnostics;
using Bizcord.NotificationService.Presentation.Errors;
using Bizcord.Shared.Contracts.Events;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Presentation
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

// Model validation failures are returned as RFC 7807 problem details, so clients get the
// same error shape no matter which layer rejected the request.
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressMapClientErrors = false);

// ---------------------------------------------------------------------------
// Application + infrastructure (the layered architecture of this microservice)
// ---------------------------------------------------------------------------
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Messaging: react to messages posted anywhere in Bizcord
// ---------------------------------------------------------------------------
builder.Services.AddMessageClient(builder.Configuration, subscriptionId: "notification-service");
builder.Services.AddMessageHandler<MessagePostedEvent, MessagePostedHandler>();

// ---------------------------------------------------------------------------
// Health probes: "am I alive" and "can I actually do my job"
// ---------------------------------------------------------------------------
builder.Services
    .AddHealthChecks()
    .AddCheck<BrokerHealthCheck>("broker", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("EnableSwagger", true))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Bizcord Notification Service v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the real application in
/// the service and integration tests.
/// </summary>
public partial class Program;
