# ImageForge

A Windows desktop application for preparing offline Windows installation images, inspired by NTLite's image-servicing workflows. Built with C#/.NET 8 and WPF, using Microsoft's DISM executable. Preview version 0.6; this is an independent implementation with a smaller feature set.

## Preview 0.6: image-library tree

The center library now uses a real WPF tree with separate **Deployed / Read-only / Other** cards. Folders expand to actual source files and their genuine editions; no empty-state panels overlay the table. Only an edition can be loaded. Search retains parent folders, expands matches and restores expansion state when cleared. Status and progress stay bound to the actual edition. Soft blue selection, panels and command accents follow the supplied ImageForge mockup. The existing WPF framework and servicing workflows are retained.

[Tree changes and Windows review checklist](docs/PREVIEW-0.6.md).

## Application menu and tool settings from preview 0.5

The hamburger menu before **Image** now opens **New instance / Settings / Check for updates / Manage license / Export HW list / Send logs / About / Exit**, matching the requested menu order.

Settings uses searchable expandable sections and toggle switches. Larger fonts, the info bar, description columns and compact dates work and persist; DISM selection uses the existing engine preference. Unsupported options are disabled and explained. Release checks show stable/preview versions from GitHub and offer manual release links; optional daily startup checking is off by default. There is no automatic installer. Manage license displays ImageForge's actual MIT terms with no activation requirement. Hardware export creates a local ImageForge JSON report. Send logs prepares a local ZIP containing the session log and only presets you explicitly select; nothing is uploaded or emailed.

New instance remains disabled because the existing per-account single-instance rule protects the mount journal. About and Exit use the real program information and existing closing safeguards. [Details and Windows dialog-review checklist](docs/PREVIEW-0.5.md).

## Desktop design from preview 0.4

The workspace now follows the reference layout: a compact Image / Preset / Tools menu and contextual toolbar, **Start → Image** on the left, an image library in the center, and a searchable **Presets → File** panel on the right. Editing navigation is hidden until an image is mounted/reconnected or a deployed Windows target is selected. Saving/unmounting returns to Image and hides those sections.

Loaded sessions show **Integrate** (Updates, Drivers, Registry, Post-Setup), **Remove** (Components), **Configure** (Features, Settings, Scheduled Tasks, Unattended), and **Finish** (Apply). Registry, Post-Setup, Components, Settings and Scheduled Tasks are explicitly **Planned** design pages, with no pretend data or actions. Drivers and Updates show their real pending queues. The top commands change with the selected page; presets are displayed on the Image page.

The library separates Deployed Windows controls, Read-only ESD/SWM sources and Other image sources. File sources expand to their actual editions. ISO import still copies media into a working directory; this release does not introduce a persistent read-only ISO mount. Source and mount-directory controls live in the collapsible **Selected image · Source and mount options** panel. Double-click an edition to load it. A new session preserves the source library, but clearing a pending queue requires confirmation; loaded images must be saved/unmounted first.

[Design release notes and Windows review checklist](docs/PREVIEW-0.4.md). ImageForge uses its own branding and UI elements; it is an independent implementation, not a complete NTLite replacement.

## Existing workflows from preview 0.3

The desktop has **no code/command output or log panel**. The bottom bar shows short loading/saving/downloading stages and genuine progress when the underlying operation provides it; otherwise it uses an indeterminate indicator. Active image rows show the current edition's progress. Diagnostics go to a per-session file in `%LOCALAPPDATA%\ImageForge\Diagnostics`, accessible from Help.

New workflows:

- **Download Windows:** open Microsoft's official Windows 11/10 page, copy its direct ISO/ESD URL and exact SHA-256 hash, then download and verify inside ImageForge. Known Microsoft HTTPS delivery hosts are enforced across redirects. Downloads can be cancelled; failed, cancelled, invalid-format and hash-mismatched files are not published. This is a direct-link workflow, not an automatic version catalogue.
- **Load ESD/SWM:** a selected edition is automatically exported to a new writable WIM cache before mounting. Select the first SWM part and keep every matching part together.
- **Export / join / convert:** export selected or all editions to WIM/ESD. Complete SWM sets can be rejoined through export. Export stages into a new private partial file and only moves it to the requested output after completion. Failed image-export partials remain for diagnosis; original inputs are not replaced.
- **Convert image to ISO:** select an install image plus matching original Windows setup media as a template. ImageForge copies boot/setup files into a new working folder, exports the selected/all image editions as install.wim, then runs ADK oscdimg. An install ESD alone does not contain the boot/setup files needed for a bootable ISO.
- **Edition management:** permanently remove unwanted editions from an unmounted WIM, keeping at least one; work on a copy. Export afterward to reclaim unreferenced WIM resources. For a loaded offline target, read DISM's eligible higher editions and queue an upgrade. Conversion does not activate Windows or grant a license.
- **VHD/VHDX:** inspect and mount compatible Windows virtual-disk images through DISM with index 1, using the existing ownership, save/discard and reconnection lifecycle. WIM-only integrity options are not passed for virtual disks.
- **Deployed Windows:** select This PC (live) or the root of a different local Windows drive. Running Windows supports applicable packages and optional features; online MSU support requires Windows 11+. Offline drivers and eligible offline edition upgrades are available on offline targets. Drive identities are checked again before servicing. Changes to deployed/live installations are immediate; there is no discard. A mounted VHD volume needs an assigned drive letter for this direct-volume route.
- **Host refresh:** choose original, matching Microsoft media and run Windows Setup's compatibility scan. Only a completed compatibility pass enables the interactive Setup launch. Microsoft signatures are checked; the setup-file hash and install-image size/timestamp are rechecked to detect media changes after the scan. The wizard does not pass quiet, license-acceptance, clean-install or compatibility-bypass flags. Windows Setup decides whether applications, settings and personal files can be preserved and controls any restarts. Do not use customized ImageForge media for this host-refresh route.

