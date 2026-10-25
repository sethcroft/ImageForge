# Preview 0.4 — desktop layout

This release focuses on matching the supplied reference's layout and navigation sequence while retaining ImageForge's own branding and independently implemented controls. It does not add NTLite's proprietary servicing functionality.

## Workspace

- Compact Image / Preset / Tools / Help menus and horizontal toolbar.
- Initially the left sidebar contains only Start → Image. Mounting, reconnecting a verified owned mount, or selecting a deployed Windows target reveals the editing sections.
- Editing groups follow the reference: Integrate, Remove, Configure, Finish. Image tools, compatibility, downloads and host refresh are reached through top menus instead of permanently occupying the sidebar.
- Center: Images with File / Directory / Download actions, source-category filter, search, Deployed controls and collapsible category → source → edition groups. ESD/SWM sources appear under Read-only; WIM/VHD/VHDX under Other. The labels describe ImageForge's workflows, not every possible filesystem read-only state.
- Source path, edition and mount-directory controls are kept in a collapsed panel below the library. Double-click an edition, or select it and choose Load. This still uses the real DISM mount workflow.
- Right: Presets with File import, Save, name search and last-change dates. Double-clicking an actual preset row loads the preset. Presets never apply changes automatically. The pane is shown on Image; editing pages use the remaining width.
- Drivers and Updates display real queue entries rather than introductory cards. Features and Apply retain their working operations. The toolbar follows the selected page, and a short footer reports status/progress without code or a UI log.
- Registry, Post-Setup, Components, Settings and Scheduled Tasks are selectable design pages **after loading a target**, explicitly marked Planned. Their unavailable actions are disabled; no sample system data or pretend functionality is supplied.
- New session requires saving/unmounting an active image first. Clearing a pending queue requires confirmation. Existing library sources and presets are retained.

## Validation performed

- Release, Windows x64, self-contained .NET 8 WPF cross-build succeeds on Linux.
- 67 automated checks passed: the previous 62 servicing/helper checks plus five navigation checks covering locked editing pages, unlocked loaded sessions, tools reachable without a mount, unloading and invalid page destinations.
- XAML markup compilation validates the new controls, named fields and handler wiring.
- This environment cannot run a Windows WPF desktop. Pixel appearance, keyboard/focus behavior, DPI scaling, actual mount transitions and Windows servicing still need Windows runtime review. No Windows screenshot validation is claimed.

## Windows review checklist (not yet executed)

1. Extract the complete ZIP, open ImageForge.exe and accept the administrator prompt. At startup confirm Start → Image is the only left navigation item; no editing sections should flash into view.
2. Add WIM and ESD sources. Expand category and source headers; confirm real editions, file size and last-change dates. Exercise category/name filters and horizontal scrolling at the minimum window size.
3. Load a disposable WIM edition. Confirm Integrate / Remove / Configure / Finish appear only after load succeeds, the loaded edition has its Mounted status, and the bottom bar shows completion without commands or logs.
4. Visit every page, confirm toolbar actions match their labels, and confirm Presets returns when Image is selected. Verify Planned pages contain no active modifying action.
5. Queue INF and CAB/MSU files on an appropriate test target. Driver and update queue tables must show only their respective operation types; Apply shows the combined ordered queue. Exercise enable/disable features with a disposable image.
6. Save/unmount or discard a disposable mount. Confirm editing sections disappear and Image becomes selected. Reconnect an owned mount and verify they reappear.
7. Load a preset while unmounted. Its queue is retained but the app stays on Image until a target is loaded. Test preset search/import/double-click and New session cancellation/confirmation.
8. Review at 100%, 125%, 150% and 200% display scaling and with the keyboard. The toolbar, library and presets must remain reachable. Existing servicing checks in WINDOWS-VALIDATION.md still apply.

The image operations and compatibility restrictions documented for preview 0.3 continue to apply.
