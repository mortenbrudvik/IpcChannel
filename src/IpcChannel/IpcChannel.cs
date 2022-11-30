using System.IO.Pipes;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using Ardalis.GuardClauses;
using static LanguageExt.Prelude;

namespace IpcChannel;

public class IpcChannel<T> where T : class 
{
    private readonly Lazy<IObservable<string>> _messages;
    private readonly int _numberOfServerInstances;
    private readonly string _id;

    private IpcChannel(string id, int numberOfServerInstances)
    {
        _numberOfServerInstances = Guard.Against.OutOfRange(numberOfServerInstances, nameof(numberOfServerInstances), 1, 254);
        _id = Guard.Against.NullOrWhiteSpace(id, nameof(id));
        
        _messages = new Lazy<IObservable<string>>(CreateNamedPipeObservable);
    }

    /// <summary>
    /// Reactive observable to subscribe for channel messages (plain json)
    /// </summary>
    public IObservable<string> MessagesAsJson => _messages.Value;

    /// <summary>
    /// Reactive observable to subscribe for channel messages
    /// </summary>
    public IObservable<T> Messages =>
            MessagesAsJson.SelectMany(msg => 
                Optional(JsonSerializer.Deserialize<T>(msg))
                    .Match(
                        Observable.Return, 
                        Observable.Empty<T>));

    /// <summary>
    /// Is the channel open to receive messages
    /// </summary>
    /// <param name="channelId">Channel id</param>
    /// <returns>True if channel is open, else false</returns>
    public static bool IsRunning(string channelId) =>
        Directory.GetFiles(@"\\.\\pipe\\").Any(f => f.Contains(channelId));

    /// <summary>
    /// Creates an IPC channel to to listen for messages (named pipe server)
    /// </summary>
    /// <param name="channelId">Unique channel id that is used for communication</param>
    /// <param name="maxNumberOfServerInstances">Maximum number of server instances. Specify a fixed value between 1 and 254
    /// exceeds the max number of server instances specified for the channel</param>
    /// <returns>An instance of IpChannel. Use the Messages property to subscribe to the channel</returns>
    public static IpcChannel<T> Create(string channelId, int maxNumberOfServerInstances = 1) =>
        new(channelId, maxNumberOfServerInstances);

    /// <summary>
    /// Send a message to an open IPC Channel (named pipe server)
    /// </summary>
    /// <param name="channelId">Id of IPC channel listener</param>
    /// <param name="message">Channel message</param>
    /// <param name="timeoutInMs">Set timeout to abort send operation</param>
    /// <param name="token">Cancellation token to abort send operation</param>
    public static async Task Send(string channelId, T message, int timeoutInMs = Timeout.Infinite, CancellationToken? token = null)
    {
        var pipe = new NamedPipeClientStream(".", channelId, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeoutInMs, token ?? CancellationToken.None);

        var messageJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await pipe.WriteAsync(messageJson);
        await pipe.FlushAsync();
    }

    private IObservable<string> CreateNamedPipeObservable() =>
        Observable.Create<string>(async (o, ct) =>
        {
            var pipe = new NamedPipeServerStream(_id, PipeDirection.In, _numberOfServerInstances,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, 0, 0);
            ct.Register(() => pipe.Close());

            while (!ct.IsCancellationRequested)
            {
                await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, ct);
            
                var message = await pipe.ReadMessage(ct);
                if (!string.IsNullOrWhiteSpace(message)) o.OnNext(message);

                pipe.Disconnect();
            }

            o.OnCompleted();
        });
}
