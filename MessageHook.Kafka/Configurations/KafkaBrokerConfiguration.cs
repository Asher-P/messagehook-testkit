using System.Collections.Generic;

namespace MessageHook.Kafka.Configurations;

public class KafkaBrokerConfiguration
{
    public IEnumerable<string> BootstrapServers { get; set; } = new List<string>();
    public KafkaCredentialsConfiguration Credentials { get; set; } = new KafkaCredentialsConfiguration();
} 