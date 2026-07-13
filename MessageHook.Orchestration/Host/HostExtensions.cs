using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Orchestration.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHook.Orchestration.Host;
    
public static class HostExtensions
{
    public static IServiceCollection AddMessageHookService(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMessageHookFactory, MessageHookFactory>();
        serviceCollection.AddSingleton<IFilterService, FilterService>();
        serviceCollection.AddSingleton<IMessagePool, MessagePool>();
        return serviceCollection;
    }
}