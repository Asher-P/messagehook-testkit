using MessageHook.Core.Extensions;
using MessageHook.Core.Messaging.Consuming;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Orchestration.Configurations;

namespace MessageHook.Orchestration.Entities;

public class CorrelationIdMessageHookStep : BaseMessageHookStep
{
    private readonly string _MessageHookId;

    public CorrelationIdMessageHookStep(
        IConsumer consumer,
        IProducer producer,
        IFilterService filterService,
        IMessagePool messagePool,
        MessageHookConfiguration configuration)
        : base(consumer, producer, filterService, messagePool, configuration)
    {
        _MessageHookId = Guid.NewGuid().ToString();
    }

    public override async Task InitializeAsync()
    {
        if (MessageHookType == Enums.MessageHookType.ProduceAndWait)
        {
            foreach (var consumeFrom in _configuration.ConsumeFrom)
            {
                var consumeMessageHookId = _MessageHookId.AddPrefix($"{consumeFrom}_");
                _filterService.AddFilter(consumeMessageHookId);
            }

            foreach (var consumeFrom in _configuration.ConsumeFrom)
            {
                await _consumer.StartConsumeAsync(consumeFrom);
            }
        }
        else if (MessageHookType == Enums.MessageHookType.ConsumeOnly)
        {
            foreach (var consumeFrom in _configuration.ConsumeFrom)
            {
                var expectedCorrelationId = _configuration.ConsumingOptions.ExpectedCorrelationId;
                var consumeMessageHookId = expectedCorrelationId.AddPrefix($"{consumeFrom}_");
                _filterService.AddFilter(consumeMessageHookId);
                await _consumer.StartConsumeAsync(consumeFrom);
            }
        }
    }

    protected override string GetMessageHookIdentifier(string topic)
    {
        if (MessageHookType == Enums.MessageHookType.ProduceAndWait)
        {
            return _MessageHookId.AddPrefix($"{topic}_");
        }
        else if (MessageHookType == Enums.MessageHookType.ConsumeOnly)
        {
            var expectedCorrelationId = _configuration.ConsumingOptions.ExpectedCorrelationId;
            return expectedCorrelationId.AddPrefix($"{topic}_");
        }
        
        throw new InvalidOperationException($"Unsupported MessageHook type: {MessageHookType}");
    }

    protected override string GetClearIdentifier()
    {
        return MessageHookType == Enums.MessageHookType.ProduceAndWait 
            ? _MessageHookId 
            : _configuration.ConsumingOptions.ExpectedCorrelationId;
    }

    protected override void AddProducingHeaders(ProducingExtraData producingExtraData)
    {
        // Add correlation ID header for traditional filtering
        producingExtraData.Headers.Add("MessageHookId", _MessageHookId);
        producingExtraData.Headers.Add("correlation_id", _MessageHookId);
    }
} 