using KafkaFlow;
using Microsoft.Extensions.Logging;

namespace MessageHook.Kafka.Middlewares;

public class ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger) : IMessageMiddleware
{

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            logger?.LogError(ex,$"Error processing message. Topic: {context.ConsumerContext.Topic}, Partition: {context.ConsumerContext.Partition}, offset: {context.ConsumerContext.Offset}");
        }
    }
}