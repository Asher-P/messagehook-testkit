using MessageHook.Core.Messaging.FilterService;
using KafkaFlow;

namespace MessageHook.Kafka.Connector;

public class ProducerConsumerConnector : IConnector
{
    private readonly IFilterService _filterService;

    public ProducerConsumerConnector(IFilterService filterService)
    {
        _filterService = filterService;
    }
    private event Action<string> NewMessageHookCreated;  
    public Task CreateNewMessageHookId(string MessageHookId)
    {
        _filterService.AddFilter(MessageHookId);
        NewMessageHookCreated?.Invoke(MessageHookId);
        return Task.CompletedTask;
    }
}