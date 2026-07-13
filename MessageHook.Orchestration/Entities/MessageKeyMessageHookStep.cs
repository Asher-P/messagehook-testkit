using MessageHook.Core.Extensions;
using MessageHook.Core.Messaging.Consuming;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Orchestration.Configurations;

namespace MessageHook.Orchestration.Entities;

public class MessageKeyMessageHookStep : BaseMessageHookStep
{
    private readonly string _expectedMessageKey;

    public MessageKeyMessageHookStep(
        IConsumer consumer,
        IProducer producer,
        IFilterService filterService,
        IMessagePool messagePool,
        MessageHookConfiguration configuration)
        : base(consumer, producer, filterService, messagePool, configuration)
    {
        _expectedMessageKey = configuration.ConsumingOptions?.ExpectedMessageKey 
            ?? throw new ArgumentException("ExpectedMessageKey is required for MessageKeyMessageHookStep");
    }

    public override async Task InitializeAsync()
    {
        if (MessageHookType == Enums.MessageHookType.ProduceAndWait || MessageHookType == Enums.MessageHookType.ConsumeOnly)
        {
            foreach (var consumeFrom in _configuration.ConsumeFrom)
            {
                var consumeMessageHookId = _expectedMessageKey.AddPrefix($"{consumeFrom}_key_");
                _filterService.AddFilter(consumeMessageHookId);
            }

            foreach (var consumeFrom in _configuration.ConsumeFrom)
            {
                await _consumer.StartConsumeAsync(consumeFrom);
            }
        }
    }

    protected override string GetMessageHookIdentifier(string topic)
    {
        return _expectedMessageKey.AddPrefix($"{topic}_key_");
    }

    protected override string GetClearIdentifier()
    {
        return _expectedMessageKey;
    }

    protected override void AddProducingHeaders(ProducingExtraData producingExtraData)
    {
        producingExtraData.Headers.Add("MessageHookId", _configuration.ConsumingOptions.ExpectedMessageKey);
        producingExtraData.Headers.Add("correlation_id", Guid.NewGuid().ToString());
    }
} 