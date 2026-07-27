using MessageHook.Core.Messaging.Publishing.Entities;

namespace MessageHook.Core.Messaging.Publishing;

public interface IProducer
{
    ///     <summary>
    ///  Produce message to broker
    /// </summary>
    /// <param name="destination">The topic in kafka and exchange in rabbitmq</param>
    /// <param name="key">The key message in kafka and routingKey in rabbitmq</param>
    /// <param name="message"> The message to send</param>
    /// <param name="correlationId">The correlationId/MessageHookId to pass in the meesage</param>
    /// <returns></returns>
    Task ProduceAsync(string destination, string key, object message,ProducingExtraData extraData);
}