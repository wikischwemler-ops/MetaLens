using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace MetaLens.Core;

public sealed record StlMetadataInfo(
    string Variant,
    string HeaderText,
    long TriangleCount,
    IReadOnlyList<string> SolidNames,
    bool HasZoneIdentifier);

public sealed partial class StlMetadataService
{
    public const string BinaryFormat = "Binary STL";
    public const string AsciiFormat = "ASCII STL";

    public static readonly DateTime NeutralTimestamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

    private const int HeaderLength = 80;
    private const int BinaryPrefixLength = HeaderLength + sizeof(uint);
    private const int TriangleRecordLength = 50;
    private const int AsciiProbeLength = 1024;
    private const long MaximumCleanableSize = 512L * 1024 * 1024;
    private const string ZoneIdentifierStream = ":Zone.Identifier";

    public bool Supports(FileMetadataSnapshot snapshot) =>
        snapshot.DetectedFormat is BinaryFormat or AsciiFormat;

    public static string? DetectVariant(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var length = stream.Length;
        Span<byte> prefix = stackalloc byte[BinaryPrefixLength];
        if (length >= BinaryPrefixLength
            && stream.ReadAtLeast(prefix, BinaryPrefixLength, throwOnEndOfStream: false) == BinaryPrefixLength)
        {
            var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(prefix[HeaderLength..]);
            if (BinaryPrefixLength + (long)triangleCount * TriangleRecordLength == length)
            {
                return BinaryFormat;
            }
        }

        stream.Position = 0;
        var probe = new byte[(int)Math.Min(length, AsciiProbeLength)];
        var probeLength = stream.ReadAtLeast(probe, probe.Length, throwOnEndOfStream: false);
        var text = Encoding.Latin1.GetString(probe, 0, probeLength).TrimStart();
        return text.StartsWith("solid", StringComparison.OrdinalIgnoreCase)
               && (text.Contains("facet", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("endsolid", StringComparison.OrdinalIgnoreCase))
            ? AsciiFormat
            : null;
    }

    public StlMetadataInfo Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        info.Refresh();
        if (!info.Exists)
        {
            throw new FileNotFoundException("The STL file no longer exists.", fullPath);
        }

        var hasZoneIdentifier = File.Exists(fullPath + ZoneIdentifierStream);
        string? variant;
        using (var stream = OpenRead(fullPath))
        {
            variant = DetectVariant(stream);
            if (variant == BinaryFormat)
            {
                stream.Position = 0;
                var prefix = new byte[BinaryPrefixLength];
                stream.ReadExactly(prefix);
                return new StlMetadataInfo(
                    BinaryFormat,
                    FormatHeader(prefix.AsSpan(0, HeaderLength)),
                    BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(HeaderLength)),
                    [],
                    hasZoneIdentifier);
            }
        }

        if (variant != AsciiFormat)
        {
            throw new InvalidDataException("The file is not a recognizable STL file.");
        }

        EnsureCleanableSize(info.Length);
        var text = File.ReadAllText(fullPath, Encoding.Latin1);
        var names = SolidLinePattern().Matches(text)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new StlMetadataInfo(
            AsciiFormat,
            string.Empty,
            FacetPattern().Count(text),
            names,
            hasZoneIdentifier);
    }

    public void Clean(FileMetadataSnapshot original)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (!Supports(original))
        {
            throw new NotSupportedException("Metadata cleaning is currently supported for STL files only.");
        }

        var fullPath = Path.GetFullPath(original.FullPath);
        var info = new FileInfo(fullPath);
        info.Refresh();
        if (!info.Exists)
        {
            throw new FileNotFoundException("The STL file no longer exists.", fullPath);
        }

        if (info.Length != original.SizeInBytes || info.LastWriteTimeUtc != original.LastWriteTimeUtc)
        {
            throw new IOException("The STL file has changed since it was opened. Reopen it before cleaning metadata.");
        }

        EnsureCleanableSize(info.Length);

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("The STL file has no parent directory.");
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}{Path.GetExtension(fullPath)}");
        var originalMetadata = Read(fullPath);
        var originalAttributes = File.GetAttributes(fullPath);
        var contentsReplaced = false;

        try
        {
            if (originalMetadata.Variant == BinaryFormat)
            {
                File.Copy(fullPath, temporaryPath);
                File.SetAttributes(temporaryPath, FileAttributes.Normal);
                using var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Write, FileShare.None);
                stream.Write(new byte[HeaderLength]);
            }
            else
            {
                var text = File.ReadAllText(fullPath, Encoding.Latin1);
                File.WriteAllText(temporaryPath, SolidLinePattern().Replace(text, "${indent}${keyword}"), Encoding.Latin1);
            }

            VerifyCleanCopy(temporaryPath, originalMetadata);

            info.Refresh();
            if (!info.Exists || info.Length != original.SizeInBytes || info.LastWriteTimeUtc != original.LastWriteTimeUtc)
            {
                throw new IOException("The STL file changed while metadata was being cleaned. The cleaned copy was not applied.");
            }

            if (originalAttributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullPath, originalAttributes & ~FileAttributes.ReadOnly);
            }

            File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: false);
            contentsReplaced = true;

            var zoneIdentifierPath = fullPath + ZoneIdentifierStream;
            if (File.Exists(zoneIdentifierPath))
            {
                File.Delete(zoneIdentifierPath);
            }

            File.SetAttributes(fullPath, originalAttributes);
            File.SetCreationTime(fullPath, NeutralTimestamp);
            File.SetLastWriteTime(fullPath, NeutralTimestamp);
            File.SetLastAccessTime(fullPath, NeutralTimestamp);
        }
        catch (Exception cleanException) when (cleanException is IOException
                                               or UnauthorizedAccessException
                                               or ArgumentException
                                               or NotSupportedException
                                               or InvalidDataException
                                               or System.Security.SecurityException)
        {
            throw new IOException(contentsReplaced
                ? "The STL metadata was removed, but the origin marker or file times could not be reset completely."
                : "STL metadata could not be cleaned. The original file was retained.", cleanException);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.SetAttributes(temporaryPath, FileAttributes.Normal);
                File.Delete(temporaryPath);
            }
        }
    }

    private void VerifyCleanCopy(string temporaryPath, StlMetadataInfo originalMetadata)
    {
        var cleaned = Read(temporaryPath);
        if (cleaned.Variant != originalMetadata.Variant
            || cleaned.TriangleCount != originalMetadata.TriangleCount
            || cleaned.HeaderText.Length != 0
            || cleaned.SolidNames.Count != 0)
        {
            throw new IOException("Could not verify the cleaned STL copy.");
        }
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static void EnsureCleanableSize(long length)
    {
        if (length > MaximumCleanableSize)
        {
            throw new IOException($"STL metadata handling is limited to files up to {MaximumCleanableSize / (1024 * 1024)} MB.");
        }
    }

    private static string FormatHeader(ReadOnlySpan<byte> header)
    {
        var builder = new StringBuilder(header.Length);
        foreach (var value in header)
        {
            builder.Append(value is >= 0x20 and < 0x7F ? (char)value : ' ');
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<keyword>solid|endsolid)(?<name>[^\r\n]*)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SolidLinePattern();

    [GeneratedRegex(@"^[ \t]*facet\b", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FacetPattern();
}
