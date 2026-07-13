using MessageHook.Core.Messaging.FilterService;

namespace MessageHook.Orchestration.Configurations;

public class ConsumingOptionsConfiguration
{
    /// <summary>
    /// TimeOut Default Value Is 30 Seconds
    /// </summary>
    public TimeSpan TimeOut { get; set; } = TimeSpan.FromSeconds(30);
    public string ExpectedCorrelationId { get; set; }
    public string ExpectedMessageKey { get; set; }
    public int MsgReceivedCount { get; set; }
}