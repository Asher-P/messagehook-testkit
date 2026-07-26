namespace MessageHook.Kafka.Configurations;

public class KafkaCredentialsConfiguration
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Mechanism { get; set; }
    public bool TlsEnabled { get; set; }
    public string? SecurityProtocol { get; set; }
    
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    /// <summary>
    /// Creates a KafkaCredentialsConfiguration from environment variables
    /// </summary>
    /// <param name="usernameVar">Environment variable name for username</param>
    /// <param name="passwordVar">Environment variable name for password</param>
    /// <param name="mechanismVar">Environment variable name for SASL mechanism</param>
    /// <param name="tlsEnabledVar">Environment variable name for TLS enabled flag</param>
    /// <param name="securityProtocolVar">Environment variable name for security protocol</param>
    /// <returns>A configured KafkaCredentialsConfiguration object</returns>
    public static KafkaCredentialsConfiguration FromEnvironment(
        string usernameVar, 
        string passwordVar, 
        string mechanismVar, 
        string tlsEnabledVar, 
        string securityProtocolVar)
    {
        return new KafkaCredentialsConfiguration
        {
            Username = Environment.GetEnvironmentVariable(usernameVar),
            Password = Environment.GetEnvironmentVariable(passwordVar),
            Mechanism = Environment.GetEnvironmentVariable(mechanismVar),
            TlsEnabled = bool.TryParse(Environment.GetEnvironmentVariable(tlsEnabledVar), out var tls) && tls,
            SecurityProtocol = Environment.GetEnvironmentVariable(securityProtocolVar)
        };
    }
} 