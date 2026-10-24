# Validation results

Executed in the Linux workspace on 2026-10-23:

- .NET SDK 8.0.426 compiled and published `ImageForge.App` in Release mode for `win-x64` with `--self-contained true`. Build completed successfully with no reported warnings or errors.
- `ImageForge.Core.Tests` completed with **21 checks passed**. Coverage includes DISM edition/feature/mount text parsing, path and operation validation, separation of shell-like characters into a single process argument, offline-only command construction, driver recursion, optional-feature payload retention, and the unattended XML namespace, passes, component attributes and input validation.
- Neither the test harness nor the build executed DISM or NTLite. These are parser, command-construction and XML checks; there are no claimed Windows integration results.

Not executed: Windows GUI launch/layout inspection, UAC behavior, real mount journal lifecycle and crash recovery, actual driver/package/feature servicing, Windows System Image Manager validation, and boot/install testing. Follow [WINDOWS-VALIDATION.md](WINDOWS-VALIDATION.md) before deployment use.

The preview is an unsigned first implementation, not a verified production servicing tool. Interrupted mount reservations are intentionally retained for manual recovery; the app does not automatically promote an incomplete mount record.

The build used workspace-local .NET SDK and NuGet caches. The workspace's fresh NuGet cache was marked as having completed the legacy permission migration to avoid an unsupported named-mutex operation in this sandbox. No application synchronization or servicing checks were disabled.
