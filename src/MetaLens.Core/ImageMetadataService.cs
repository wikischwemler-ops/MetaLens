using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MetadataExtractor;

namespace MetaLens.Core;

public sealed class ImageMetadataService
{
    private const long MaximumEditableImageSize = 512L * 1024 * 1024;
    private const int MaximumMetadataRows = 2_000;
    private const string ExifToolRelativePath = @"Tools\ExifTool\exiftool.exe";

    private static readonly string[] EditableMetadataKeys =
    [
        "EXIF:ImageDescription",
        "EXIF:Artist",
        "EXIF:Copyright",
        "EXIF:Software",
        "EXIF:Make",
        "EXIF:Model",
        "EXIF:DateTimeOriginal",
        "IPTC:ObjectName",
        "IPTC:Keywords",
        "IPTC:DateCreated",
        "IPTC:TimeCreated",
        "IPTC:By-line",
        "IPTC:By-lineTitle",
        "IPTC:City",
        "IPTC:Province-State",
        "IPTC:Country-PrimaryLocationName",
        "IPTC:Credit",
        "IPTC:Source",
        "IPTC:CopyrightNotice",
        "IPTC:Caption-Abstract",
        "IPTC:Headline",
        "IPTC:SpecialInstructions",
        "XMP-dc:Title",
        "XMP-dc:Description",
        "XMP-dc:Creator",
        "XMP-dc:Subject",
        "XMP-dc:Rights",
        "XMP-photoshop:Headline",
        "XMP-photoshop:City",
        "XMP-photoshop:State",
        "XMP-photoshop:Country",
        "XMP-photoshop:Credit",
        "XMP-photoshop:Source",
        "XMP-photoshop:Copyright",
        "XMP-photoshop:Instructions",
        "XMP-photoshop:DateCreated",
        "XMP-xmp:Rating",
        "XMP-xmp:Label",
        "XMP-xmp:CreateDate",
        "XMP-xmp:ModifyDate",
        "XMP-xmp:MetadataDate",
        "XMP-xmp:CreatorTool",
        "XMP-iptcCore:CreatorWorkEmail",
        "XMP-iptcCore:CreatorWorkURL",
        "XMP-iptcCore:Location",
        "XMP-iptcCore:CountryCode",
        "XMP-iptcCore:IntellectualGenre",
        "XMP-iptcCore:Scene"
    ];

    private static readonly HashSet<string> EditableMetadataKeySet =
        new(EditableMetadataKeys, StringComparer.Ordinal);

    public bool Supports(FileMetadataSnapshot snapshot) =>
        snapshot.DetectedFormat is "JPEG image" or "PNG image" or "TIFF image" or "GIF image";

    public bool SupportsEditing(FileMetadataSnapshot snapshot) =>
        snapshot.DetectedFormat is "JPEG image" or "TIFF image";

