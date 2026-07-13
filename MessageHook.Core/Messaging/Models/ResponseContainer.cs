namespace MessageHook.Core.Messaging.Models;

public class ResponseContainer
{
    public string MessageHookId { get; set; }
    public List<MessageContainer> Messages {
        get;
        set;
    }
}

public class MessageContainer
{
    public object Key { get; set; }
    public object Value { get; set; }
}

public static class KeyValuePairExtensions
{
    public static List<MessageContainer> ToMessageContainerList(this List<KeyValuePair<object, object>> keyValuePairs)
    {
        List<MessageContainer> messageContainers = keyValuePairs.Select(x=>new MessageContainer{Key = x.Key, Value = x.Value}).ToList();
        return messageContainers;
    }
}