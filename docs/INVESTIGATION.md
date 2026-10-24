# NTLite investigation and implementation boundary

Date: 2026-10-23. Target: the publicly downloadable NTLite x64 installer from the official [download page](https://ntlite.com/download/), advertised as version 2026.10.12522.

## Observations

- Downloaded `https://downloads.ntlite.com/files/NTLite_setup_x64.exe` to `/workspace/ntlite-study/NTLite_setup_x64.exe` (28,192,512 bytes). Its SHA-256 is `579e61e0cffdd69272173e3d23f1e55a215015697e210f22e57442bfb377b94d`, matching the official page at the time of inspection. This comparison alone is not Authenticode signature verification.
- REA CLI package `rea-agents@6.7.0` inspected the installer, using `inspect-pe-resources` and `inspect-with-objdump ... file_headers`. The first reported PE32+, x86-64 resources; the second reported `pei-x86-64`, an x86-64 architecture and entry point `0x4fd5d0`, with the file digest unchanged after inspection.
- Complete REA Evidence is saved in `/workspace/ntlite-study/installer-resources.json` and `installer-headers.json`, with unchanged copies bundled in this project's `docs/evidence/` directory. Evidence IDs: `ev_0e30f29c192cfef625191c6c0a38e83a74e70444333aeb2b1d457f12eb268c66` and `ev_00f5c5f7a1f701baa27f5c3f927727edf59e6ef061330e792ce98818d58d945e` respectively.
- `rea doctor --client codex --json` found no native Ghidra, Hopper, or IDA provider configured, and no active MCP registration. The CLI was used directly; no client configuration was changed. The downloaded installer was never executed.
- NTLite's public [features](https://ntlite.com/features/) describe image management, drivers, updates, optional components, unattended setup, presets and other workflows. The public download page describes a native C++ Windows application. These are vendor descriptions, not runtime observations.

## Implementation choices and unknowns

ImageForge's WPF UI and DISM command adapter are independently written. The installer evidence establishes file structure and identity, not how NTLite implements image servicing. The installer was not unpacked, the application executable was not decompiled, and no NTLite runtime behavior was observed. NTLite's algorithms, component-removal logic, dependency model and internal formats remain unknown. No feature-equivalence claim follows from this inspection.

The implementation relies on Microsoft's documented command interface:

- [DISM image management](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11)
- [DISM package and feature servicing](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-operating-system-package-servicing-command-line-options?view=windows-11)
- [DISM driver servicing](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-driver-servicing-command-line-options-s14?view=windows-11)
- [Windows unattended international settings](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/unattend/microsoft-windows-international-core)

Windows runtime behavior, image bootability, hardware-driver compatibility and deployment results remain unverified in this Linux environment. Compilation and core checks are separate evidence and do not establish any of those outcomes.
