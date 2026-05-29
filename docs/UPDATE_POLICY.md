# Update Policy

DBuilder does not port Ultimate Doom Builder's in-app update checker directly. The current editor is a cross-platform .NET application without a packaged release channel, so automatic update checks would be misleading until installation and release artifacts exist.

The replacement policy is:

- Development builds are updated through the repository using `git pull`, then verified with `bash scripts/verify.sh`.
- Release builds, once packaging exists, should publish signed artifacts through the project release process and document the install path for each supported platform.
- The editor should not make network update checks until a stable release feed, signing policy, and user-visible update settings exist.
- If an update UI is added later, it must be opt-in or clearly user initiated, and it must report the exact release source it checks.

This keeps update behavior deterministic during the port while leaving a clear path for a future packaged updater.
