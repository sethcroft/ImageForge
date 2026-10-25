const featureGroups = [
 {name:'Image',items:[
  ['Download Windows','Direct Microsoft ISO/ESD links with SHA-256 verification','available','↓','Open Microsoft’s official download page and paste its direct media URL and exact SHA-256 hash. Downloads stay on approved Microsoft delivery hosts, verify the supplied hash and container format, and publish only a completed file. Cancellation and failures remove partial downloads. This is not an automatic version catalogue.'],
  ['Image management','WIM, ESD, source folders and ISO media','available','▦','Inspect WIM/ESD/SWM/VHD/VHDX sources. Load ESD or complete split sets through an automatically prepared writable cache. Export selected or all editions, join/split WIM sets, delete unwanted WIM editions, and queue eligible offline edition upgrades. Build ISOs with matching setup media and a separately supplied ADK oscdimg tool.'],
  ['Deployed install editing','Live Windows, other drives and virtual-disk images','partial','◫','Select This PC (live) for applicable packages and optional features, or another Windows drive for offline servicing. VHD/VHDX images use the DISM mount lifecycle. Live/deployed changes write immediately and cannot be discarded. Offline drivers are supported; registry tweaks and proprietary component removal are not included.'],
  ['Host refresh wizard','Compatibility scan and interactive Windows Setup','partial','↻','Choose original, matching Microsoft media, run a compatibility scan, then open interactive Windows Setup after a completed pass. Setup determines whether apps, settings and data can be preserved. ImageForge does not accept license terms, bypass checks, choose a clean install, or force a restart.']
 ]},
 {name:'Integrate',items:[
  ['Updates','Queue local CAB and MSU packages','available','↓','Add applicable updates for a mounted image, other Windows drive or the running PC. Online MSU support requires Windows 11 or newer. Review and reorder prerequisites yourself. Live/deployed changes write immediately; a restart may be required.'],
  ['Update performance','Parallel extraction and reusable caching','planned','ϟ','Parallel update extraction and persistent package caching are not implemented.'],
  ['Updates downloader','Find matching Windows updates','planned','⇩','Microsoft Update Catalog discovery, download and automatic dependency resolution are planned.'],
  ['Secure Boot readiness','Inspect boot signer and firmware readiness','planned','◇','Secure Boot certificate migration and readiness inspection are not available in this preview.'],
  ['Cleanup & optimization','Reduce superseded update content','planned','✧','A guided component-store cleanup workflow is planned. The preview does not run ResetBase or perform proprietary component-store surgery.'],
  ['Drivers','Individual INF files or recursive folders','available','▤','Integrate signed INF packages into a mounted image or another offline Windows drive. Online driver integration, driver installer EXEs and automatic hardware matching are not included.'],
  ['Hardware targeting','Match a deployment to a device profile','planned','⌘','Hardware inventory, missing-driver analysis and profile-based targeting are future work.'],
  ['Registry','Prepare image registry changes','planned','⊞','An offline registry editor and REG-file import are not available.'],
  ['Power plan','Manage deployment power settings','planned','ϟ','Importing and selecting offline power schemes is not implemented.'],
  ['Post-setup additions','Stage scripts and application installers','planned','＋','Queuing first-boot installers and arbitrary scripts is a separate future workflow.'],
  ['Install updates wizard','Install updates on a running system','planned','↳','The current update workflow services offline mounted images only.']
 ]},
 {name:'Remove',items:[
  ['Components','Optional Windows feature controls','partial','◫','Available today: enable or disable DISM optional features. Disabling retains payload files. A dependency-aware component-removal engine and built-in app removal are not implemented.'],
  ['Remove reinstalls','Track components restored by later updates','planned','↺','Detecting and removing update-restored components is not part of this preview.'],
  ['Scheduled tasks','Configure tasks in a deployment image','planned','◷','Task enable, disable and removal controls are planned.']
 ]},
 {name:'Configure',items:[
  ['Settings','Windows policies and preferences','planned','⚙','Privacy, Explorer, performance and policy presets are not included.'],
  ['Features','Read and queue optional-feature changes','available','☷','Read the actual selected Windows target’s feature inventory. Enable includes parent dependencies and does not fetch missing payloads from Windows Update; missing source payloads can cause DISM to reject an operation.'],
  ['Services','Configure startup and service policy','planned','≋','Offline service configuration and dependency-aware startup controls are planned.']
 ]},
 {name:'Automate',items:[
  ['Unattended setup','Machine name, locale and time zone','partial','▧','Export a partial amd64 Autounattend.xml answer file. Partitioning, account creation and full zero-touch setup are not included. Validate against the actual target image with Windows System Image Manager.'],
  ['Disk partitioning','Prepare an installation disk layout','planned','▥','Automated partitioning and disk-wipe operations are not available.'],
  ['Post-setup execution','Run actions during installation','planned','▷','First-boot and post-install task execution is not implemented.'],
  ['Image tasks','Automate work across multiple editions','planned','⧉','The library can list multiple image sources. Batch servicing across editions is future work.'],
  ['Command-line automation','Use repeatable non-interactive builds','planned','›_','Presets can be saved and loaded in the desktop UI. A public headless command-line interface is not yet available.']
 ]}
];
const matrices = {
 host:{head:['Host platform','Preview status','Requirements / limits'],rows:[
  ['Windows 11 x64','Development target','target','Administrator account; compatible DISM engine. No build-specific certification yet.'],
  ['Windows 10 x64','Experimental','experimental','Runtime lifecycle and DISM version matter; Windows 11 targets can require ADK DISM.'],
  ['Windows Server Desktop Experience x64','Experimental','experimental','Desktop and servicing behavior have not been verified.'],
  ['Windows 7 / 8 / 8.1','Not supported','unavailable','No legacy .NET desktop package.'],
  ['32-bit Windows / ARM64 hosts','Not supported','unavailable','Only the Windows x64 desktop package is provided; emulation is unverified.'],
  ['Server Core / WinPE / Linux / macOS','Not supported','unavailable','No headless or non-Windows servicing host workflow.']
 ]},
 target:{head:['Target Windows image','Preview status','Version reference / limits'],rows:[
  ['Windows 11 25H2','Development target','target','Base build 26200; needs recorded servicing and deployment tests.'],
  ['Windows 11 24H2','Development target','target','Base build 26100; use an appropriate Microsoft servicing engine.'],
  ['Windows 11 26H2 / 26H1','Experimental','experimental','New or hardware-specific release families; no ImageForge validation claim.'],
  ['Windows 11 23H2 / 22H2 / 21H2','Experimental','experimental','Legacy targets; edition lifecycle and command availability vary.'],
  ['Windows 10 22H2','Development target','target','Base build 19045; normal Windows support ended 14 October 2025.'],
  ['Windows 10 Enterprise / IoT LTSC','Experimental','experimental','Edition-specific lifecycle; target applicability remains unverified.'],
  ['Windows Server 2025 / 2022 / 2019 / 2016','Experimental','experimental','Compatible tooling may service these images; ImageForge has not verified them.'],
  ['Windows 8.1 / 8 / 7 and older Server','Outside preview scope','unavailable','Underlying DISM capabilities do not establish ImageForge support.']
 ]},
 formats:{head:['Format / operation','Implementation status','What is included'],rows:[
  ['WIM','Implemented','target','Inspect, mount, service, save/discard, export one/all editions, split, delete editions.'],
  ['ESD','Implemented','target','Inspect/export one or all editions. Load automatically prepares a writable WIM cache.'],
  ['ISO import','Implemented','target','Copy a local ISO to a new editable media folder using Windows Storage cmdlets.'],
  ['ISO creation','Requires Windows ADK','experimental','Create BIOS + UEFI media with oscdimg and a complete setup-media folder.'],
  ['SWM split sets','Implemented','target','Create split sets; export/join complete matching parts; load through a writable WIM cache. Windows runtime validation remains pending.'],
  ['VHD / VHDX','Implemented; unverified','experimental','Inspect and mount with DISM. Integrity flags are WIM-only. No native disk-partition editing.'],
  ['Deployed / live Windows','Partial','experimental','Applicable packages and optional features; offline drivers and eligible offline edition upgrades. Changes write immediately.'],
  ['Unattended answer file','Partial','experimental','amd64 machine name, locale and time zone. Validate with Windows SIM.']
 ]}
};
