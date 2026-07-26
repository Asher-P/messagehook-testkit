using MessageHook.Core.Messaging.Models;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Orchestration.Entities.Enums;

namespace MessageHook.Orchestration.Entities.Interfaces;

public interface IMessageHookStep : IExecutableStep
{
    MessageHookType MessageHookType { get; }
    
    Task InitializeAsync();
    
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, T message);
    
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, T message, ProducingExtraData producingExtraData);
    
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, IEnumerable<T> messages, ProducingExtraData producingExtraData);
    
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync();
} 