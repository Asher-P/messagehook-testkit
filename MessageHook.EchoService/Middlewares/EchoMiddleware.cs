using System.Text;
using KafkaFlow;
using KafkaFlow.Producers;
using Microsoft.Extensions.Logging;

namespace MessageHook.EchoService.Middlewares;

public class EchoMiddleware : IMessageMiddleware
{
    private readonly IProducerAccessor _producers;
    private readonly ILogger<EchoMiddleware> _logger;

    public EchoMiddleware(IProducerAccessor producers, ILogger<EchoMiddleware> logger)
    {
        _producers = producers;
        _logger = logger;
    }

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        var producer = _producers.GetProducer("echo-producer");

        var headers = new MessageHeaders();
        foreach (var header in context.Headers)
            headers.Add(header.Key, header.Value);

        var key = context.Message.Key is byte[] keyBytes
            ? Encoding.UTF8.GetString(keyBytes)
            : context.Message.Key?.ToString();

        await producer.ProduceAsync("B", key, context.Message.Value, headers);

        _logger.LogInformation("Echoed message from A to B. Key: {Key}", key);

        await next(context);
    }
}
