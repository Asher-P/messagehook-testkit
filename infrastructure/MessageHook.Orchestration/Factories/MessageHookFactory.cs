using MessageHook.Core.Messaging.Consuming;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Entities;
using MessageHook.Orchestration.Entities.Interfaces;

namespace MessageHook.Orchestration.Factories;

public class MessageHookFactory(
    IConsumer consumer,
    IProducer producer,
    IFilterService filterService,
    IMessagePool messagePool) : IMessageHookFactory
{
    public async Task<IMessageHookStep> CreateMessageHookStepAsync(MessageHookConfiguration configuration)
    {
        IMessageHookStep MessageHookStep = DetermineMessageHookStepType(configuration);
        await MessageHookStep.InitializeAsync();
        return MessageHookStep;
    }

    private IMessageHookStep DetermineMessageHookStepType(MessageHookConfiguration configuration)
    {
        // Determine filter mode based on configuration
        var hasMessageKey = !string.IsNullOrEmpty(configuration.ConsumingOptions?.ExpectedMessageKey);
        var hasCorrelationId = !string.IsNullOrEmpty(configuration.ConsumingOptions?.ExpectedCorrelationId);

        if (hasMessageKey && hasCorrelationId)
        {
            throw new ArgumentException("Cannot specify both ExpectedMessageKey and ExpectedCorrelationId. Choose one filtering mode.");
        }

        if (hasMessageKey)
        {
            return new MessageKeyMessageHookStep(consumer, producer, filterService, messagePool, configuration);
        }
        
        // Default to correlation ID mode (backward compatibility)
        return new CorrelationIdMessageHookStep(consumer, producer, filterService, messagePool, configuration);
    }
}