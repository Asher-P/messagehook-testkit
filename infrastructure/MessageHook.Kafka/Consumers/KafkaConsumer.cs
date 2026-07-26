using KafkaFlow;
using KafkaFlow.Consumers;
using IConsumer = MessageHook.Core.Messaging.Consuming.IConsumer;

namespace MessageHook.Kafka.Consumers;

public class KafkaConsumer : IConsumer
{
    private readonly IKafkaBus _kafkaBus;

    public KafkaConsumer(IKafkaBus kafkaBus)
    {
        _kafkaBus = kafkaBus;
    }

    public async Task StartConsumeAsync(string consumerName)
    {
        if (!_kafkaBus.Consumers.All.Any())
        {
            await _kafkaBus.StartAsync();
            await _kafkaBus.StopAsync();
        }

        var relevantConsumer = _kafkaBus.Consumers.GetConsumer(consumerName);
        if (relevantConsumer != null)
            await relevantConsumer.StartAsync();
        else
        {
            throw new NullReferenceException($"The Consumer with name {consumerName} not exist");
        }

        while (!(_kafkaBus.Consumers.All.Where(x => x.ConsumerName == consumerName)
               .Any(y => y.Status == ConsumerStatus.Running)))
        {
            await Task.Delay(TimeSpan.FromSeconds(0.2));
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}