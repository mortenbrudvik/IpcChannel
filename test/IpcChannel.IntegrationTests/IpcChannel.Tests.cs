using FluentAssertions;

namespace IpcChannel.IntegrationTests;

public class IpcChannelTests : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly string _channelId;

    public IpcChannelTests()
    {
        _channelId = $"12618167-97c2-4479-8392-94c6333f89fe-{Environment.UserName}-{1920}";
        _cts = new CancellationTokenSource();
    }
    
    [Fact]
    public async Task ShouldReceiveMessage()
    {
        const string expected = "quit";
        TestMessage? result = null;

        IpcChannel<TestMessage>.Create(_channelId)
            .Messages.Subscribe(
                message => result = message, _cts.Token);
        
        await SendMessage(new TestMessage("quit"), _channelId, _cts);

        result!.Action.Should().Be(expected);
    }
    
    [Fact]
    public async Task ShouldReceiveTwoMessages()
    {
        var messagesSent = 0;
    
        var sut = IpcChannel<TestMessage>.Create(_channelId);
        
        sut.Messages.Subscribe(_ => messagesSent++, e => throw e, _cts.Token);
        
        await SendMessage(new TestMessage("quit"), _channelId, _cts);
        await SendMessage(new TestMessage("activate"), _channelId, _cts);
    
        messagesSent.Should().Be(2);
    }
    
    [Fact]
    public void ShouldThrowException_WhenTwoChannelsWithSameIdIsCreated()
    {
        var exceptionThrown = new Exception();
        IpcChannel<TestMessage>.Create(_channelId)
            .Messages.Subscribe(_ => {}, _cts.Token);
    
        IpcChannel<TestMessage>.Create(_channelId)
            .Messages.Subscribe(_ => { },
                exception => exceptionThrown = exception, _cts.Token);
    
        exceptionThrown.Should().BeOfType<IOException>();
    }
    
    [Fact]
    public void ShouldReturnTrue_WhenChannelExist()
    {
        IpcChannel<TestMessage>.Create(_channelId)
            .Messages.Subscribe(_=>{}, _cts.Token);
        var isRunning = IpcChannel<TestMessage>.IsRunning(_channelId);
    
        isRunning.Should().BeTrue();
    }
    
    [Fact]
    public void ShouldReturnFalse_WhenChannelDoesNotExist() =>
        IpcChannel<TestMessage>.IsRunning(_channelId)
            .Should().BeFalse();
    
    
    [Fact]
    public async Task ShouldTakeDownChannel_WhenCancelled()
    {
        IpcChannel<TestMessage>.Create(_channelId)
            .Messages.Subscribe(_=>{}, _cts.Token);
    
        await Task.Delay(300, _cts.Token);
        
        _cts.Cancel();
    
        IpcChannel<TestMessage>.IsRunning(_channelId)
            .Should().BeFalse();
    }
    
    [Fact]
    public async Task ShouldTimeoutWhen_SendMessageDoNotGetAResponse()
    {
        await IpcChannel<TestMessage>.Send("bogusChannelId", new TestMessage("noop"), 100);
    }
    
    private static async Task SendMessage(TestMessage message, string appId, CancellationTokenSource cts, int delayInMs = 200)
    {
        await IpcChannel<TestMessage>.Send(appId, message);
        await Task.Delay(delayInMs, cts.Token);
    }

    public async void Dispose()
    {
        _cts.Cancel();
        await Task.Delay(400);
    }
}

public record TestMessage(string Action);
