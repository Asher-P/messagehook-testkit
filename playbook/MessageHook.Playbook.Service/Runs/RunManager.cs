using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using MessageHook.Playbook;
using MessageHook.Playbook.Execution;
using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Results;
using MessageHook.Playbook.Service.Playbooks;
using MessageHook.Playbook.Service.Storage;

namespace MessageHook.Playbook.Service.Runs;

/// <summary>
/// Runs test cases and streams results to the response as newline-delimited JSON events, so the UI shows each
/// step live. Bridges the library's <see cref="PlaybookRunOptions.Progress"/> hook to the response stream.
/// </summary>
public sealed class RunManager
{
    // Compact single-line events, camelCase to match the rest of the HTTP API and the UI's TypeScript types.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    private readonly PlaybookRunner _runner;
    private readonly SuiteStore _store;

    public RunManager(PlaybookRunner runner, SuiteStore store)
    {
        _runner = runner;
        _store = store;
    }

    /// <summary>Broker-free validation of one case. Returns the error list (empty = valid).</summary>
    public IReadOnlyList<string> Validate(Suite suite, TestCase testCase)
    {
        var definition = PlaybookAssembler.Assemble(suite, testCase);
        var provider = new FileSystemPayloadProvider(_store.SuiteDirectory(suite.Id));
        try
        {
            _runner.Validate(definition, provider);
            return Array.Empty<string>();
        }
        catch (PlaybookException e)
        {
            return e.Errors;
        }
    }

    /// <summary>Runs the given cases in order, streaming NDJSON events (step / result / error / done) to <paramref name="output"/>.</summary>
    public async Task StreamAsync(Suite suite, IReadOnlyList<TestCase> cases, Stream output, CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<string>();

        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var testCase in cases)
                {
                    ct.ThrowIfCancellationRequested();

                    var definition = PlaybookAssembler.Assemble(suite, testCase);
                    var provider = new FileSystemPayloadProvider(_store.SuiteDirectory(suite.Id));
                    // Synchronous progress: System.Progress<T> marshals Report asynchronously, so the last step's
                    // live event can land after the run's `result`/`done` are written and the channel is closed —
                    // dropping that step's live tick. Writing on the reporting thread keeps step events in order.
                    var progress = new SyncProgress<StepResult>(step =>
                        channel.Writer.TryWrite(Event(new { type = "step", caseId = testCase.Id, caseName = testCase.Name, step })));

                    var options = new PlaybookRunOptions
                    {
                        PayloadProvider = provider,
                        Progress = progress,
                        CancellationToken = ct
                    };

                    try
                    {
                        var result = await _runner.RunAsync(definition, options);
                        channel.Writer.TryWrite(Event(new { type = "result", caseId = testCase.Id, caseName = testCase.Name, result }));
                    }
                    catch (PlaybookException e)
                    {
                        channel.Writer.TryWrite(Event(new { type = "error", caseId = testCase.Id, error = e.Message, errors = e.Errors }));
                    }
                    catch (Exception e)
                    {
                        channel.Writer.TryWrite(Event(new { type = "error", caseId = testCase.Id, error = e.Message }));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                channel.Writer.TryWrite(Event(new { type = "error", error = "Run cancelled." }));
            }
            finally
            {
                channel.Writer.TryWrite(Event(new { type = "done" }));
                channel.Writer.Complete();
            }
        }, CancellationToken.None);

        await foreach (var line in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                await output.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), ct);
                await output.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break; // client disconnected; let the producer wind down
            }
        }

        await producer;
    }

    private static string Event(object payload) => JsonSerializer.Serialize(payload, Json);

    /// <summary>
    /// An <see cref="IProgress{T}"/> that invokes its handler on the calling thread, unlike
    /// <see cref="Progress{T}"/> which posts asynchronously. Keeps step events ordered ahead of the run's final
    /// result on the same stream.
    /// </summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
