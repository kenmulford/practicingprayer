using NSubstitute;
using PrayerApp.Helpers;
using PrayerApp.Services;

namespace PrayerApp.Tests.Helpers;

public class TaskExtensionsTests
{
    [Fact]
    public void SafeFireAndForget_DoesNotThrowOnSuccess()
    {
        var log = Substitute.For<IDiagnosticLog>();

        // Should complete without throwing
        Task.CompletedTask.SafeFireAndForget(log);

        log.DidNotReceive().Log(Arg.Any<string>(), Arg.Any<Exception>());
    }

    [Fact]
    public async Task SafeFireAndForget_LogsExceptionOnFailure()
    {
        var log = Substitute.For<IDiagnosticLog>();
        var ex = new InvalidOperationException("boom");

        Task.FromException(ex).SafeFireAndForget(log);

        // Give the async void method time to complete
        await Task.Delay(100);

        log.Received(1).Log("SafeFireAndForget", ex);
    }

    [Fact]
    public async Task SafeFireAndForget_SwallowsExceptionWhenNoLog()
    {
        var ex = new InvalidOperationException("boom");

        // Should not throw even with null log
        Task.FromException(ex).SafeFireAndForget(null);

        // Give the async void method time to complete
        await Task.Delay(100);

        // If we got here without an unhandled exception, the test passes
    }
}
