# Security and privacy

AMP processes default-device playback samples in memory. The application has no network client, listener, telemetry, updater, account system, microphone capture, or audio-file recording feature. It runs with the current user's privileges.

Local preferences intentionally retain appearance, window geometry, hotkeys, and interaction choices in `Amp.settings.xml`. Treat that file and old full-preference exports as personal data. Starting with 2.2.1, newly saved and exported appearance presets omit geometry, hotkeys, and click-through state. Importing an older preset preserves the current window and recovery configuration.

XML imports prohibit DTDs and external resolution, are limited to 1 MiB, and reject nesting beyond 32 levels before deserialization. Numeric, color, choice, and hotkey values are validated. Named presets reject path traversal, alternate data streams, and Windows device names. Writes use unique temporary files and replacement in the destination directory. Native imports are restricted to Windows System32 search paths.

The relevant untrusted inputs are preset XML and filenames. Native playback buffers are validated before decoding. A process already running as the same Windows user can still change files in a writable portable-app folder; AMP is not an isolation boundary against a compromised Windows account. Windows and .NET Framework security updates remain part of the platform.

Before sharing a diagnostic report, review it for local paths and device details. Never post credentials, personal settings, or recordings in an issue. Use the repository's private vulnerability-reporting channel when available; otherwise request a private contact method without posting exploit details or sensitive data publicly.

The automated checks and review in `AUDIT.md` reduce known risks. They are not a certification that all vulnerabilities or bugs have been eliminated.
