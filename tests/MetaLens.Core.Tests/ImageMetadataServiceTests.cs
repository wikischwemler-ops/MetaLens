using MetaLens.Core;
using System.Buffers.Binary;

namespace MetaLens.Core.Tests;

public sealed class ImageMetadataServiceTests
{
    private readonly ImageMetadataService _service = new();

    [Fact]
    public void Read_ListsPngMetadataAsReadOnly()
    {
        using var image = TemporaryImage.CreatePng();

        var rows = _service.Read(image.Path);

        Assert.Contains(rows, row => row.Name == "Image Width");
        Assert.DoesNotContain(rows, row => row.CanEdit);
    }

    [Fact]
    public void Read_ListsEditableExifPlaceholdersForJpeg()
    {
        using var image = TemporaryImage.CreateJpeg();

        var rows = _service.Read(image.Path);

        Assert.Contains(rows, row => row.EditKey == "EXIF:ImageDescription" && row.CanEdit);
        Assert.Contains(rows, row => row.EditKey == "EXIF:Artist" && row.CanEdit);
        Assert.Contains(rows, row => row.EditKey == "EXIF:DateTimeOriginal" && row.CanEdit);
        Assert.Contains(rows, row => row.EditKey == "IPTC:Headline" && row.CanEdit);
        Assert.Contains(rows, row => row.EditKey == "XMP-dc:Title" && row.CanEdit);
    }

    [Fact]
    public void Save_AddsExifDescriptionWithoutChangingImagePixels()
    {
        using var image = TemporaryImage.CreateJpeg();
        var originalBytes = File.ReadAllBytes(image.Path);
        var rows = _service.Read(image.Path);
        var description = Assert.Single(rows, row => row.EditKey == "EXIF:ImageDescription");
        description.Value = "Metadata test";
        var original = new FileMetadataService().Read(image.Path);

        _service.Save(original, [description]);

        var updated = _service.Read(image.Path);
        Assert.Equal("Metadata test", Assert.Single(updated, row => row.EditKey == "EXIF:ImageDescription").Value);
        Assert.Equal(GetJpegScanData(originalBytes), GetJpegScanData(File.ReadAllBytes(image.Path)));
    }

    [Fact]
    public void Save_WritesAndVerifiesIptcAndXmpValues()
    {
        using var image = TemporaryImage.CreateJpeg();
        var originalBytes = File.ReadAllBytes(image.Path);
        var rows = _service.Read(image.Path);
        var headline = Assert.Single(rows, row => row.EditKey == "IPTC:Headline");
        var title = Assert.Single(rows, row => row.EditKey == "XMP-dc:Title");
        headline.Value = "IPTC metadata test";
        title.Value = "XMP metadata test";
        var original = new FileMetadataService().Read(image.Path);

        _service.Save(original, [headline, title]);

        var updated = _service.Read(image.Path);
        Assert.Equal("IPTC metadata test", Assert.Single(updated, row => row.EditKey == "IPTC:Headline").Value);
        Assert.Equal("XMP metadata test", Assert.Single(updated, row => row.EditKey == "XMP-dc:Title").Value);
        Assert.Equal(GetJpegScanData(originalBytes), GetJpegScanData(File.ReadAllBytes(image.Path)));
    }

    [Fact]
    public void Save_RejectsInvalidExifDateWithoutChangingTheOriginal()
    {
        using var image = TemporaryImage.CreateJpeg();
        var originalBytes = File.ReadAllBytes(image.Path);
        var row = Assert.Single(_service.Read(image.Path), item => item.EditKey == "EXIF:DateTimeOriginal");
        row.Value = "not a timestamp";
        var original = new FileMetadataService().Read(image.Path);

        Assert.Throws<ArgumentException>(() => _service.Save(original, [row]));

        Assert.Equal(originalBytes, File.ReadAllBytes(image.Path));
    }

    [Fact]
    public void Save_RejectsPngEmbeddedExifEditing()
    {
        using var image = TemporaryImage.CreatePng();
        var original = new FileMetadataService().Read(image.Path);
        var row = new ImageMetadataRow("Exif IFD0", "Image Description", "not allowed", "EXIF:ImageDescription");

        Assert.Throws<NotSupportedException>(() => _service.Save(original, [row]));
    }

    private static byte[] GetJpegScanData(byte[] bytes)
    {
        var offset = 2;
        while (offset + 4 < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                throw new InvalidDataException("Invalid JPEG marker.");
            }

            while (offset < bytes.Length && bytes[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                break;
            }

            var marker = bytes[offset++];
            if (marker == 0xDA)
            {
                return bytes[(offset - 2)..];
            }

            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            var segmentLength = (bytes[offset] << 8) | bytes[offset + 1];
            offset += segmentLength;
        }

        throw new InvalidDataException("JPEG image data segment was not found.");
    }

    private sealed class TemporaryImage : IDisposable
    {
        private TemporaryImage(string path) => Path = path;

        public string Path { get; }

        public static TemporaryImage CreatePng()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "MetaLens.ico");
            var icon = File.ReadAllBytes(iconPath);
            var pngLength = BinaryPrimitives.ReadInt32LittleEndian(icon.AsSpan(14, 4));
            var pngOffset = BinaryPrimitives.ReadInt32LittleEndian(icon.AsSpan(18, 4));
            File.WriteAllBytes(path, icon.AsSpan(pngOffset, pngLength).ToArray());
            return new TemporaryImage(path);
        }

        public static TemporaryImage CreateJpeg()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
            var fixturePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "ExifFixture.jpg");
            File.Copy(fixturePath, path);
            return new TemporaryImage(path);
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
