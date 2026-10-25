# Preview 0.5 — application menu and tool dialogs

The hamburger button immediately before Image now opens an application menu in the supplied order: New instance, Settings, Check for updates, Manage license, Export HW list, Send logs, About, and Exit. It uses independently implemented controls and ImageForge branding.

## Available actions

- **Settings:** searchable, expandable Appearance, Lists, Presets, Performance, Storage, DISM and Network sections, with switch-style toggles and OK/Cancel. Larger fonts, the top info bar, image-description columns and compact file/preset dates work and persist in `%LOCALAPPDATA%\ImageForge\tool-settings.json`. The compatible ADK DISM selector uses the existing workspace preference and cannot change during an image mount. Changes take effect on OK; Cancel does not apply them.
- **Storage:** displays ImageForge's actual per-user data/diagnostics/edit-cache directories and available space. Folder buttons open those locations. Paths are fixed in this release; there is no cache-erasure action. Download destinations remain chosen per download.
- **Check for updates:** reads public GitHub release metadata, separates stable and preview releases, shows the installed informational version, and opens the corresponding ImageForge release page. Checks use semantic numeric version ordering, exclude drafts/inconsistent tags and time out. GitHub failures/rate limits are reported as unavailable, not successful checks. Optional daily checks on startup are off by default and remember the last successful check. No hardware inventory, logs or presets are transmitted in release checks. Host updates open Windows Settings; host update availability is not scanned by ImageForge.
- **Manage license:** shows the actual embedded repository MIT license and copyright notice. ImageForge has no activation key, paid subscription, expiry or remote activation. Windows and Microsoft tool licensing remains separate.
- **Export HW list:** uses a fixed local Windows CIM query to save a new ImageForge JSON report containing device names, classes, manufacturers, status and configuration error codes. Explicit device IDs/serial-number fields are not requested. Review the report before sharing. This is not NTLite's proprietary checklist format and no checklist import is implemented.
- **Send logs:** creates a new local support ZIP containing this session's diagnostic log, a version/OS summary and only the optional JSON presets explicitly chosen in the dialog. Nothing is uploaded or emailed. Logs/presets can contain local paths or personal data; review before sharing. No memory dumps, hardware list, credentials or mount journal are automatically included. Safe archive entry names, per-file/count limits and temporary-file cleanup protect the export; existing outputs are not overwritten.
- **About:** program version, runtime, project link, affiliation note and license access.
- **Exit:** uses the existing busy/mount/queued-change closing checks.

## Clearly unavailable controls

New instance stays disabled with an explanation: the existing per-account single-instance rule protects the mount journal. Use New session to clear the current workspace after saving/unmounting.

Additional themes/languages, hover actions, automatic session presets, preset history/backups, boot-image update propagation, component-removal levels, manual thread selection, storage relocation, custom proxy credentials, debug-level selection and automatic application installation are not implemented. Their controls are disabled with explanations. The integrity and offline feature-servicing policies are retained rather than exposed as working switches.

## Verification

The Windows x64 self-contained WPF Release cross-build succeeded. All 79 automated checks passed, including release selection, numeric preview ordering, URL/tag rejection, support ZIP contents, output preservation, size limits and partial-file cleanup. XAML names/events compile successfully. The environment is Linux: these dialogs, CIM hardware collection, browser/Settings launches, keyboard/focus behavior and DPI layout have not been executed on Windows.

Windows review checklist (not yet executed):

1. Open the hamburger button with mouse and keyboard; verify item order, tooltips, disabled New instance, and popup dismissal.
2. Search settings, collapse/expand sections, toggle each enabled option, Cancel, reopen and confirm nothing changed. Repeat with OK and restart to verify persistence. Test 100%, 125%, 150% and 200% scaling.
3. Confirm description columns, date formats, larger fonts and info-bar visibility actually change; a loaded mount must prevent changing the servicing engine.
4. Check releases online and offline, verify stable/preview fields and manual release links. Opt into daily checking only if desired. Automatic installation must remain unavailable.
5. Read the embedded MIT notice in Manage license and About. No activation or payment prompt should appear.
6. Export hardware to a new JSON file on a Windows test host. Inspect its schema/device fields; existing files must be preserved.
7. Create a local support ZIP with no presets, then with explicitly selected presets. Inspect the files and confirm nothing is sent. Test an existing destination and inaccessible/oversize inputs.
8. Test Exit with a busy operation, queued changes and a disposable mounted image; existing closing safeguards must still apply.
