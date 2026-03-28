using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RetentionCleanerTests
{
    [Fact]
    public void Cleanup_DeletesOnlyOldFilesInTopDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(nested);

        var oldFile = Path.Combine(root, "old.ts");
        var newFile = Path.Combine(root, "new.ts");
        var nestedFile = Path.Combine(nested, "nested.ts");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.WriteAllText(nestedFile, "nested");
        File.SetLastWriteTime(oldFile, new DateTime(2026, 2, 1, 0, 0, 0));
        File.SetLastWriteTime(newFile, new DateTime(2026, 3, 27, 0, 0, 0));
        File.SetLastWriteTime(nestedFile, new DateTime(2026, 2, 1, 0, 0, 0));

        var cleaner = new RetentionCleaner();
        var deleted = cleaner.Cleanup(root, 30, new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.FromHours(9)));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
        Assert.True(File.Exists(nestedFile));
    }
}
