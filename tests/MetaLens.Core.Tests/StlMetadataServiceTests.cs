using System.Buffers.Binary;
using System.Text;
using MetaLens.Core;

namespace MetaLens.Core.Tests;

public sealed class StlMetadataServiceTests
{
    private const string AsciiModel =
        "solid exported_by_jdoe\n" +
        "  facet normal 0 0 1\n" +
        "    outer loop\n" +
        "      vertex 0 0 0\n" +
        "      vertex 1 0 0\n" +
        "      vertex 0 1 0\n" +
        "    endloop\n" +
        "  endfacet\n" +
        "endsolid exported_by_jdoe\n";

    private readonly FileMetadataService _fileService = new();
    private readonly StlMetadataService _service = new();

    [Fact]
    public void Read_RecognizesBinaryStlAndShowsHeaderText()
    {
        using var file = TemporaryFile.Create(".stl", CreateBinaryStl("solid C:\\Users\\jdoe\\part.SLDPRT", triangleCount: 2));

        var snapshot = _fileService.Read(file.Path);
        var stl = _service.Read(file.Path);

        Assert.Equal(StlMetadataService.BinaryFormat, snapshot.DetectedFormat);
        Assert.Equal(2, stl.TriangleCount);
        Assert.Contains("jdoe", stl.HeaderText, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_EmptiesBinaryHeaderAndKeepsGeometry()
    {
        var contents = CreateBinaryStl("Exported by jdoe on WORKSTATION-7", triangleCount: 3);
        using var file = TemporaryFile.Create(".stl", contents);
        var snapshot = _fileService.Read(file.Path);

        _service.Clean(snapshot);

        var cleaned = File.ReadAllBytes(file.Path);
        Assert.Equal(contents.Length, cleaned.Length);
        Assert.All(cleaned[..80], value => Assert.Equal(0, value));
        Assert.Equal(contents[80..], cleaned[80..]);
        Assert.Equal(string.Empty, _service.Read(file.Path).HeaderText);
    }

    [Fact]
    public void Clean_RemovesAsciiSolidNamesAndKeepsFacets()
    {
        using var file = TemporaryFile.Create(".stl", Encoding.ASCII.GetBytes(AsciiModel));
        var snapshot = _fileService.Read(file.Path);
        Assert.Equal(StlMetadataService.AsciiFormat, snapshot.DetectedFormat);
        Assert.Equal(["exported_by_jdoe"], _service.Read(file.Path).SolidNames);

        _service.Clean(snapshot);

        var cleaned = File.ReadAllText(file.Path, Encoding.ASCII);
        Assert.DoesNotContain("jdoe", cleaned, StringComparison.Ordinal);
        Assert.StartsWith("solid\n", cleaned, StringComparison.Ordinal);
        Assert.EndsWith("endsolid\n", cleaned, StringComparison.Ordinal);
        Assert.Contains("      vertex 1 0 0\n", cleaned, StringComparison.Ordinal);
        Assert.Equal(1, _service.Read(file.Path).TriangleCount);
    }

    [Fact]
    public void Clean_SetsNeutralTimesAndRemovesZoneIdentifier()
    {
        using var file = TemporaryFile.Create(".stl", CreateBinaryStl("header", triangleCount: 1));
        File.WriteAllText(file.Path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://example.com/part.stl\r\n");
        Assert.True(_service.Read(file.Path).HasZoneIdentifier);
        var snapshot = _fileService.Read(file.Path);

        _service.Clean(snapshot);

        Assert.False(File.Exists(file.Path + ":Zone.Identifier"));
        Assert.Equal(StlMetadataService.NeutralTimestamp, File.GetCreationTime(file.Path));
        Assert.Equal(StlMetadataService.NeutralTimestamp, File.GetLastWriteTime(file.Path));
        Assert.Equal(StlMetadataService.NeutralTimestamp, File.GetLastAccessTime(file.Path));
    }

    [Fact]
    public void Clean_KeepsReadOnlyAttribute()
    {
        using var file = TemporaryFile.Create(".stl", CreateBinaryStl("header", triangleCount: 1));
        File.SetAttributes(file.Path, FileAttributes.ReadOnly);
        var snapshot = _fileService.Read(file.Path);

        _service.Clean(snapshot);

        Assert.True(File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal(string.Empty, _service.Read(file.Path).HeaderText);
    }

    [Fact]
    public void Clean_RejectsFileChangedSinceItWasOpened()
    {
        using var file = TemporaryFile.Create(".stl", CreateBinaryStl("header", triangleCount: 1));
        var snapshot = _fileService.Read(file.Path);
        File.WriteAllBytes(file.Path, CreateBinaryStl("changed", triangleCount: 2));
        var currentContents = File.ReadAllBytes(file.Path);

        var exception = Assert.Throws<IOException>(() => _service.Clean(snapshot));

        Assert.Contains("changed since it was opened", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(currentContents, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Clean_RejectsNonStlFiles()
    {
        using var file = TemporaryFile.Create(".stl", "not a model"u8.ToArray());
        var snapshot = _fileService.Read(file.Path);

        Assert.Equal("Unknown format", snapshot.DetectedFormat);
        Assert.Throws<NotSupportedException>(() => _service.Clean(snapshot));
    }

    private static byte[] CreateBinaryStl(string header, int triangleCount)
    {
        var contents = new byte[84 + triangleCount * 50];
        Encoding.ASCII.GetBytes(header).CopyTo(contents, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(contents.AsSpan(80), (uint)triangleCount);
        for (var index = 84; index < contents.Length; index++)
        {
            contents[index] = (byte)(index % 251);
        }

        return contents;
    }

    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path) => Path = path;

        public string Path { get; }

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
}
