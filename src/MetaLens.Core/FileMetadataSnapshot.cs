namespace MetaLens.Core;

public sealed record FileMetadataSnapshot(
    string FullPath,
    string FileName,
    string Extension,
    long SizeInBytes,
    DateTime CreationTime,
    DateTime LastAccessTime,
    DateTime LastWriteTime,
    DateTime LastWriteTimeUtc,
    string DetectedFormat,
    string FormatDetectionDetails,
    FileAttributes Attributes)
{
    public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);
    public bool IsHidden => Attributes.HasFlag(FileAttributes.Hidden);
    public bool IsSystem => Attributes.HasFlag(FileAttributes.System);
}
