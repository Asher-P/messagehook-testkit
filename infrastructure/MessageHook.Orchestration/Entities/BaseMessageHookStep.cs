using System.Diagnostics;
using MessageHook.Core.Messaging.Consuming;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Models;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Core.Extensions;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Entities.Enums;
using MessageHook.Orchestration.Entities.Interfaces;

namespace MessageHook.Orchestration.Entities;

public abstract class BaseMessageHookStep : IMessageHookStep
{
    protected readonly IConsumer _consumer;
    protected readonly IMessagePool _messagePool;
    protected readonly IProducer _producer;
    protected readonly MessageHookConfiguration _configuration;
    protected readonly IFilterService _filterService;

    public MessageHookType MessageHookType
    {
        get
        {
            if (_configuration.ConsumeFrom.IsNullOrEmpty())
                return MessageHookType.ProduceAndForget;
            else if (!_configuration.ProduceTo.IsNullOrEmpty())
                return MessageHookType.ProduceAndWait;
            else return MessageHookType.ConsumeOnly;
        }
    }

    protected BaseMessageHookStep(
        IConsumer consumer,
        IProducer producer,
        IFilterService filterService,
        IMessagePool messagePool,
        MessageHookConfiguration configuration)
    {
        _producer = producer;
        _configuration = configuration;
        _consumer = consumer;
        _messagePool = messagePool;
        _filterService = filterService;
    }

    public abstract Task InitializeAsync();
    
    protected abstract string GetMessageHookIdentifier(string topic);
    
    protected abstract string GetClearIdentifier();
    
    protected abstract void AddProducingHeaders(ProducingExtraData producingExtraData);

    public async Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, T message)
    {
        return await ExecuteAsync(key, (IEnumerable<T>)new[] { message }, new ProducingExtraData());
    }

    public async Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync()
    {
        return await ExecuteConsumeAsync();
    }

    public async Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key, T message,
        ProducingExtraData producingExtraData)
    {
        return await ExecuteAsync(key, (IEnumerable<T>)new[] { message }, producingExtraData);
    }

    public async Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteAsync<T>(string key,
        IEnumerable<T> messages,
        ProducingExtraData producingExtraData)
    {
        // Add any mode-specific headers
        AddProducingHeaders(producingExtraData);

        foreach (var message in messages)
        {
            await _producer.ProduceAsync(_configuration.ProduceTo, key, message, producingExtraData);
        }

        var tcs = new TaskCompletionSource<IEnumerable<ResponseContainer>>();

        switch (MessageHookType)
        {
            case MessageHookType.ProduceAndForget:
                tcs.SetResult(new List<ResponseContainer>());
                break;
            case MessageHookType.ProduceAndWait:
                GetTaskResultAsync(tcs);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return tcs;
    }

    private async Task<TaskCompletionSource<IEnumerable<ResponseContainer>>> ExecuteConsumeAsync()
    {
        var tcs = new TaskCompletionSource<IEnumerable<ResponseContainer>>();
        GetTaskResultAsync(tcs);
        return tcs;
    }

    private async Task GetTaskResultAsync(TaskCompletionSource<IEnumerable<ResponseContainer>> tcs)
    {
        var sw = new Stopwatch();
        sw.Start();

        IEnumerable<string> consumeMessageHookIds = _configuration.ConsumeFrom
            .Select(GetMessageHookIdentifier);

        while (sw.Elapsed <= _configuration.ConsumingOptions.TimeOut)
        {
            var responseContainers = _messagePool.GetMessages(consumeMessageHookIds);
            if (_configuration.ConsumingOptions.MsgReceivedCount > 0 && !responseContainers.IsNullOrEmpty() &&
                responseContainers.Sum(x => x.Messages.Count) >= _configuration.ConsumingOptions.MsgReceivedCount)
            {
                tcs.SetResult(responseContainers);

                _messagePool.ClearMessageHookMessages(GetClearIdentifier());
                return;
            }

            await Task.Delay(250);
        }

        sw.Stop();
        tcs.SetException(new TimeoutException("Did not receive enough messages within the timeout scope"));
    }
} 