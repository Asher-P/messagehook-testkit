namespace MessageHook.Core.Messaging.Consuming;

public interface IConsumer
{
    public Task StartConsumeAsync(string consumerName);
}