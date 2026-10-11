namespace MetaLens.Core;

public sealed class FileMetadataService
{
    private const int SignatureLength = 12;

    public FileMetadataSnapshot Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            throw new ArgumentException("Select a file, not a folder.", nameof(path));
        }

        var info = new FileInfo(fullPath);
        info.Refresh();

        if (!info.Exists)
        {
            throw new FileNotFoundException("The selected file no longer exists.", fullPath);
        }

        var attributes = File.GetAttributes(fullPath);
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            throw new ArgumentException("Select a file, not a folder.", nameof(path));
        }

        var (format, detectionDetails) = DetectFormat(fullPath, info.Extension);

        return new FileMetadataSnapshot(
            fullPath,
            info.Name,
            info.Extension,
            info.Length,
            info.CreationTime,
            info.LastAccessTime,
            info.LastWriteTime,
            info.LastWriteTimeUtc,
            format,
            detectionDetails,
            attributes);
    }

    public void Save(FileMetadataSnapshot original, FileMetadataChanges changes)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(changes);

        var current = new FileInfo(original.FullPath);
        current.Refresh();

        if (!current.Exists)
        {
            throw new FileNotFoundException("The file was removed before the changes could be saved.", original.FullPath);
        }

        var currentAttributes = File.GetAttributes(original.FullPath);
        if (current.Length != original.SizeInBytes
            || current.LastWriteTimeUtc != original.LastWriteTimeUtc
            || currentAttributes != original.Attributes)
        {
            throw new IOException("The file has changed since it was opened. Reopen it before saving metadata.");
        }

        var originalAttributes = File.GetAttributes(original.FullPath);
        var originalCreationTime = File.GetCreationTime(original.FullPath);
        var originalLastAccessTime = File.GetLastAccessTime(original.FullPath);
        var originalLastWriteTime = File.GetLastWriteTime(original.FullPath);

        try
        {
            SetReadOnly(original.FullPath, originalAttributes, isReadOnly: false);
            File.SetCreationTime(original.FullPath, changes.CreationTime);
            File.SetLastAccessTime(original.FullPath, changes.LastAccessTime);
            File.SetLastWriteTime(original.FullPath, changes.LastWriteTime);
            SetReadOnly(original.FullPath, File.GetAttributes(original.FullPath), changes.IsReadOnly);
        }
        catch (Exception saveException) when (saveException is IOException
                                              or UnauthorizedAccessException
                                              or ArgumentException
                                              or NotSupportedException
                                              or System.Security.SecurityException)
        {
            try
            {
                SetReadOnly(original.FullPath, File.GetAttributes(original.FullPath), isReadOnly: false);
                File.SetCreationTime(original.FullPath, originalCreationTime);
                File.SetLastAccessTime(original.FullPath, originalLastAccessTime);
                File.SetLastWriteTime(original.FullPath, originalLastWriteTime);
                File.SetAttributes(original.FullPath, originalAttributes);
            }
            catch (Exception rollbackException) when (rollbackException is IOException
                                                       or UnauthorizedAccessException
                                                       or ArgumentException
                                                       or NotSupportedException
                                                       or System.Security.SecurityException)
            {
                throw new IOException(
                    "Saving metadata failed, and restoring the original metadata also failed. Check the file immediately.",
                    new AggregateException(saveException, rollbackException));
            }

            throw new IOException("Metadata could not be saved. The original metadata was restored.", saveException);
        }
    }

    private static (string Format, string Details) DetectFormat(string path, string extension)
    {
        Span<byte> signature = stackalloc byte[SignatureLength];
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var bytesRead = stream.ReadAtLeast(signature, SignatureLength, throwOnEndOfStream: false);
            signature = signature[..bytesRead];
        }

        if (signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("PNG image", "Recognized from the file signature.");
        }

        if (signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return ("JPEG image", "Recognized from the file signature.");
        }

        if (signature.StartsWith("GIF87a"u8) || signature.StartsWith("GIF89a"u8))
        {
            return ("GIF image", "Recognized from the file signature.");
        }

        if (signature.StartsWith("%PDF-"u8))
        {
            return ("PDF document", "Recognized from the file signature.");
        }

        if (signature.StartsWith("BM"u8))
        {
            return ("BMP image", "Recognized from the file signature.");
        }

        if (signature.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 })
            || signature.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }))
        {
            return ("TIFF image", "Recognized from the file signature.");
        }

        if (signature.Length >= 12 && signature[..4].SequenceEqual("RIFF"u8) && signature[8..12].SequenceEqual("WEBP"u8))
        {
            return ("WebP image", "Recognized from the file signature.");
        }

        if (signature.StartsWith("PK\u0003\u0004"u8))
        {
            return ("ZIP archive", "Recognized from the file signature. Some document formats use ZIP containers.");
        }

        if (signature.StartsWith("ID3"u8))
        {
            return ("MP3 audio", "Recognized from the file signature.");
        }

        var suffix = string.IsNullOrWhiteSpace(extension) ? "no extension" : $"extension {extension}";
        return ("Unknown format", $"No known signature detected ({suffix}). File contents were not interpreted.");
    }

    private static void SetReadOnly(string path, FileAttributes attributes, bool isReadOnly)
    {
        var updated = isReadOnly
            ? attributes | FileAttributes.ReadOnly
            : attributes & ~FileAttributes.ReadOnly;

        if (updated != attributes)
        {
            File.SetAttributes(path, updated);
        }
    }
}
