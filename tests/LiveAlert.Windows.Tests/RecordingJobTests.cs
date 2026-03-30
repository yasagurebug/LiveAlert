using System;
using System.IO;
using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RecordingJobTests
{
    [Fact]
    public void HasRecordedContent_ReturnsTrue_WhenTsFileExistsAndIsNotEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tsPath = Path.Combine(root, "recording.ts");

        try
        {
            File.WriteAllText(tsPath, "data");

            Assert.True(RecordingJob.HasRecordedContent(tsPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void HasRecordedContent_ReturnsFalse_WhenTsFileIsMissingOrEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var missingPath = Path.Combine(root, "missing.ts");
        var emptyPath = Path.Combine(root, "empty.ts");

        try
        {
            File.WriteAllText(emptyPath, string.Empty);

            Assert.False(RecordingJob.HasRecordedContent(missingPath));
            Assert.False(RecordingJob.HasRecordedContent(emptyPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
