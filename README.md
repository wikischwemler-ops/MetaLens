# MetaLens

MetaLens is a Windows desktop prototype for inspecting file-system information and safely editing selected file-system metadata.

## Current scope

- Open any file through the file picker or drag and drop.
- Display file name, path, extension, size, available timestamps, Windows file attributes, and a best-effort format guess based on the file signature.
- Show an inventory of all 25 requested metadata categories, distinguishing values available from Windows from format-specific data that has not yet been inspected.
- Rename files within the same folder and edit creation, last-access, and last-write times, plus read-only, hidden, and system attributes.
- Find the editing controls immediately below the selected-file card, before the long metadata inventory.
- Read EXIF, IPTC, and XMP entries from supported images. On JPEG and TIFF images, edit selected EXIF, IPTC, and XMP descriptive fields. Multi-value fields use `; ` as a separator; other image metadata values are shown read-only.
- Embedded metadata in PDF and Office files is not yet supported.
- Treat unrecognized file formats as unknown rather than inferring support from the extension.
- Use the MetaLens icon in both the Windows executable and application window/taskbar.

Timestamp and attribute availability and behavior depend on the file system and the user's permissions. If a save fails, MetaLens attempts to restore the original metadata and reports when restoration also fails.
Image metadata changes are written to a temporary image, re-read for verification, and replace the original only after validation succeeds. ExifTool 13.59 is bundled for embedded metadata reading and writing. Operations use the current user's permissions and do not bypass Windows access controls.

Editable image fields are:

- EXIF: Image Description, Artist, Copyright, Software, Make, Model, Date/Time Original.
- IPTC: Object Name, Keywords, Date Created, Time Created, By-line, By-line Title, City, Province/State, Country, Credit, Source, Copyright Notice, Caption/Abstract, Headline, Special Instructions.
- XMP: Dublin Core Title, Description, Creator, Subject, Rights; Photoshop Headline, City, State, Country, Credit, Source, Copyright, Instructions, Date Created; XMP Rating, Label, Create Date, Modify Date, Metadata Date, Creator Tool; IPTC Core Creator email/URL, Location, Country Code, Intellectual Genre, Scene.

The editable indicator in the image-metadata table is authoritative; fields not listed as editable remain read-only.

Bundled ExifTool files retain their upstream notices in `tools\ExifTool`. ExifTool is Copyright 2003-2026 Phil Harvey and is distributed under the Perl Artistic License or GPL. Its corresponding source archive is included at `tools\Image-ExifTool-13.59.tar.gz`. The Windows package also includes components from Strawberry Perl and a CC0-licensed launcher by Oliver Betz; see the included `README.txt`, `exiftool_files\readme_windows.txt`, and license files for notices and terms.

## Build and run

Requires the .NET 10 SDK and Windows Desktop Runtime.

```powershell
dotnet run --project src\MetaLens.Desktop
```

Publish a self-contained, single-file .NET Windows x64 executable. ExifTool remains a bundled companion runtime: the published folder contains `Tools\ExifTool`, and the repository-root `MetaLens.exe` uses `tools\ExifTool`:

```powershell
dotnet publish src\MetaLens.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Regenerate the Windows icon from its vector drawing source with:

```powershell
dotnet run --project tools\MetaLens.IconGenerator -- src\MetaLens.Desktop\Assets\MetaLens.ico
```

## Tests

```powershell
dotnet test tests\MetaLens.Core.Tests
```
