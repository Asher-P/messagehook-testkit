using System.Text;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Core.Messaging.Publishing.Entities;
using KafkaFlow;
using KafkaFlow.Producers;
using Microsoft.Extensions.Logging;

namespace MessageHook.Kafka.Producers;

public class KafkaProducer : IProducer
{
    private readonly ILogger _logger;
    private const string CorrelationId = "correlation_id";

    private IProducerAccessor producers;

    public KafkaProducer(IKafkaBus kafkaBus, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        producers = kafkaBus.Producers;
    }

    public async Task ProduceAsync(string destination, string key, object message, ProducingExtraData extraData)
    {
        var producer = producers.GetProducer(destination);
        var headers = new MessageHeaders();

        foreach (var header in extraData.Headers)
        {
            headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
        }

        // headers.Add("x-datadog-trace-id", uniqAsBytes);
        var res = await producer.ProduceAsync(key, messageValue: message, headers: headers);
        _logger?.LogInformation($"Produced message to: {res.Topic}, Partition: {res.Partition}, Offset: {res.Offset}");
    }
}