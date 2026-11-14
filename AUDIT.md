# Code, privacy, and security review — 2026-11-14

Scope: all application source, interop declarations, UI, diagnostics, build scripts, documentation, Git history, and the published v2.2.0 portable ZIP. This was a source-assisted local assessment of a Windows desktop application. There is no application network service to attack.

## Findings addressed in 2.2.1

| Finding | Resolution and evidence |
| --- | --- |
| Appearance exports contained window coordinates and custom hotkeys | Separate cached serializer omits private interaction/geometry fields. Reproduced on 2.2.0; the patched export excludes both. Local settings still retain them. |
| Predictable `destination.tmp` could overwrite/delete an unrelated neighboring file | Unique create-new temporary file in the destination directory, followed by replacement. An old-build canary was lost; the patched build preserves it. Locked-destination tests preserve the original and clean up the temporary file. |
| XML size check and parsing used separate file opens; nesting was not bounded | One read-only file stream, byte/character limits, depth preflight, explicit null-root rejection. DTD/external-entity rejection was already present and is retained. Excessive nesting accepted by the old build is now rejected. No remote code execution was demonstrated. |
| Windows device names were accepted as preset names | Reject reserved names, including names with extensions and superscript COM/LPT digits. Traversal and alternate data stream attempts remain rejected. |
| Native drawing trusted caller-supplied array counts | Reject negative/oversized counts before P/Invoke. Native library lookup is restricted to System32 as additional hardening. |
| Audio/FFT helpers accepted invalid dimensions | Validate array lengths, sample formats, FFT power-of-two size, and supported device rate. Correct DC/Nyquist amplitude normalization and reject degenerate Hann windows. Clear discontinuous audio history and check shutdown while draining packets. |
| Window recreation could disable click-through after a failed layered presentation | Wait for WinForms recreation to finish, reset its layered state, and re-establish recovery/presentation. Actual desktop test reproduced the failure during review and passed after the final lifecycle fix. |
| Closing and disposing duplicated window cleanup; cancelled closes could leave resources unusable | Separate save/close behavior from idempotent resource disposal, honor cancellation before stopping the timer, and avoid duplicate saves. Desktop checks verify continued settings and click-through operation after cancellation. |
| Custom unmodified shortcuts could fire while entering text | Preserve ordinary editing keys in text, numeric, and combo editors. Reject unusable global F12, mouse-only/synthetic-only codes, oversized strings, and unknown modifier bits. |
| Fonts and application icon relied on eventual cleanup | Explicit form-owned font cache and icon disposal. Benchmark renderer is disposed on exceptional paths; partial brush allocation cleanup tolerates unfinished entries. |
| Scope axes could disagree with the plotted data | Use the actual sample span for time labels, including low/high-rate limits. Use one amplitude mapping for waveform, grids, and labels. Pixel tests cover four known amplitudes. |
| Threshold dragging could continue after mouse capture was lost | End the drag when capture changes, even without MouseUp. A regression check verifies subsequent movement does not edit the threshold. |
| Diagnostic errors could escape and crash; unknown switches opened the app | Clean nonzero exits for diagnostic I/O failures and unknown commands. Playback probe creates its report directory; default artifacts stay under work. |
| Compressed formatting, mixed responsibilities, and fragile editor bindings | Separate capture, PCM decoding, FFT, interop, persistence, presets, main-window lifecycle, settings shell, controls, and pages. Typed editor selectors catch renamed fields at compile time. Tests enforce reset-section coverage; one local-only annotation controls both preset export and import preservation. |
| Missing repeatable maintenance checks and incomplete UI snapshots | Roslyn formatting with token-preservation checks, EditorConfig, build output parameter, privacy guardrail, Windows CI, documented resource ownership, and a package allowlist. Group tests by responsibility. Settings snapshots now create controls invisibly and reject empty forms; page tests check expected controls. |

## Validation

- Optimized Windows .NET Framework build with warnings treated as errors.
- 182 automated assertions, including 128 seeded configurations that validate and render boundary settings, malformed/deep/oversized XML, external-entity and expansion attempts, preset-name attacks, null/nonfinite values, save failures, native array bounds, shortcut validation, calibrated scope axes, typed editor conversion, reset coverage, and lost mouse capture.
- Invalid command and invalid diagnostic-path exit checks.
- Eleven desktop checks: clear/opaque click-through transitions, settings unlock/relock, handle recreation, cancelled close, and repeated settings/page rebuilds. After collection, GDI handles remained 84 → 84 and USER handles 30 → 30 across the repeated-cycle comparison. This is a short lifecycle check, not a long-duration leak certification.
- Four renderer snapshots remained byte-identical across the final structural cleanup. Grid/label images differ from 2.2.0 where positions were intentionally calibrated. The old unshown-form settings image was blank and was not evidence of visual correctness; the corrected image contains the controls and was inspected.
- Three alternating original/final renderer trials showed similar timings, with zero generation-0 collections in all measured trials. Both-view median wall time was 2.386 → 2.433 ms/frame, bars 0.223 → 0.226, and scope 3.464 → 3.453. See Performance.md for workload and limits.
- No credentials, private Windows user paths, personal settings, or saved presets were found in the inspected published source/ZIP. The sole published commit uses the public GitHub account's noreply identity. Local personal settings stay ignored.
- The published v2.2.0 ZIP SHA-256 is `C98E4E2D62C302B31E0CAFEDF249C2EB73EEC3295AA34C2BBE62E0F4ADAD6010`; it was inspected as an archive without executing extracted content.

## Limits and remaining platform behavior

The tests do not establish absence of every bug. No kernel/driver exploitation, independent third-party penetration test, long-duration soak, GPU profiling, or exhaustive multi-monitor/DPI matrix was performed. The timer remains best effort: earlier desktop measurements achieved about 31–32 FPS with a 60 FPS setting. That scheduling limitation is independent of click-through and was not changed as part of this security patch. Very high sample rates can shorten the available scope window; labels now reflect that limit.

The optional native-array fast path still depends on private .NET Framework graphics handles for performance; a tested public API fallback remains. There are no third-party runtime packages to audit. The executable remains unsigned.

## Guidance consulted

- [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions): clarity, consistent formatting, and explicit ownership.
- [Microsoft disposal guidance](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/dispose-pattern) and [Form.OnFormClosing](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.onformclosing): deterministic resource cleanup and cancellation-aware window lifecycle.
- [Microsoft XML reader security guidance](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings): bounded parsing and disabled external resolution.
- [Microsoft Windows filename rules](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file): reserved device names and path semantics.
- [Microsoft DLL search path controls](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.defaultdllimportsearchpathsattribute): constrain native library resolution.
- [Microsoft RegisterHotKey contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey): recovery registration and reserved F12.
- [Microsoft capture-buffer contract](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer): packet ownership and discontinuity handling.
- [GitHub Actions secure-use guidance](https://docs.github.com/en/actions/reference/security/secure-use): minimal token permissions, pinned actions, and untrusted build isolation.
