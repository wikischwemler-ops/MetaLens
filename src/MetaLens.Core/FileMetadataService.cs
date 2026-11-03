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

    public IReadOnlyList<MetadataCategoryEntry> ReadCategoryInventory(FileMetadataSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var currentUser = Environment.UserName;
        var currentDomain = Environment.UserDomainName;
        var computerName = Environment.MachineName;
        var directory = Path.GetDirectoryName(snapshot.FullPath) ?? string.Empty;
        var uncPath = snapshot.FullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? snapshot.FullPath
            : "Not a UNC path";
        var commonSidecars = new[] { "Thumbs.db", "desktop.ini" }
            .Select(name => Path.Combine(directory, name))
            .Where(File.Exists)
            .Select(Path.GetFileName)
            .ToArray();

        var sidecarDetails = commonSidecars.Length == 0
            ? "No Thumbs.db or desktop.ini found beside this file."
            : string.Join(", ", commonSidecars);
        var hasImageMetadataProvider = snapshot.DetectedFormat is
            "JPEG image" or "PNG image" or "TIFF image" or "GIF image";
        var imageExifEditable = snapshot.DetectedFormat is "JPEG image" or "TIFF image";
        var isStl = snapshot.DetectedFormat is StlMetadataService.BinaryFormat or StlMetadataService.AsciiFormat;

        return
        [
            new(1, "File identity",
                $"Name: {snapshot.FileName}; extension: {(string.IsNullOrEmpty(snapshot.Extension) ? "none" : snapshot.Extension)}; path: {snapshot.FullPath}; size: {snapshot.SizeInBytes:N0} bytes; detected format: {snapshot.DetectedFormat}.",
                "Available",
                "Renaming within the same folder, times, and selected attributes are editable below; moving is not yet supported."),
            new(2, "Timestamps",
                $"Created: {snapshot.CreationTime:O}; last access: {snapshot.LastAccessTime:O}; last write: {snapshot.LastWriteTime:O}; filesystem-entry change time: not read.",
                "Available (change time not inspected)",
                "Creation, access, and last-write times are editable below; entry change time is not yet editable."),
            new(3, "Owner",
                "Owner name, owner SID, and group SID are not inspected in this version.",
                "Not inspected",
                "Not editable until Windows security-descriptor support is implemented."),
            new(4, "Permissions",
                "Access-control list (ACL), detailed read/write/execute rights, and inheritance are not inspected in this version.",
                "Not inspected",
                "Not editable until Windows security-descriptor support is implemented. Windows permissions are never bypassed."),
            new(5, "User identity",
                $"Current Windows account: {currentDomain}\\{currentUser}. Embedded author name, display name, and email are format-specific and not inspected.",
                "Current account available; embedded values not inspected",
                "Current Windows identity cannot be changed here; embedded values require a format provider."),
            new(6, "Computer identity",
                $"Computer: {computerName}; domain/workgroup: {currentDomain}.",
                "Available from Windows",
                "Values belong to the current computer and are not file metadata."),
            new(7, "Paths",
                $"Local path: {snapshot.FullPath}; containing folder: {directory}; UNC path: {uncPath}.",
                "Available",
                "Moving the file or changing its path is not yet supported."),
            isStl
                ? new(8, "Creator",
                    "STL stores creator, exporter, and model names only in the binary header or in the ASCII 'solid' name; both are listed in the STL metadata panel.",
                    "STL header inspected",
                    "Removed by 'Clean all metadata' in the STL metadata panel.")
                : new(8, "Creator",
                hasImageMetadataProvider
                    ? "Image EXIF/IPTC/XMP is listed in the image metadata panel when present; other file formats are not inspected."
                    : "Creating user, author, creator, and original source depend on embedded file metadata and are not inspected.",
                hasImageMetadataProvider ? "Image metadata inspected" : "Not inspected",
                imageExifEditable ? "Supported EXIF fields can be edited in the image metadata panel." : "Read-only here; editing requires a supported format provider."),
            new(9, "Last editor",
                hasImageMetadataProvider
                    ? "Image EXIF/IPTC/XMP is listed in the image metadata panel when present; other file formats are not inspected."
                    : "Last editor and previous editors depend on embedded file metadata and are not inspected.",
                hasImageMetadataProvider ? "Image metadata inspected" : "Not inspected",
                imageExifEditable ? "Supported EXIF fields can be edited in the image metadata panel." : "Read-only here; editing requires a supported format provider."),
            new(10, "Software",
                hasImageMetadataProvider
                    ? "Image EXIF/IPTC/XMP is listed in the image metadata panel when present; other file formats are not inspected."
                    : "Creating, editing, and export software/version values depend on embedded file metadata and are not inspected.",
                hasImageMetadataProvider ? "Image metadata inspected" : "Not inspected",
                imageExifEditable ? "Software EXIF field can be edited for JPEG/TIFF." : "Read-only here; editing requires a supported format provider."),
            new(11, "Description",
                hasImageMetadataProvider
                    ? "Image EXIF/IPTC/XMP is listed in the image metadata panel when present; other file formats are not inspected."
                    : "Title, subject, description, comments, keywords, and tags depend on embedded file metadata and are not inspected.",
                hasImageMetadataProvider ? "Image metadata inspected" : "Not inspected",
                imageExifEditable ? "Image Description EXIF field can be edited for JPEG/TIFF." : "Read-only here; editing requires a supported format provider."),
            new(12, "Copyright",
                hasImageMetadataProvider
                    ? "Image EXIF/IPTC/XMP is listed in the image metadata panel when present; other file formats are not inspected."
                    : "Copyright, rights holder, license, and contact details depend on embedded file metadata and are not inspected.",
                hasImageMetadataProvider ? "Image metadata inspected" : "Not inspected",
                imageExifEditable ? "Copyright EXIF field can be edited for JPEG/TIFF." : "Read-only here; editing requires a supported format provider."),
            isStl
                ? new(13, "Origin",
                    "The Zone.Identifier download marker is listed in the STL metadata panel; other origin data is not inspected.",
                    "Zone.Identifier inspected",
                    "The Zone.Identifier stream is removed by 'Clean all metadata'.")
                : new(13, "Origin",
                "Download URL, referrer, and origin markings (including Zone.Identifier) are not inspected.",
                "Not inspected",
                "Requires stream and format-specific metadata support."),
            new(14, "Filesystem extra data",
                "Alternate data streams (ADS), Zone.Identifier, and other named streams are not inspected.",
                "Not inspected",
                "Not editable until named-stream support is implemented."),
            new(15, "Identifiers",
                "NTFS file ID, object IDs, UUIDs, and document IDs are not inspected.",
                "Not inspected",
                "Filesystem identifiers are generally system-managed; document IDs require a format provider."),
            new(16, "Network",
                $"UNC/network path: {uncPath}. Embedded server names, share paths, addresses, and external links are not inspected.",
                snapshot.FullPath.StartsWith(@"\\", StringComparison.Ordinal) ? "UNC path available" : "No UNC path",
                "Embedded values require a format provider."),
            new(17, "Versions",
                "Version, revision, edit history, and version references depend on embedded file metadata and are not inspected.",
                "Not inspected",
                "Requires a format-specific metadata provider."),
            new(18, "Security information",
                $"File-system attributes: {snapshot.Attributes}; encryption and compression details, signatures, certificates, and integrity data are not inspected.",
                "Attributes available; other security data not inspected",
                "Read-only, hidden, and system attributes are editable below. Other values require platform or format-specific support."),
            new(19, "Embedded data",
                "Attachments, embedded objects, previews, and thumbnails are not inspected.",
                "Not inspected",
                "Requires a format-specific parser; embedded objects are not executed."),
            new(20, "Hidden content",
                "Hidden text, comments, notes, and concealed content inside the file are not inspected.",
                "Not inspected",
                "Requires a format-specific parser."),
            new(21, "Structure",
                $"Containing folder: {directory}; internal archive/document paths and directory structure are not inspected.",
                "Containing folder available; internal structure not inspected",
                "Requires format-specific support; folder contents are not modified."),
            new(22, "Technical properties",
                $"Attributes: {snapshot.Attributes}; sparse/compression state and other filesystem flags are not inspected.",
                "Attributes available; other flags not inspected",
                "Read-only, hidden, and system attributes are editable below; other flags are not yet supported."),
            new(23, "Database and application IDs",
                "Internal record references, object IDs, and application-specific identifiers are not inspected.",
                "Not inspected",
                "Requires a format- or application-specific provider."),
            new(24, "Protocol information",
                "Embedded processing timestamps, user data, origin, and processing history are not inspected.",
                "Not inspected",
                "Requires a format-specific metadata provider."),
            new(25, "Cache and sidecar files",
                sidecarDetails,
                "Checked for Thumbs.db and desktop.ini in the containing folder",
                "These are separate files and are not edited as metadata of the selected file.")
        ];
    }

    public void Save(FileMetadataSnapshot original, FileMetadataChanges changes)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(changes);
        ValidateFileName(changes.FileName);

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
        var directory = Path.GetDirectoryName(original.FullPath)
            ?? throw new IOException("The selected file has no parent directory.");
        var targetPath = Path.Combine(directory, changes.FileName);
        var isRename = !string.Equals(original.FullPath, targetPath, StringComparison.OrdinalIgnoreCase);

        if (isRename && File.Exists(targetPath))
        {
            throw new IOException($"A file named '{changes.FileName}' already exists in this folder.");
        }

        try
        {
            SetReadOnly(original.FullPath, originalAttributes, isReadOnly: false);
            if (isRename)
            {
                File.Move(original.FullPath, targetPath);
            }

            var attributes = File.GetAttributes(targetPath)
                & ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System);
            if (changes.IsReadOnly)
            {
                attributes |= FileAttributes.ReadOnly;
            }

            if (changes.IsHidden)
            {
                attributes |= FileAttributes.Hidden;
            }

            if (changes.IsSystem)
            {
                attributes |= FileAttributes.System;
            }

            File.SetAttributes(targetPath, attributes);
            File.SetCreationTime(targetPath, changes.CreationTime);
            File.SetLastAccessTime(targetPath, changes.LastAccessTime);
            File.SetLastWriteTime(targetPath, changes.LastWriteTime);
        }
        catch (Exception saveException) when (saveException is IOException
                                              or UnauthorizedAccessException
                                              or ArgumentException
                                              or NotSupportedException
                                              or System.Security.SecurityException)
        {
            try
            {
                var rollbackPath = File.Exists(targetPath) ? targetPath : original.FullPath;
                SetReadOnly(rollbackPath, File.GetAttributes(rollbackPath), isReadOnly: false);
                if (isRename && File.Exists(targetPath) && !File.Exists(original.FullPath))
                {
                    File.Move(targetPath, original.FullPath);
                    rollbackPath = original.FullPath;
                }

                File.SetCreationTime(rollbackPath, originalCreationTime);
                File.SetLastAccessTime(rollbackPath, originalLastAccessTime);
                File.SetLastWriteTime(rollbackPath, originalLastWriteTime);
                File.SetAttributes(rollbackPath, originalAttributes);
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

    private static void ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileName is "." or ".."
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Enter a valid file name without a folder path.", nameof(fileName));
        }
    }

    private static (string Format, string Details) DetectFormat(string path, string extension)
    {
        Span<byte> signature = stackalloc byte[SignatureLength];
        string? stlFormat;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var bytesRead = stream.ReadAtLeast(signature, SignatureLength, throwOnEndOfStream: false);
            signature = signature[..bytesRead];
            stream.Position = 0;
            stlFormat = StlMetadataService.DetectVariant(stream);
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

        if (stlFormat is not null)
        {
            return (stlFormat, stlFormat == StlMetadataService.BinaryFormat
                ? "Recognized from the file size matching the triangle count in the STL header."
                : "Recognized from the 'solid' keyword followed by STL facet data.");
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
