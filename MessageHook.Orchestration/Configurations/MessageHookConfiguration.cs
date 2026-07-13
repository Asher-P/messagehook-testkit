namespace MessageHook.Orchestration.Configurations;

public class MessageHookConfiguration
{
    public IEnumerable<string> ConsumeFrom { get; set; }
    public string ProduceTo { get; set; }
    // public Predicate Filter;

    public ConsumingOptionsConfiguration ConsumingOptions { get; set; }
}

