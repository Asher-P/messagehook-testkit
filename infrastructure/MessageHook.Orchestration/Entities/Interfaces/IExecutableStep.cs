using MessageHook.Core.Messaging.Models;
using MessageHook.Core.Messaging.Publishing.Entities;

namespace MessageHook.Orchestration.Entities.Interfaces;

public interface IExecutableStep
{
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, T message, ProducingExtraData extraData);
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, IEnumerable<T> messages, ProducingExtraData extraData);
    Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync();

}