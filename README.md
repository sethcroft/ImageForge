# ImageForge

A Windows desktop application for preparing offline Windows installation images, inspired by NTLite's image-servicing workflows. Built with C#/.NET 8 and WPF, using Microsoft's DISM executable. Preview version 0.2; this is an independent implementation with a smaller feature set.

## Preview 0.2: image and ISO workspace

The interface now uses a light toolbar, grouped image library, left navigation and a persistent presets panel. Presets remember their file locations between sessions; operations still run only when requested.

New tools in **Image tools**:

- **Add folder:** discover WIM/ESD sources up to four nested directory levels, with bounded traversal and explicit reporting of skipped paths or scan limits.
- **Import ISO:** select a local ISO and type a new output-folder name. The app mounts the ISO read-only, copies media into the new editable folder, then dismounts only its own ISO mount. Already mounted ISOs and existing output folders are refused. Inspect partial output after an error.
- **Export edition / convert:** export one selected edition to a new WIM (maximum compression) or ESD (recovery compression). The exported file contains that edition only, typically at index 1. Existing output files are never appended to or overwritten.
- **Split WIM:** write a new SWM set with a requested part size (default 3800 MB). Keep all parts together; a large indivisible resource can exceed the requested size, so verify actual sizes before copying to FAT32.
- **Create bootable ISO:** use a complete extracted Windows installation-media folder and Microsoft ADK `oscdimg.exe`. The app verifies BIOS/UEFI boot files, `setup.exe`, `sources/boot.wim` and an install image exist before running the tool. This does not certify the ISO's boot or installation behavior.
- **Servicing engine:** optionally select an installed ADK `dism.exe`; leave blank for the host DISM. The matching ADK support files must remain with it. Installing ADK alone does not change the selected engine.

Save/discard all mounts before exporting or splitting a source. ISO creation refuses any registered DISM mount whose source is under the media folder. Keep ISO output outside the input media tree.

[Public documentation and feature roadmap](https://imageforge-preview-docs.loftydeer260.chatgpt.site/) · [Windows compatibility](docs/COMPATIBILITY.md) · [Requested feature roadmap](docs/ROADMAP.md)

## Run on Windows

[Download the first Windows x64 preview](https://github.com/sethcroft/ImageForge/raw/refs/tags/v0.2.0-preview.1/downloads/v0.2.0-preview.1/ImageForge-win-x64.zip) · [Release notes](https://github.com/sethcroft/ImageForge/releases/tag/v0.2.0-preview.1)

Extract the Windows x64 package and open `ImageForge.exe`. Accept the Windows administrator prompt. The self-contained package includes the .NET runtime; keep all packaged files together. It uses the DISM version included with your Windows installation. Use a Windows host that supports servicing the target image version. The preview executable is unsigned.

1. Work on a copy of `install.wim` on a local writable drive. If you have an ISO, use **Import ISO** to create an editable media folder, then select `sources/install.wim`. For ESD, select its edition and use **Export edition** to create a WIM before mounting.
2. In **Image**, browse to the WIM, read editions, select an edition, and select a new or empty mount folder. Mount the image.
3. In **Integrate**, queue INF files, driver folders, and CAB/MSU update packages. Put update prerequisites before dependent packages. The app preserves your order; it does not resolve dependencies automatically.
4. In **Features**, refresh the mounted image's optional features, select entries, and queue enable/disable operations. Enabling includes parent features and uses `/LimitAccess`; features with missing payloads may require source media and will fail in this preview. Disabling retains the payload.
5. In **Apply**, review and reorder the queue, then apply. Successful steps leave the queue; processing stops at the first failure. Inspect the log after any error because DISM may have made partial changes.
6. **Save & unmount** commits the mounted image to its original WIM. **Discard & unmount** drops all unsaved mounted changes, including previously applied steps. Pending queued steps are preserved.

You can save a JSON preset containing the image path, edition index, mount directory, and pending operations. Loading a preset reads and verifies its source image but does not mount or apply anything. Presets do not include image files, drivers, packages, or previously completed steps.

The **Unattended** tab exports an amd64 answer file for computer name, locale, and time zone. Put `Autounattend.xml` at the root of installation media. This is a partial answer file; Windows Setup will still ask questions not covered by those settings. Language packs must already be available in the image. Validate the answer file with Windows System Image Manager against the actual target image before deploying.

## Recovery and logs

The application serializes its operations and allows one running instance per Windows account. It records mounts in `%LOCALAPPDATA%\ImageForge\mounts.json` and verifies source WIM, edition index, mount directory, and DISM status before servicing or unmounting. It does not claim mounts created by other applications.

If you close the app with an image mounted, it leaves the mount intact. Restart, enter the same mount directory, and choose **Reconnect owned mount**. If a mount is interrupted or DISM reports `Needs Remount`, preserve the journal and inspect `dism /Get-MountedImageInfo` and `%WINDIR%\Logs\DISM\dism.log`. Resolve DISM's actual state before retrying. Never delete mounted image contents manually. Save a preset before closing if you need to preserve the pending queue.

The activity panel can be exported to a text file. The application blocks normal closing while a servicing operation is running. It does not terminate DISM or automatically restart Windows.

## Scope

Implemented: WIM/ESD edition listing, WIM mounting, driver integration, update integration, optional-feature enable/disable, ordered apply queue, commit/discard, mount reconnection, JSON presets, partial unattended XML, activity logs, folder discovery, single-edition WIM/ESD export, WIM splitting, ISO import, ADK ISO creation, and ADK DISM selection.

Not implemented: NTLite's proprietary component-removal engine, AppX removal, dependency visualization, live OS servicing, registry tweaks, Windows download/update catalog integration, automatic package ordering, direct SWM editing/joining, VHD/VHDX editing, and boot/deployment verification. This is not a full NTLite replacement. No NTLite application code, artwork, or branding is bundled.

## Build

Install the .NET 8 SDK on the build machine, then run:

```powershell
./build.ps1
```

Or run directly, including when cross-compiling from Linux:

```sh
dotnet publish ImageForge.App/ImageForge.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
dotnet run --project ImageForge.Core.Tests
```

The project enables Windows targeting for cross-compilation. Core checks run on Linux and do not execute DISM. WPF display and actual servicing require Windows; use the [Windows smoke checklist](docs/WINDOWS-VALIDATION.md).

See [investigation notes](docs/INVESTIGATION.md) for exactly what REA inspected and what remains unknown.
