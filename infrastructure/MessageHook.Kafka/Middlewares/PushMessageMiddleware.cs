using System.Text;
using MessageHook.Core.Extensions;
using MessageHook.Core.Messaging.Receivers;
using KafkaFlow;

namespace MessageHook.Kafka.Middlewares;

public class PushMessageMiddleware : IMessageMiddleware
{
    private readonly IMessagePool _messagePool;

    public PushMessageMiddleware(IMessagePool messagePool)
    {
        _messagePool = messagePool;
    }
    public Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        _messagePool.AddMessage(context.Items["MessageHookId"].ToString(),new KeyValuePair<object, object>(context.Message.Key,context.Message.Value) );
        return next(context);
    }
}