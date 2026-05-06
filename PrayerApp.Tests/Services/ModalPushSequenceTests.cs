using Microsoft.Maui.Dispatching;
using NSubstitute;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class ModalPushSequenceTests
{
    private readonly IDispatcher _dispatcher = Substitute.For<IDispatcher>();

    public ModalPushSequenceTests()
    {
        _dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            ((Action)call[0])();
            return true;
        });
    }

    [Fact]
    public async Task Execute_AwaitsGateBeforeDispatching()
    {
        // Cold-start contract: if WhenShellReady has not completed,
        // the dispatcher must not be invoked yet.
        var gate = new TaskCompletionSource();
        var pushed = false;
        Func<Task> push = () => { pushed = true; return Task.CompletedTask; };

        var task = ModalPushSequence.ExecuteAsync(_dispatcher, gate.Task, push);

        await Task.Yield();
        _dispatcher.DidNotReceive().Dispatch(Arg.Any<Action>());
        Assert.False(pushed);

        gate.SetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(2));

        _dispatcher.Received(1).Dispatch(Arg.Any<Action>());
        Assert.True(pushed);
    }

    [Fact]
    public async Task Execute_DispatchesPushOnceShellReady()
    {
        var pushed = false;
        Func<Task> push = () => { pushed = true; return Task.CompletedTask; };

        await ModalPushSequence.ExecuteAsync(_dispatcher, Task.CompletedTask, push);

        _dispatcher.Received(1).Dispatch(Arg.Any<Action>());
        Assert.True(pushed);
    }

    [Fact]
    public async Task Execute_AwaitsCompletionOfDispatchedPush()
    {
        // Caller must observe the modal-push completion; if the dispatched
        // action is still in-flight, ExecuteAsync must not return early.
        var pushGate = new TaskCompletionSource();
        Func<Task> push = () => pushGate.Task;

        var task = ModalPushSequence.ExecuteAsync(_dispatcher, Task.CompletedTask, push);

        await Task.Yield();
        Assert.False(task.IsCompleted);

        pushGate.SetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Execute_PushThrows_StillCompletesAndDoesNotDeadlock()
    {
        // The TCS sits in the dispatched action's finally block — a throw
        // from pushModal must not leave the awaiting caller hung.
        Func<Task> push = () => throw new InvalidOperationException("push failed");

        var task = ModalPushSequence.ExecuteAsync(_dispatcher, Task.CompletedTask, push);

        await task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
