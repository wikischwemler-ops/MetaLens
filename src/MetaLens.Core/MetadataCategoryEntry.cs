namespace MetaLens.Core;

public sealed record MetadataCategoryEntry(
    int Number,
    string Category,
    string AvailableInformation,
    string InspectionStatus,
    string EditingStatus);
