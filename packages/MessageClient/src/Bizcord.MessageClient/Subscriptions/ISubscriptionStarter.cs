namespace Bizcord.MessageClient.Subscriptions;

/// <summary>
/// Starts one registered subscription. Implemented by the closed generic
/// <see cref="SubscriptionStarter{TMessage,THandler}"/>, which lets the hosted service
/// start subscriptions without any reflection over generic methods.
/// </summary>
public interface ISubscriptionStarter
{
    Task StartAsync(CancellationToken cancellationToken);
}
