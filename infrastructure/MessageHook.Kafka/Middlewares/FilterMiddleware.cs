using System.Text;
using MessageHook.Core.Extensions;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Receivers;
using KafkaFlow;
using Microsoft.Extensions.Logging;

namespace MessageHook.Kafka.Middlewares;

public class FilterMiddleware : IMessageMiddleware
{
    private static readonly string[] CorrelationIdHeaderOptions = new[] { "correlation_id", "CorrelationId" };
    
    private readonly IFilterService _filterService;
    private readonly ILogger<FilterMiddleware> _logger;

    public FilterMiddleware(
        IFilterService filterService, 
        ILogger<FilterMiddleware> logger)
    {
        _filterService = filterService;
        _logger = logger;
    }

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        // Try correlation ID first
        var correlationId = GetCorrelationId(context);
        if (correlationId != null)
        {
            var MessageHookIdentifier = correlationId.AddPrefix($"{context.ConsumerContext.Topic}_");
            var MessageHookId = _filterService.Filter(MessageHookIdentifier);
            
            if (MessageHookId != null)
            {
                context.Items["MessageHookId"] = MessageHookId;
                await next(context).ConfigureAwait(false);
                return;
            }
        }

        // Try message key second
        var messageKey = Encoding.UTF8.GetString(context.Message.Key as byte[]);
        if (messageKey != null)
        {
            var MessageHookIdentifier = messageKey.AddPrefix($"{context.ConsumerContext.Topic}_key_");
            var MessageHookId = _filterService.Filter(MessageHookIdentifier);
            
            if (MessageHookId != null)
            {
                context.Items["MessageHookId"] = MessageHookId;
                await next(context).ConfigureAwait(false);
                return;
            }
        }

        //_logger?.LogWarning($"No matching correlation ID or message key found: topic:{context.ConsumerContext.Topic}, partition:{context.ConsumerContext.Partition}, offset:{context.ConsumerContext.Offset}");
    }

    private string? GetCorrelationId(IMessageContext context)
    {
        foreach (var option in CorrelationIdHeaderOptions)
        {
            var correlationId = context.Headers.GetString(option);
            if (correlationId != null) 
                return correlationId;
        }
        return null;
    }
}