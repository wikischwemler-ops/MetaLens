namespace MetaLens.Core;

public sealed record FileMetadataChanges(
    DateTime CreationTime,
    DateTime LastAccessTime,
    DateTime LastWriteTime,
    bool IsReadOnly);
