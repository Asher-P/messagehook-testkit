namespace MessageHook.Core.Messaging.Publishing.Entities;

public class ProducingExtraData
{
    public Dictionary<string, string> Headers { get; set; } = new();
}