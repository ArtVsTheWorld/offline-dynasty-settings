# Working on this repository

- Author is Ace. Keep the plugin DLL filename and existing settings paths compatible.
- Production source is in Plugin/ and Shared/.
- Build.ps1 requires a local MMC SDK. Test.ps1 without MmcRoot runs SDK-free checks.
- Keep the offline-Dynasty and native hook ownership checks. Do not bypass a failed
  fingerprint or replace the checks with a whole-game EXE hash gate.
- Review docs/updating.md before changing native addresses. Regenerate the native
  manifest with python Tools/export_layout.py after reviewed source changes.
- Never commit game captures, SDK DLLs, saves, logs, credentials or generated binaries.
- Default tests must not patch a running game or create a CollegeFB27-named process
  while MMC is watching. The optional helper lifecycle harness is documented separately.
- Release ZIPs contain only the DLL and docs/README-release.txt renamed README.txt.