Presets continue to describe image-file projects. Registry tweaks, proprietary component removal and full unattended host refresh are not included.

[Public documentation and feature roadmap](https://imageforge-preview-docs.loftydeer260.chatgpt.site/) · [Windows compatibility](docs/COMPATIBILITY.md) · [Requested feature roadmap](docs/ROADMAP.md)

## Run on Windows

[Download Windows x64 preview 0.6](https://github.com/sethcroft/ImageForge/raw/refs/tags/v0.6.0-preview.1/downloads/v0.6.0-preview.1/ImageForge-win-x64.zip) · [Release notes](https://github.com/sethcroft/ImageForge/releases/tag/v0.6.0-preview.1)

Extract the Windows x64 package and open `ImageForge.exe`. Accept the Windows administrator prompt. The self-contained package includes the .NET runtime; keep all packaged files together. It uses the host DISM by default; an installed ADK DISM can be selected in Image tools. Use a Windows host that supports servicing the target image version. The preview executable is unsigned.

1. Work on a copy of `install.wim` on a local writable drive. If you have an ISO, use **Import ISO** to create an editable media folder, then select `sources/install.wim`. For ESD/SWM, Load automatically prepares the selected edition in a writable WIM cache.
2. In **Image**, browse to the WIM, read editions, select an edition, and select a new or empty mount folder. Mount the image.
3. In **Drivers** and **Updates**, queue INF files, driver folders, and CAB/MSU packages. Put update prerequisites before dependent packages. The app preserves your order; it does not resolve dependencies automatically.
4. In **Features**, refresh the mounted image's optional features, select entries, and queue enable/disable operations. Enabling includes parent features and uses `/LimitAccess`; features with missing payloads may require source media and will fail in this preview. Disabling retains the payload.
5. In **Apply**, review and reorder the queue, then apply. Successful steps leave the queue; processing stops at the first failure. Inspect the diagnostics file after any error because DISM may have made partial changes.
6. **Save & unmount** commits the mounted image to its original WIM. **Discard & unmount** drops all unsaved mounted changes, including previously applied steps. Pending queued steps are preserved.

You can save a JSON preset containing the image path, edition index, mount directory, and pending operations. Loading a preset reads and verifies its source image but does not mount or apply anything. Presets do not include image files, drivers, packages, or previously completed steps.

The **Unattended** tab exports an amd64 answer file for computer name, locale, and time zone. Put `Autounattend.xml` at the root of installation media. This is a partial answer file; Windows Setup will still ask questions not covered by those settings. Language packs must already be available in the image. Validate the answer file with Windows System Image Manager against the actual target image before deploying.

## Recovery and logs

The application serializes its operations and allows one running instance per Windows account. It records mounts in `%LOCALAPPDATA%\ImageForge\mounts.json` and verifies source image file, edition index, mount directory, and DISM status before servicing or unmounting. It does not claim mounts created by other applications.

If you close the app with an image mounted, it leaves the mount intact. Restart, enter the same mount directory, and choose **Reconnect owned mount**. If a mount is interrupted or DISM reports `Needs Remount`, preserve the journal and inspect `dism /Get-MountedImageInfo` and `%WINDIR%\Logs\DISM\dism.log`. Resolve DISM's actual state before retrying. Never delete mounted image contents manually. Save a preset before closing if you need to preserve the pending queue.

Diagnostics are saved to files; there is no on-screen activity log or command output. Open the diagnostics directory from Help when troubleshooting. The application blocks normal closing while a servicing operation is running. It does not terminate DISM or automatically restart Windows.

## Scope

Implemented: WIM/ESD edition listing, WIM mounting, driver integration, update integration, optional-feature enable/disable, ordered apply queue, commit/discard, mount reconnection, JSON presets, partial unattended XML, file-only diagnostics, progress reporting, verified Microsoft downloads, folder discovery, selected/all-edition export, SWM splitting/joining, edition management, ISO import/conversion, VHD/VHDX mounts, deployed/live target controls and guided interactive Windows Setup.

Not implemented: NTLite's proprietary component-removal engine, AppX removal, dependency visualization, arbitrary registry tweaks, Windows download/update catalog integration, automatic package ordering, direct in-place SWM editing, native disk partitioning, and boot/deployment verification. This is not a full NTLite replacement. No NTLite application code, artwork, or branding is bundled.

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
