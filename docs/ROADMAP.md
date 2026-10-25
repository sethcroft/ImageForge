# Feature roadmap

Based on the reference images supplied for ImageForge. The next-preview priority selected by the project owner was **image management and ISO tools**. This document distinguishes shipped functionality from later work; it does not promise parity with NTLite.

## Preview 0.2: implemented priority

- Light desktop workspace with top menus and toolbar, grouped/searchable image editions, left navigation and persistent preset shortcuts.
- WIM/ESD folder discovery, WIM mounting, save/discard and mount reconnection.
- Single-edition WIM/ESD export and conversion, WIM-to-SWM splitting.
- Local ISO import to a new editable media directory.
- BIOS + UEFI ISO creation using a separately supplied Windows ADK oscdimg.exe.
- Optional ADK DISM selection; host DISM remains the default.
- Existing driver/update integration, optional-feature controls, ordered apply queue, partial amd64 answer-file generation and presets.
- Public website with feature statuses, host/target/format compatibility matrix and downloads.

These features are implemented and cross-built. The new Windows runtime operations and updated WPF layout still require the recorded Windows checks in WINDOWS-VALIDATION.md.

## Later: integrate

Update Catalog downloads, prerequisite sorting, extraction/caching performance, Secure Boot readiness inspection, component-store cleanup, hardware inventory/driver targeting, offline registry settings, power-plan import, post-setup additions and a live-update wizard.

## Later: remove and configure

Dependency-aware component/AppX removal, tracking components restored by updates, scheduled task controls, Windows policy/settings presets and offline service configuration. Current feature disable retains payload; it is not a component-removal engine.

## Later: automate and expand image routes

Full unattended setup, account and disk-layout configuration, post-setup execution, multi-edition batch tasks, a documented CLI, deployed/live Windows editing, VHD/VHDX routes, SWM joining and a host-refresh wizard.

The public feature catalog labels these entries **Planned** or **Partial**, rather than adding inactive controls to the desktop app.

## Preview 0.3: expanded image and installed-Windows routes

Implemented: compact status/progress UI with file-only diagnostics, SHA-256 verified Microsoft direct-link downloads, automatic ESD/SWM cache preparation, selected/all-edition export, complete SWM-set rejoining, WIM edition deletion, eligible offline edition upgrades, setup-template-based ISO conversion, VHD/VHDX DISM mounts, and explicit deployed/live targets. Host refresh now has compatibility scanning and an interactive Windows Setup hand-off. These new Windows routes are unverified at runtime; see PREVIEW-0.3.md.

Still not included: an automatic Microsoft version catalogue, registry/policy tweaking, proprietary component removal, full unattended host refresh, disk partitioning, or a guarantee that applications/data can be preserved on every upgrade path. Earlier roadmap entries describe the state before preview 0.3.
