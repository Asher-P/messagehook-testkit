namespace MessageHook.Kafka.Connector;

public interface IConnector
{
    Task CreateNewMessageHookId(string MessageHookId);
}