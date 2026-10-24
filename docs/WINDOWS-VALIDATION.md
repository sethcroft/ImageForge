# Windows validation checklist

These checks require a Windows 10/11 x64 test machine or VM, sufficient local disk space, and expendable copies of a Windows installation WIM. They have not been executed by the Linux build environment.

1. Open the extracted self-contained package, accept elevation, and verify the five tabs render and resize correctly. Launch a second instance and verify it exits without starting a second UI.
2. Select a WIM containing multiple editions. Compare displayed names and indices against `dism /English /Get-WimInfo /WimFile:<file>`. Change the source textbox and verify mounting requires rereading editions.
3. Mount a copied edition in an empty local NTFS directory. Verify `/Get-MountedImageInfo` matches its source and index. Verify an occupied directory and a mount created by another tool are rejected.
4. Read optional features. Queue a harmless feature change available in that image, apply it, and verify `/Get-FeatureInfo`. Save and unmount; remount and verify persistence. Repeat a change with discard and verify it was not committed.
5. Integrate a signed INF driver matched to the image architecture and a compatible CAB update. Verify with `/Get-Drivers` and `/Get-Packages` on the mounted image. Test package ordering and an intentionally inapplicable update: processing must stop and leave subsequent steps queued.
6. Save a preset, restart the app, load the preset, and verify paths, edition, queue order and operation kinds. Test unknown operation kinds, missing files, invalid indices, and malformed JSON. Import must never execute queued commands.
7. Close while mounted, restart, reconnect, and verify normal servicing. Try to close during a long operation: closing must be blocked. Test mount recovery in a disposable VM after an interrupted operation, retaining the journal for diagnosis.
8. Generate `Autounattend.xml` and validate it in Windows System Image Manager with the target image catalog. Deploy in an expendable VM and verify computer name, locale and time zone, and that other setup questions are still presented.
9. Export activity logs. Verify failure diagnostics are readable, the queue retains a failed step, and completed steps are removed.
10. Boot and install the final serviced image in a VM. Confirm driver/update results and feature state after installation before considering production use.
