using System.Net.Sockets;

namespace Bizcord.NotificationService.TestSupport;

/// <summary>
/// Integration tests need a real RabbitMQ. This probes the broker so the suite can skip
/// those tests with a clear reason on a machine where <c>docker compose up</c> is not
/// running, instead of failing with a connection error.
/// </summary>
public static class BrokerAvailability
{
    public static string Host =>
        Environment.GetEnvironmentVariable("RABBITMQ_HOST")
        ?? Environment.GetEnvironmentVariable("RabbitMq__Host")
        ?? "localhost";

    public static int Port =>
        int.TryParse(Environment.GetEnvironmentVariable("RabbitMq__Port"), out var port) ? port : 5672;

    public static string SkipReason =>
        $"RabbitMQ is not reachable on {Host}:{Port}. Start it with 'docker compose up -d rabbitmq'.";

    public static bool IsAvailable()
    {
        try
        {
            using var client = new TcpClient();

            return client.ConnectAsync(Host, Port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
