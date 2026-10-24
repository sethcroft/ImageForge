# Preview 0.2 validation and limitations

The Windows x64 self-contained Release build completed successfully in the Linux workspace. The core harness passed 35 checks covering the original servicing command/parser/XML behavior plus export selection/compression, refusing original-file overwrite, split sizing and split-set collisions, BIOS/UEFI ISO arguments, required media files, invalid output placement/labels, folder inventory, and rejecting ISO import into an existing destination before any process starts.

The public site's local scripts passed syntax checks and 15 DOM interaction/navigation checks. Chromium screenshot capture was attempted but the container's SUID sandbox helper is not configured; no browser layout screenshot was obtained. No browser sandbox was disabled to work around that environment limitation.

Not executed in this Linux workspace: the WPF GUI, Windows Storage ISO mount/copy/eject, ADK oscdimg, DISM edition export/splitting/servicing, ADK engine switching, or boot/install validation. The owner reported successfully testing preview 0.1, but did not supply a host/build/target matrix. That feedback does not establish preview 0.2 runtime results.

## Windows checks for this release

1. Resize the light workspace and verify toolbar, library, navigation, filtering, preset list and expanded log remain usable.
2. Add WIM/ESD sources and a folder. Verify source groups, edition selection and filters. Compare edition indices with DISM.
3. Export one edition to a new WIM and ESD. Verify the new index/name and that existing output paths are rejected. Repeat while the source is mounted and verify refusal.
4. Split a copied WIM into SWM files. Verify every part remains together, actual part sizes, and rejection of similarly named existing parts. Validate the split set using Microsoft deployment tools.
5. Import an unmounted Windows ISO into a new folder, verify all media files copied and image files are writable, and confirm the ISO was ejected. Refuse an already-mounted ISO and preserve partial output after an induced copy failure. Check cleanup diagnostics if eject fails.
6. Select an installed ADK oscdimg, create BIOS + UEFI media from the complete imported folder, and verify incomplete media or output inside the source folder is rejected. Boot/install in expendable BIOS and UEFI VMs.
7. Select a compatible ADK DISM, perform image reads and mount/servicing checks, and confirm the tool selection cannot change during a mount. Verify support files remain installed beside the selected ADK executable.
8. Save/reopen preset shortcuts and tool preferences. Load a preset and confirm it only fills the pending plan. Test missing files and malformed preset JSON.
9. Repeat commit/discard/error handling and mount reconnection checks from WINDOWS-VALIDATION.md. No operation should modify the running Windows installation.
