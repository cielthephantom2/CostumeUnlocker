# Release workflow

User preference (2026-10-23): after implementing future changes, build/check and copy the DLL pair into Steam when Last Epoch is closed. Do not automatically push to GitHub, create/update tags or publish/upload releases. Only publish when the user explicitly requests it. The current explicit request authorizes publishing 1.1.0-dev.69; it does not authorize automatic publication of later builds.

Keep backups and verify installed DLL hashes. Never overwrite DLLs while Last Epoch is running. Preserve unrelated local work and stage only files belonging to the release.

User preference (2026-10-24): debug instrumentation and private test tools are welcome during investigation; run measurements autonomously. Exclude debug instrumentation and test tools when producing a release build.
