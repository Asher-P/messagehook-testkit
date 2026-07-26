using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Entities.Interfaces;

namespace MessageHook.Orchestration.Factories;

public interface IMessageHookFactory
{
    Task<IMessageHookStep> CreateMessageHookStepAsync(MessageHookConfiguration configuration);
}