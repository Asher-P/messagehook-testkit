using System.Collections.Concurrent;
using MessageHook.Core.Messaging.Models;

namespace MessageHook.Core.Messaging.Receivers;

public class MessagePool : IMessagePool
{
    private ConcurrentDictionary<string, List<KeyValuePair<object,object>>> cachedMessages = new();

    public List<ResponseContainer> GetMessages(IEnumerable<string> MessageHookIds)
    {
        var allMessages = new List<ResponseContainer>();
        foreach (var MessageHookId in MessageHookIds)
        {
            cachedMessages.TryGetValue(MessageHookId, out var messages);
            if (messages != null && messages.Count > 0)
            {
                allMessages.Add(new ResponseContainer()
                {
                    MessageHookId = MessageHookId,
                    Messages = new List<MessageContainer>(messages.ToMessageContainerList())
                });
            }
        }

        return allMessages;
    }

    public void ClearMessageHookMessages(string MessageHookId)
    {
        if (cachedMessages.TryGetValue(MessageHookId, out _))
        {
            cachedMessages[MessageHookId] = new List<KeyValuePair<object,object>>();
        }
    }

    public void AddMessage(string MessageHookId, KeyValuePair<object,object> keyValuePair)
    {
        var value = cachedMessages.GetOrAdd(MessageHookId, (s => new List<KeyValuePair<object,object>>()));
        value.Add(keyValuePair);
    }
}