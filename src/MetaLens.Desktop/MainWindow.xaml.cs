using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MetaLens.Core;

namespace MetaLens.Desktop;

public partial class MainWindow : Window
{
    private static readonly string[] LocalDateFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fffffff"
    ];

    private readonly FileMetadataService _metadataService = new();
    private readonly ImageMetadataService _imageMetadataService = new();
    private FileMetadataSnapshot? _currentFile;
    private ObservableCollection<ImageMetadataRow> _imageMetadataRows = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a file to inspect",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            OpenFile(dialog.FileName);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
        {
            OpenFile(paths[0]);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null)
        {
            return;
        }

        if (!TryParseLocalTime(CreationTimeInput.Text, out var creationTime)
            || !TryParseLocalTime(LastAccessTimeInput.Text, out var lastAccessTime)
            || !TryParseLocalTime(LastWriteTimeInput.Text, out var lastWriteTime))
        {
            SetStatus("Enter each time as yyyy-MM-dd HH:mm:ss.fffffff.");
            MessageBox.Show(
                this,
                "One or more times are invalid. Use the format yyyy-MM-dd HH:mm:ss.fffffff.",
                "Invalid time",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var changes = new FileMetadataChanges(
            FileNameInput.Text,
            creationTime,
            lastAccessTime,
            lastWriteTime,
            ReadOnlyInput.IsChecked == true,
            HiddenInput.IsChecked == true,
            SystemInput.IsChecked == true);
        var summary = BuildChangeSummary(_currentFile, changes);

        if (MessageBox.Show(
                this,
                $"Apply these file-system metadata changes?\n\n{summary}\n\nNo file contents will be changed.",
                "Review changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var path = Path.Combine(
                Path.GetDirectoryName(_currentFile.FullPath)!,
                changes.FileName);
            _metadataService.Save(_currentFile, changes);
            if (!OpenFile(path, showErrors: false))
            {
                SetStatus("Metadata was saved, but the file could not be reloaded for display.");
                return;
            }

            SetStatus("File-system metadata saved.");
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or System.Security.SecurityException)
        {
            SetStatus("Could not save file-system metadata.");
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSaveImageMetadataClick(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null)
        {
            return;
        }

        ImageMetadataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ImageMetadataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var changes = _imageMetadataRows.Where(row => row.IsChanged).ToArray();
        if (changes.Length == 0)
        {
            SetStatus("No image metadata values have changed.");
            return;
        }

        var summary = string.Join(
            Environment.NewLine,
            changes.Select(row => $"{row.Group} / {row.Name}: {row.OriginalValue} → {row.Value}"));
        if (MessageBox.Show(
                this,
                $"Apply these image metadata changes?\n\n{summary}\n\nThe image pixels will not be intentionally edited.",
                "Review image metadata",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var path = _currentFile.FullPath;
            _imageMetadataService.Save(_currentFile, changes);
            if (!OpenFile(path, showErrors: false))
            {
                SetStatus("Image metadata was saved, but the file could not be reloaded.");
                return;
            }

            SetStatus("Image metadata saved.");
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidOperationException
                                   or System.Security.SecurityException)
        {
            SetStatus("Could not save image metadata.");
            MessageBox.Show(this, ex.Message, "Image metadata save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool OpenFile(string path, bool showErrors = true)
    {
        try
        {
            var snapshot = _metadataService.Read(path);
            _currentFile = snapshot;
            ShowSnapshot(snapshot);
            SetStatus("File opened. No changes have been made.");
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or System.Security.SecurityException)
        {
            _currentFile = null;
            FileNameText.Text = "Could not open selected file";
            FilePathText.Text = path;
            InformationGrid.ItemsSource = null;
            CategoryGrid.ItemsSource = null;
            ImageMetadataPanel.Visibility = Visibility.Collapsed;
            ImageMetadataGrid.ItemsSource = null;
            SaveImageMetadataButton.IsEnabled = false;
            FormatText.Text = "Format unavailable";
            EmbeddedMetadataText.Text = "No file information could be read.";
            CreationTimeInput.IsEnabled = false;
            LastAccessTimeInput.IsEnabled = false;
            LastWriteTimeInput.IsEnabled = false;
            FileNameInput.IsEnabled = false;
            ReadOnlyInput.IsEnabled = false;
            HiddenInput.IsEnabled = false;
            SystemInput.IsEnabled = false;
            SaveButton.IsEnabled = false;
            SetStatus("Could not read the selected file.");
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "Could not open file", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }
    }

    private void ShowSnapshot(FileMetadataSnapshot snapshot)
    {
        FileNameText.Text = snapshot.FileName;
        FilePathText.Text = snapshot.FullPath;
        FormatText.Text = snapshot.DetectedFormat;
        EmbeddedMetadataText.Text =
            $"{snapshot.FormatDetectionDetails} See the image metadata panel for supported image formats.";

        InformationGrid.ItemsSource = new[]
        {
            new InformationRow("File name", snapshot.FileName, "File system"),
            new InformationRow("Full path", snapshot.FullPath, "File system"),
            new InformationRow("Extension", string.IsNullOrEmpty(snapshot.Extension) ? "None" : snapshot.Extension, "File system"),
            new InformationRow("Size", FormatSize(snapshot.SizeInBytes), "File system"),
            new InformationRow("Created", FormatTime(snapshot.CreationTime), "File system"),
            new InformationRow("Last accessed", FormatTime(snapshot.LastAccessTime), "File system"),
            new InformationRow("Last modified", FormatTime(snapshot.LastWriteTime), "File system"),
            new InformationRow("Attributes", FormatAttributes(snapshot.Attributes), "Windows")
        };
        CategoryGrid.ItemsSource = _metadataService.ReadCategoryInventory(snapshot);
        ShowImageMetadata(snapshot);

        CreationTimeInput.Text = FormatInputTime(snapshot.CreationTime);
        LastAccessTimeInput.Text = FormatInputTime(snapshot.LastAccessTime);
        LastWriteTimeInput.Text = FormatInputTime(snapshot.LastWriteTime);
        FileNameInput.Text = snapshot.FileName;
        ReadOnlyInput.IsChecked = snapshot.IsReadOnly;
        HiddenInput.IsChecked = snapshot.IsHidden;
        SystemInput.IsChecked = snapshot.IsSystem;
        FileNameInput.IsEnabled = true;
        CreationTimeInput.IsEnabled = true;
        LastAccessTimeInput.IsEnabled = true;
        LastWriteTimeInput.IsEnabled = true;
        ReadOnlyInput.IsEnabled = true;
        HiddenInput.IsEnabled = true;
        SystemInput.IsEnabled = true;
        SaveButton.IsEnabled = true;
    }

    private void ShowImageMetadata(FileMetadataSnapshot snapshot)
    {
        ImageMetadataGrid.ItemsSource = null;
        ImageMetadataPanel.Visibility = Visibility.Collapsed;
        SaveImageMetadataButton.IsEnabled = false;
        _imageMetadataRows = [];

        if (!_imageMetadataService.Supports(snapshot))
        {
            return;
        }

        ImageMetadataPanel.Visibility = Visibility.Visible;
        try
        {
            _imageMetadataRows = new ObservableCollection<ImageMetadataRow>(
                _imageMetadataService.Read(snapshot.FullPath));
            ImageMetadataGrid.ItemsSource = _imageMetadataRows;
            var editingSupported = _imageMetadataService.SupportsEditing(snapshot);
            ImageMetadataNoticeText.Text = editingSupported
                ? "EXIF, IPTC, and XMP entries are listed. JPEG and TIFF support editing selected EXIF, IPTC, and XMP fields; list values use '; ' as a separator. Fields without an enabled editor remain read-only."
                : "EXIF, IPTC, and XMP entries are listed when present. Embedded metadata editing is currently enabled only for JPEG and TIFF images.";
            SaveImageMetadataButton.IsEnabled = editingSupported;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or System.Security.SecurityException
                                   or InvalidOperationException)
        {
            ImageMetadataNoticeText.Text = $"Image metadata could not be read: {ex.Message}";
        }
    }

    private static bool TryParseLocalTime(string value, out DateTime time)
    {
        if (DateTime.TryParseExact(
                value,
                LocalDateFormats,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            time = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
            return true;
        }

        time = default;
        return false;
    }

    private static string BuildChangeSummary(FileMetadataSnapshot original, FileMetadataChanges changes)
    {
        var entries = new List<string>();
        if (!string.Equals(original.FileName, changes.FileName, StringComparison.Ordinal))
        {
            entries.Add($"File name: {original.FileName} → {changes.FileName}");
        }

        AddTimeChange(entries, "Created", original.CreationTime, changes.CreationTime);
        AddTimeChange(entries, "Last accessed", original.LastAccessTime, changes.LastAccessTime);
        AddTimeChange(entries, "Last modified", original.LastWriteTime, changes.LastWriteTime);

        AddAttributeChange(entries, "Read-only", original.IsReadOnly, changes.IsReadOnly);
        AddAttributeChange(entries, "Hidden", original.IsHidden, changes.IsHidden);
        AddAttributeChange(entries, "System", original.IsSystem, changes.IsSystem);

        return entries.Count == 0 ? "No values have changed." : string.Join(Environment.NewLine, entries);
    }

    private static void AddAttributeChange(List<string> entries, string label, bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            entries.Add($"{label}: {oldValue} → {newValue}");
        }
    }

    private static void AddTimeChange(List<string> entries, string label, DateTime oldValue, DateTime newValue)
    {
        if (oldValue != newValue)
        {
            entries.Add($"{label}: {FormatInputTime(oldValue)} → {FormatInputTime(newValue)}");
        }
    }

    private static string FormatSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)size;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{size:N0} {units[unit]}"
            : $"{value:N2} {units[unit]}";
    }

    private static string FormatAttributes(FileAttributes attributes)
    {
        var names = Enum.GetValues<FileAttributes>()
            .Where(attribute => attribute != 0 && attributes.HasFlag(attribute))
            .Select(attribute => attribute.ToString());

        return string.Join(", ", names);
    }

    private static string FormatTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    private static string FormatInputTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

    private void SetStatus(string message) => StatusText.Text = message;

    private sealed record InformationRow(string Name, string Value, string Source);
}
