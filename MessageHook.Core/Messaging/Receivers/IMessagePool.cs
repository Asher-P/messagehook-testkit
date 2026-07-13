using MessageHook.Core.Messaging.Models;

namespace MessageHook.Core.Messaging.Receivers;

public interface IMessagePool
{
    List<ResponseContainer> GetMessages(IEnumerable<string> MessageHookIds);
    void ClearMessageHookMessages(string MessageHookId);
    void AddMessage(string MessageHookId, KeyValuePair<object,object> keyValuePair);
}