# MetaLens

MetaLens is a Windows desktop prototype for inspecting file-system information and safely editing selected file-system metadata.

## Current scope

- Open any file through the file picker or drag and drop.
- Display file name, path, extension, size, available timestamps, Windows file attributes, and a best-effort format guess based on the file signature.
- Preview and explicitly confirm changes to creation, last-access, and last-write times, plus the read-only attribute.
- Keep file contents unchanged. Embedded metadata such as EXIF, PDF properties, and Office document properties is not yet read or edited.
- Treat unrecognized file formats as unknown rather than inferring support from the extension.

Timestamp and attribute availability and behavior depend on the file system and the user's permissions. If a save fails, MetaLens attempts to restore the original metadata and reports when restoration also fails.

## Build and run

Requires the .NET 10 SDK and Windows Desktop Runtime.

```powershell
dotnet run --project src\MetaLens.Desktop
```

## Tests

```powershell
dotnet test tests\MetaLens.Core.Tests
```
