using System.Diagnostics;
using CyberpunkDb.Api.Desktop;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Desktop;

public class ChildProcessJobTests
{
    [Fact]
    public void Children_die_with_the_job()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var job = new ChildProcessJob();
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 > nul") { CreateNoWindow = true, UseShellExecute = false })!;
        job.Add(child).Should().BeTrue();
        child.HasExited.Should().BeFalse();

        // Closing the job's last handle is what happens when the app's process ends, however it ends.
        job.Dispose();
        child.WaitForExit(5000).Should().BeTrue();
    }
}
