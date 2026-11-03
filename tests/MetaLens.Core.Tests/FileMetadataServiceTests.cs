using MetaLens.Core;

namespace MetaLens.Core.Tests;

public sealed class FileMetadataServiceTests
{
    private readonly FileMetadataService _service = new();

    [Fact]
    public void Read_InspectsUnknownFileWithoutInterpretingItsContents()
    {
        using var file = TemporaryFile.Create(".bin", [0x01, 0x02, 0x03]);

        var result = _service.Read(file.Path);

        Assert.Equal("Unknown format", result.DetectedFormat);
        Assert.Equal(file.Path, result.FullPath);
        Assert.Equal(3, result.SizeInBytes);
        Assert.Contains("not interpreted", result.FormatDetectionDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_RecognizesPdfFromSignatureInsteadOfExtension()
    {
        using var file = TemporaryFile.Create(".data", "%PDF-1.7\n"u8.ToArray());

        var result = _service.Read(file.Path);

        Assert.Equal("PDF document", result.DetectedFormat);
        Assert.Contains("signature", result.FormatDetectionDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_ChangesFileSystemTimesWithoutChangingFileContents()
    {
        byte[] contents = [0x00, 0x01, 0xFE, 0xFF];
        using var file = TemporaryFile.Create(".dat", contents);
        var original = _service.Read(file.Path);
        var changes = new FileMetadataChanges(
            original.FileName,
            original.CreationTime.AddMinutes(-2),
            original.LastAccessTime.AddMinutes(-1),
            original.LastWriteTime.AddMinutes(-3),
            original.IsReadOnly,
            original.IsHidden,
            original.IsSystem);

        _service.Save(original, changes);

        Assert.Equal(contents, File.ReadAllBytes(file.Path));
        Assert.Equal(changes.LastWriteTime, File.GetLastWriteTime(file.Path));
        Assert.Equal(changes.CreationTime, File.GetCreationTime(file.Path));
    }

    [Fact]
    public void Save_RejectsFileChangedSinceItWasInspected()
    {
        using var file = TemporaryFile.Create(".txt", "before"u8.ToArray());
        var original = _service.Read(file.Path);
        File.WriteAllText(file.Path, "changed after inspection");
        var currentContents = File.ReadAllBytes(file.Path);
        var changes = new FileMetadataChanges(
            original.FileName,
            original.CreationTime,
            original.LastAccessTime,
            original.LastWriteTime,
            original.IsReadOnly,
            original.IsHidden,
            original.IsSystem);

        var exception = Assert.Throws<IOException>(() => _service.Save(original, changes));

        Assert.Contains("changed since it was opened", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(currentContents, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Read_RejectsDirectories()
    {
        using var directory = TemporaryDirectory.Create();

        Assert.Throws<ArgumentException>(() => _service.Read(directory.Path));
    }

    [Fact]
    public void ReadCategoryInventory_ListsAllTwentyFiveCategories()
    {
        using var file = TemporaryFile.Create(".txt", "metadata inventory"u8.ToArray());
        var snapshot = _service.Read(file.Path);

        var entries = _service.ReadCategoryInventory(snapshot);

        Assert.Equal(25, entries.Count);
        Assert.Equal(Enumerable.Range(1, 25), entries.Select(entry => entry.Number));
        Assert.Contains(entries, entry => entry.Category == "Owner");
        Assert.Contains(entries, entry => entry.Category == "Permissions");
        Assert.Contains(entries, entry => entry.Category == "Embedded data");
        Assert.Contains(entries, entry => entry.Category == "Cache and sidecar files");
        Assert.Contains(entries, entry => entry.InspectionStatus == "Not inspected");
    }

    [Fact]
    public void Save_RenamesFileAndPreservesItsContents()
    {
        byte[] contents = [0x11, 0x22, 0x33];
        using var file = TemporaryFile.Create(".old", contents);
        var original = _service.Read(file.Path);
        var newName = $"{Guid.NewGuid():N}.new";
        var changes = new FileMetadataChanges(
            newName,
            original.CreationTime,
            original.LastAccessTime,
            original.LastWriteTime,
            original.IsReadOnly,
            original.IsHidden,
            original.IsSystem);

        _service.Save(original, changes);

        var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path)!, newName);
        Assert.False(File.Exists(file.Path));
        Assert.Equal(contents, File.ReadAllBytes(newPath));
        file.TrackRenamedPath(newPath);
    }

    [Fact]
    public void Save_UpdatesHiddenAndSystemAttributes()
    {
        using var file = TemporaryFile.Create(".dat", [0xAB]);
        var original = _service.Read(file.Path);
        var changes = new FileMetadataChanges(
            original.FileName,
            original.CreationTime,
            original.LastAccessTime,
            original.LastWriteTime,
            original.IsReadOnly,
            IsHidden: true,
            IsSystem: true);

        _service.Save(original, changes);

        var attributes = File.GetAttributes(file.Path);
        Assert.True(attributes.HasFlag(FileAttributes.Hidden));
        Assert.True(attributes.HasFlag(FileAttributes.System));
    }

    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path) => Path = path;

        public string Path { get; private set; }

        public void TrackRenamedPath(string path) => Path = path;

        public static TemporaryFile Create(string extension, byte[] contents)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(path, contents);
            return new TemporaryFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.SetAttributes(Path, FileAttributes.Normal);
                File.Delete(Path);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path);
    }
}
