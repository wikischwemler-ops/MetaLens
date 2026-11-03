namespace MetaLens.Core;

public sealed record FileMetadataChanges(
    string FileName,
    DateTime CreationTime,
    DateTime LastAccessTime,
    DateTime LastWriteTime,
    bool IsReadOnly,
    bool IsHidden,
    bool IsSystem);
