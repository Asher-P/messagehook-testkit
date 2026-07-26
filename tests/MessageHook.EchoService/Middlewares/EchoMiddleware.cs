using System.Text;
using KafkaFlow;
using KafkaFlow.Producers;
using MessageHook.EchoService.Tracking;
using Microsoft.Extensions.Logging;

namespace MessageHook.EchoService.Middlewares;

public class EchoMiddleware : IMessageMiddleware
{
    private readonly IProducerAccessor _producers;
    private readonly PayloadChangeStamper _stamper;
    private readonly ILogger<EchoMiddleware> _logger;

    public EchoMiddleware(IProducerAccessor producers, PayloadChangeStamper stamper, ILogger<EchoMiddleware> logger)
    {
        _producers = producers;
        _stamper = stamper;
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

        // Echo the payload back annotated with whether this id's name changed since the last message for it.
        var value = context.Message.Value is byte[] bytes ? _stamper.Stamp(bytes, key) : context.Message.Value;

        await producer.ProduceAsync("B", key, value, headers);

        _logger.LogInformation("Echoed message from A to B. Key: {Key}", key);

        await next(context);
    }
}
