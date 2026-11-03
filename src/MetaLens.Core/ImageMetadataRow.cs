namespace MetaLens.Core;

public sealed class ImageMetadataRow
{
    public ImageMetadataRow(string group, string name, string value, string? editKey)
    {
        Group = group;
        Name = name;
        Value = value;
        OriginalValue = value;
        EditKey = editKey;
    }

    public string Group { get; }
    public string Name { get; }
    public string Value { get; set; }
    public string OriginalValue { get; }
    public string? EditKey { get; }
    public bool CanEdit => EditKey is not null;
    public bool IsChanged => CanEdit && !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
}
