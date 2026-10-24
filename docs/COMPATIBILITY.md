# Windows compatibility

Last reviewed: 24 October 2026.

ImageForge is a Windows desktop application. The **host** is the PC running ImageForge; the **target** is the offline Windows image being edited. Their compatibility requirements are different. A Windows release appearing in this document does not mean every servicing operation has been verified on it.

## What has been tested

| Evidence | Result | What it establishes |
| --- | --- | --- |
| Windows x64 cross-build | First preview compiled successfully on Linux | The application and its packaged dependencies build for Windows x64. |
| Core checks | First preview passed 21 parser, command-construction, and answer-file checks on Linux | These checks do not execute Windows image servicing. |
| User preview feedback | The project owner reported successfully testing the first preview | The exact Windows edition, version, build, target image, and operations were not recorded. This is a user smoke test, not a version-specific certification. |
| Reproducible Windows servicing matrix | Not yet recorded | No specific Windows build is currently labeled **ImageForge verified**. |

The test count above describes v0.1.0-preview.1. Later releases should report their own executed checks in their release notes.

## Status meanings

- **Verified:** a recorded ImageForge test names the host, target image, architecture, release, and operations that passed. No build-specific rows have reached this status yet.
- **Development target:** a Windows family the preview is being designed for. It still needs the Windows checks above before a verified claim.
- **Experimental:** plausible from the available runtime or DISM capability, but not a committed ImageForge support promise.
- **Not supported:** outside the current package or application workflow.

## Running ImageForge: host requirements

| Host | ImageForge status | Notes |
| --- | --- | --- |
| Windows 11, x64 desktop | Development target | Use an administrator account and a host DISM suitable for the target image. |
| Windows 10, x64 desktop | Experimental | Runtime lifecycle depends on the Windows edition. An older in-box DISM is not a promise of Windows 11 image support. |
| Windows Server with Desktop Experience, x64 | Experimental | The runtime's Server support does not establish ImageForge GUI or servicing validation. |
| Windows Server Core, Nano Server, or WinPE as the application host | Not supported | The current package is a WPF desktop application and has no supported headless host workflow. |
| Windows 7, 8, or 8.1 as the application host | Not supported | The current .NET 8 desktop package does not offer a legacy runtime build. |
| 32-bit Windows | Not supported | The distributed application is built for `win-x64`. |
| Windows on Arm | Not supported as a preview host | There is no native ImageForge Arm64 build. Possible x64 emulation is not verified servicing support. |
| Linux or macOS | Not supported as a servicing host | Cross-building or running core tests does not run the Windows application. |

The self-contained package includes its .NET runtime, so a separate .NET install is not required. It still depends on operating-system facilities. Microsoft's current .NET 8 table excludes Windows 7/8.1 and limits its listed Windows 10 desktop support to specific Enterprise/LTSC releases; shipping the runtime inside the ZIP does not extend that lifecycle. See [.NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows) and the [.NET 8 OS matrix](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md).

## Editing Windows images: version reference

These version/build families come from Microsoft's release information. **The ImageForge status column is our own development assessment, not a Microsoft certification.** Patch revisions change; use the linked Microsoft pages for the current revision and edition lifecycle.

| Target image family | Base build | ImageForge status |
| --- | --- | --- |
| Windows 11 26H2 | 26300 | Experimental; new release requires ImageForge and runtime validation. |
| Windows 11 26H1 | 28000 | Experimental; hardware-specific release, not a general feature upgrade from 24H2/25H2. No architecture-specific promise. |
| Windows 11 25H2 | 26200 | Development target; needs recorded servicing tests. |
| Windows 11 24H2 | 26100 | Development target; edition lifecycle and command applicability still matter. |
| Windows 11 23H2 | 22631 | Experimental legacy image; edition lifecycle varies. |
| Windows 11 22H2 / 21H2 | 22621 / 22000 | Experimental legacy images; no validation claim. |
| Windows 10 22H2 | 19045 | Development target for offline image workflows; the normal Windows support lifecycle has ended. |
| Windows 10 Enterprise / IoT LTSC 2021 | 19044 | Experimental; edition-specific lifecycle. |
| Windows 10 Enterprise / IoT LTSC 2019 | 17763 | Experimental; edition-specific lifecycle. |
| Windows 10 LTSB 2016 and earlier Windows 10 releases | 14393 and earlier | Experimental legacy images; no validation claim. |
| Windows Server 2025 / 2022 / 2019 / 2016 | — | Experimental target families; not yet verified by ImageForge. |
| Windows 8.1 / 8 / 7 and older Server images | — | Outside the current ImageForge target scope. Some DISM combinations can handle them, but that is not an ImageForge support promise. |

Microsoft's [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information) identifies the release/build families and the special deployment scope of 26H1. Its [Windows 10 release information](https://learn.microsoft.com/en-us/windows/release-health/release-information) records the end of normal support on 14 October 2025 and describes ESU and LTSC exceptions. Those lifecycle states are separate from whether a copied image can technically be opened by a servicing tool.

## DISM compatibility matters

Preview 0.2 uses the host DISM by default and adds an explicit **Image tools → Servicing engine** selector for an installed Windows ADK `dism.exe`. Installing ADK alone does not change the selected tool. Its matching support files must remain installed with the executable. Switching the selected engine is blocked while an image is mounted.

Microsoft requires a compatible DISM/host/target combination. A newer target cannot simply be assumed compatible with an older DISM, and available operations differ between target versions. Windows 10 can service Windows 11 images in Microsoft's platform table **when using the Windows 11 version of DISM**; that qualification still matters when selecting an engine in ImageForge. See [DISM supported platforms](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-supported-platforms?view=windows-11) and [DISM overview](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/what-is-dism?view=windows-11).

## Image formats and operation limits

Preview 0.2 implements:

| Format | Implemented operations | Limits |
| --- | --- | --- |
| WIM | Edition inspection, mounting, offline servicing, save/discard, single-edition export and splitting | Sources must be unmounted before export, splitting or ISO creation. |
| ESD | Edition inspection and export to WIM/ESD | Export to WIM before mounting/editing. Export retains the selected edition, not every edition. |
| ISO import | Read-only ISO mount, copy into a new editable media folder, owned ISO dismount | Windows Storage cmdlets are required. Existing destination folders and already-mounted ISOs are refused. Partial outputs are preserved on error. |
| ISO creation | BIOS + UEFI ISO from complete Windows setup media | Requires a separately installed ADK `oscdimg.exe`, boot files and setup payloads; boot/install results are unverified. |
| SWM | Create a split set from a WIM | Direct SWM editing/joining is not implemented. Individual resources can exceed the requested split size; verify actual sizes for FAT32. |
| VHD/VHDX / deployed Windows | Not implemented | No direct route in the preview. |

These operations are implemented and cross-built, but their new Windows runtime behavior remains unverified. They are not a certification of every supported Microsoft format combination.

Drivers must be INF packages; updates must be CAB/MSU packages applicable to the target. The user controls package order. Enabling an optional feature may fail when its payload or prerequisites are unavailable. The first preview exports answer files for `amd64` only; this does not certify x86 or Arm64 unattended deployment.

Live Windows editing is not part of the current servicing workflow. An offline Windows image can still be incompatible with a particular operation even when the file format is accepted.

## Recording a verified configuration

For each future compatibility claim, record the ImageForge release, host edition/version/build and architecture, DISM version, target edition/version/build and architecture, input format, operations exercised, commit/discard result, and deployment/boot result when tested. Use the [Windows validation checklist](WINDOWS-VALIDATION.md). Mark untested operations explicitly instead of promoting an entire Windows family from one successful launch.
