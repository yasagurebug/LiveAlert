using LiveAlert.Windows.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RecordingEnvironmentValidatorTests
{
    [Fact]
    public async Task Validation_RequiresFfprobe()
    {
        var probe = new Probe();
        var result = await new RecordingEnvironmentValidator(probe).ValidateAsync(Path.GetTempPath(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("ffprobe unavailable", result.Message);
        Assert.Equal(new[] { "yt-dlp", "ffmpeg", "ffprobe" }, probe.Commands);
    }

    private sealed class Probe : IExternalCommandProbe
    {
        public List<string> Commands { get; } = new();
        public Task<CommandProbeResult> ProbeAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            Commands.Add(fileName);
            return Task.FromResult(new CommandProbeResult(fileName != "ffprobe", "ffprobe unavailable"));
        }
    }
}
