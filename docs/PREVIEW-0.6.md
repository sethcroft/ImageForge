# Preview 0.6 — image-library tree

The center image library now uses a native WPF TreeView instead of nested ListView groups and empty-state overlays. This addresses overlapping Read-only/Other presentation and follows the supplied folder/edition layout. No framework migration or servicing-engine change is required. A screenshot alone does not establish whether NTLite uses WinForms.

- Deployed controls remain in their own collapsible card. Only actual ImageForge workflows are shown; there are no fabricated recovery installations or sample source/preset entries.
- Read-only and Other remain distinct collapsible cards, including when empty. Empty and no-match messages are ordinary tree nodes, so they cannot cover column headers or real rows.
- Folders expand to actual source files, then genuine DISM edition entries. The first folder shows the common source directory's full path; intermediate directories have short names. Sources with identical filenames in different locations remain separate.
- Only selecting an edition populates the source/edition fields and enables Load. Selecting or double-clicking folders, source files, categories or empty messages does not mount anything. Double-clicking an actual edition retains the existing load workflow. Active mounts remain protected while browsing.
- Rows retain their original metadata/progress objects, so mounted state and real progress remain bound to the correct source edition.
- Search and category filtering rebuild the visible hierarchy while retaining ancestor paths. Search expands matching branches. Clearing search restores remembered expansion state; filtering does not delete any source. An explicit import/preset selection reveals its edition by clearing conflicting filters when necessary.
- Source and preset dates, descriptions, compact status and existing image tools continue to work. Unmounting synchronizes the visible edition selection before enabling another Load.
- Soft blue selection, lighter panels and blue command accents follow the mockup while retaining ImageForge's own branding and controls. Editing navigation still appears only after loading a Windows target.

Read-only currently describes packed ESD and split SWM sources that must be converted before editing. It is not an assertion of every file's access permissions. ISO import still copies media into a working directory; this release adds no persistent read-only ISO library mount.

## Verification

All 90 automated checks passed, including eleven new checks for category order, common folders, duplicate source filenames, numeric edition ordering, metadata identity, unique node keys, filtering, empty/no-match states, format classification and directory-boundary handling. The Windows x64 self-contained WPF Release cross-build succeeds. This Linux environment cannot run Windows WPF, so pixel appearance, DPI, focus/keyboard behavior and actual mount transitions still need Windows runtime review.

Windows checklist (not yet executed):

1. Start with no sources. Confirm Deployed, Read-only and Other are separate, correctly spaced cards and no empty text covers headers.
2. Add WIM/ESD/SWM images from one folder, nested folders and different drives. Confirm the actual directories/source filenames/editions, including two install.wim files from different directories.
3. Expand/collapse with mouse and keyboard. Select a category, folder or image-file node: Load must stay unavailable. Select an edition: Load must enable; double-clicking that edition should use the normal disposable-image mount workflow.
4. Search by name/path/format and change category filters. Confirm matching ancestor branches remain visible, no-match text is accurate, and clearing search restores previous expansion state.
5. Import a source or load a preset while a conflicting filter is active; its intended edition should become visible and selected. A preset still does not mount or apply anything automatically.
6. Mount a disposable edition, inspect progress/status, browse other nodes, then save/discard. Confirm no other edition is reported mounted and the next Load follows the current genuine edition selection. Existing ownership checks must remain in force.
7. Toggle descriptions, larger fonts and date formats in tool settings. Review minimum window size and 100%/125%/150%/200% scaling, horizontal/vertical scrolling and all sidebar pages.