    public IReadOnlyList<ImageMetadataRow> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists)
        {
            throw new FileNotFoundException("The image no longer exists.", path);
        }

        if (info.Length > MaximumEditableImageSize)
        {
            throw new IOException($"Image metadata inspection is limited to files up to {MaximumEditableImageSize / (1024 * 1024)} MB.");
        }

        var canEdit = Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".tif" or ".tiff";
        var metadataRows = new List<ImageMetadataRow>();
        try
        {
            foreach (var directory in ImageMetadataReader.ReadMetadata(path))
            {
                if (directory.Name.Contains("IPTC", StringComparison.OrdinalIgnoreCase)
                    || directory.Name.Contains("XMP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var tag in directory.Tags)
                {
                    if (metadataRows.Count >= MaximumMetadataRows)
                    {
                        return AddDisplayLimitRow(metadataRows);
                    }

                    metadataRows.Add(new ImageMetadataRow(
                        directory.Name,
                        tag.Name,
                        tag.Description ?? string.Empty,
                        canEdit ? GetEditableKey(directory.Name, tag.Name) : null));
                }
            }
        }
        catch (ImageProcessingException ex)
        {
            throw new InvalidDataException("The image metadata is malformed or unsupported.", ex);
        }

        foreach (var row in ReadIptcAndXmp(path))
        {
            if (metadataRows.Count >= MaximumMetadataRows)
            {
                return AddDisplayLimitRow(metadataRows);
            }

            metadataRows.Add(row);
        }

        if (canEdit)
        {
            AddEditablePlaceholders(metadataRows);
        }

        return metadataRows;
    }

    public void Save(FileMetadataSnapshot original, IReadOnlyCollection<ImageMetadataRow> rows)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(rows);

        var fullPath = Path.GetFullPath(original.FullPath);
        if (!SupportsEditing(original))
        {
            throw new NotSupportedException("Embedded metadata editing is currently supported for JPEG and TIFF images only.");
        }

        if (Path.GetExtension(fullPath).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".tif" or ".tiff"))
        {
            throw new NotSupportedException("Embedded metadata editing is currently supported for JPEG and TIFF images only.");
        }

        var info = new FileInfo(fullPath);
        info.Refresh();
        if (!info.Exists)
        {
            throw new FileNotFoundException("The image no longer exists.", fullPath);
        }

        if (info.Length != original.SizeInBytes || info.LastWriteTimeUtc != original.LastWriteTimeUtc)
        {
            throw new IOException("The image has changed since it was opened. Reopen it before saving metadata.");
        }

        if (info.Length > MaximumEditableImageSize)
        {
            throw new IOException($"Image editing is limited to files up to {MaximumEditableImageSize / (1024 * 1024)} MB.");
        }

        var changedRows = rows.Where(row => row.IsChanged).ToArray();
        if (changedRows.Length == 0)
        {
            return;
        }

        var unsupportedEdit = changedRows.FirstOrDefault(row =>
            row.EditKey is null || !EditableMetadataKeySet.Contains(row.EditKey));
        if (unsupportedEdit is not null)
        {
            throw new InvalidOperationException($"The '{unsupportedEdit.Name}' metadata field is read-only.");
        }

        foreach (var row in changedRows)
        {
            ValidateValue(row);
        }

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("The image has no parent directory.");
        var extension = Path.GetExtension(fullPath);
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}{extension}");

        try
        {
            File.Copy(fullPath, temporaryPath);
            var arguments = new List<string>
            {
                "-overwrite_original",
                "-P",
                "-sep",
                "; "
            };
            arguments.AddRange(changedRows.Select(row => $"-{row.EditKey}={row.Value}"));
            arguments.Add(temporaryPath);
            RunExifTool(arguments);

            var validationRows = Read(temporaryPath);
            foreach (var editedRow in changedRows)
            {
                var verified = validationRows.FirstOrDefault(row =>
                    string.Equals(row.EditKey, editedRow.EditKey, StringComparison.Ordinal));
                var verifiedValue = verified?.Value ?? string.Empty;
                if (!ValuesMatch(editedRow, verifiedValue))
                {
                    throw new IOException($"Could not verify the saved '{editedRow.Name}' metadata value.");
                }
            }

            info.Refresh();
            if (!info.Exists || info.Length != original.SizeInBytes || info.LastWriteTimeUtc != original.LastWriteTimeUtc)
            {
                throw new IOException("The original image changed while metadata was being saved. The edited copy was not applied.");
            }

            File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: false);
        }
        catch (Exception saveException) when (saveException is IOException
                                              or UnauthorizedAccessException
                                              or ArgumentException
                                              or NotSupportedException
                                              or System.Security.SecurityException
                                              or InvalidOperationException)
        {
            throw new IOException("Image metadata could not be saved. The original image was retained.", saveException);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IReadOnlyList<ImageMetadataRow> ReadIptcAndXmp(string path)
    {
        var output = RunExifTool(
        [
            "-j",
            "-G1",
            "-s",
            "-a",
            "-n",
            "-charset",
            "filename=UTF8",
            "-IPTC:all",
            "-XMP:all",
            path
        ]);

        try
        {
            using var document = JsonDocument.Parse(output);
            var rows = new List<ImageMetadataRow>();
            foreach (var property in document.RootElement[0].EnumerateObject())
            {
                var separator = property.Name.IndexOf(':');
                if (separator <= 0 || separator == property.Name.Length - 1)
                {
                    continue;
                }

                var group = property.Name[..separator];
                if (!group.StartsWith("IPTC", StringComparison.Ordinal)
                    && !group.StartsWith("XMP", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = property.Name[(separator + 1)..];
                rows.Add(new ImageMetadataRow(
                    group,
                    name,
                    FormatJsonValue(property.Value),
                    EditableMetadataKeySet.Contains(property.Name) ? property.Name : null));
            }

            return rows;
        }
        catch (JsonException ex)
        {
            throw new IOException("ExifTool returned invalid image metadata. The image was not changed.", ex);
        }
    }

    private static string RunExifTool(IEnumerable<string> arguments)
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, ExifToolRelativePath);
        if (!File.Exists(executablePath))
        {
            throw new IOException($"The bundled ExifTool executable was not found at '{executablePath}'.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new IOException("ExifTool could not be started.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(standardOutput, standardError);

            if (process.ExitCode != 0)
            {
                var error = standardError.Result.Trim();
                throw new IOException(string.IsNullOrEmpty(error)
                    ? $"ExifTool failed with exit code {process.ExitCode}."
                    : $"ExifTool failed: {error}");
            }

            return standardOutput.Result;
        }
        catch (Win32Exception ex)
        {
            throw new IOException("The bundled ExifTool executable could not be started.", ex);
        }
    }

    private static string FormatJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Array => string.Join("; ", value.EnumerateArray().Select(FormatJsonScalar)),
            JsonValueKind.Object => JsonSerializer.Serialize(value),
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.ToString()
        };

    private static string FormatJsonScalar(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

    private static string? GetEditableKey(string directory, string name)
    {
        if (directory.StartsWith("Exif IFD0", StringComparison.Ordinal))
        {
            var key = name switch
            {
                "Image Description" => "EXIF:ImageDescription",
                "Artist" => "EXIF:Artist",
                "Copyright" => "EXIF:Copyright",
                "Software" => "EXIF:Software",
                "Make" => "EXIF:Make",
                "Model" => "EXIF:Model",
                _ => null
            };
            return key;
        }

        return directory.StartsWith("Exif SubIFD", StringComparison.Ordinal)
            && name == "Date/Time Original"
            ? "EXIF:DateTimeOriginal"
            : null;
    }

    private static void AddEditablePlaceholders(ICollection<ImageMetadataRow> rows)
    {
        foreach (var key in EditableMetadataKeys)
        {
            if (rows.Any(row => string.Equals(row.EditKey, key, StringComparison.Ordinal)))
            {
                continue;
            }

            var separator = key.IndexOf(':');
            var group = key[..separator];
            var name = key[(separator + 1)..];
            rows.Add(new ImageMetadataRow(group, name, string.Empty, key));
        }
    }

    private static IReadOnlyList<ImageMetadataRow> AddDisplayLimitRow(List<ImageMetadataRow> rows)
    {
        rows.Add(new ImageMetadataRow(
            "MetaLens",
            "Display limit",
            $"Metadata display stopped after {MaximumMetadataRows:N0} entries.",
            null));
        return rows;
    }

    private static void ValidateValue(ImageMetadataRow row)
    {
        if (row.EditKey == "EXIF:DateTimeOriginal"
            && !DateTime.TryParseExact(
                row.Value,
                "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new ArgumentException("Date/Time Original must use yyyy:MM:dd HH:mm:ss.");
        }
    }

    private static bool ValuesMatch(ImageMetadataRow expected, string actual)
    {
        if (expected.EditKey == "EXIF:DateTimeOriginal"
            && DateTime.TryParseExact(expected.Value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var expectedDate)
            && DateTime.TryParseExact(actual, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var actualDate))
        {
            return expectedDate == actualDate;
        }

        return string.Equals(expected.Value, actual, StringComparison.Ordinal);
    }
}
