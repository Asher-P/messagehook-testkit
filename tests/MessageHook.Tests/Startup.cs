using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Receivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageHook.Tests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            // services.AddKafkaConsumer()
            //     .AddKafkaProducer()
            //     .UseKafka();
            services.AddSingleton<IFilterService, FilterService>();
            services.AddSingleton<IMessagePool, MessagePool>();

    }  
    
    public void Configure()
    {
    }
}