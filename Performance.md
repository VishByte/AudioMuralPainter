# AMP performance checks

## 2.2.1 audit regression comparison — 2026-11-14

Three alternating 2.2.0/final-source trials used the built-in synthetic benchmark: 60 warm-up frames followed by 360 measured frames per view. The configuration was 801 × 471 pixels at scale 1, 50 bars, FFT 4096, 1024 waveform points, a 40 ms scope, glow 40, gradient styles, clear background, and all three label types enabled.

| View | 2.2.0 wall ms/frame | 2.2.1 wall ms/frame | 2.2.0 CPU ms/frame | 2.2.1 CPU ms/frame |
| --- | ---: | ---: | ---: | ---: |
| Both | 2.386 | 2.433 | 2.387 | 2.431 |
| Bars | 0.223 | 0.226 | 0.217 | 0.217 |
| Oscilloscope | 3.464 | 3.453 | 3.472 | 3.429 |

These are medians, not confidence intervals. The small differences do not demonstrate a material regression or an optimization gain. All measured trials had zero generation-0 collections. The benchmark excludes playback capture, window presentation, and desktop composition, so this comparison is not a new measurement of live click-through cost.

## Historical 2.1 optimization comparison

Measured on a local Windows PC. Both versions were run under comparable background load in three alternating before/after trials. The comparison uses the original version 2 rendering source and the optimized version 2.1 source, compiled with the existing Windows .NET Framework compiler.

## Results

Median **process CPU milliseconds per rendered frame**, across three trials of 360 measured frames each, following 60 warm-up frames:

| View | Before | Optimized | Reduction |
| --- | ---: | ---: | ---: |
| Both | 5.556 ms | 4.167 ms | 25.0% |
| Bars | 2.604 ms | 0.391 ms | 85.0% |
| Oscilloscope | 6.380 ms | 6.380 ms | Approximately unchanged |

Both-view runs triggered five generation-0 collections per measured trial in the original build and zero in the optimized build. Scope-only runs changed from four collections to zero. Nine native before/after output PNG pairs (three views × three trials) had identical SHA-256 hashes.

The workload used the saved 1289 × 808 window configuration, a 4096-sample FFT, 1024 waveform points, full-resolution glow, continuous vertical gradients, a clear background, and the saved label/grid settings. Input was the same deterministic synthetic audio buffer in each run. No visual settings were reduced to obtain these results.

These measurements cover software rendering, including FFT work. They do **not** measure total live application CPU in Task Manager, Windows desktop composition, or GPU use. Real CPU percentage depends on music, window size, display scaling, settings, other running applications, and CPU scheduling. A 25% renderer reduction does not guarantee that a particular 6% Task Manager reading will become 4.5%.

## Changes

- Batches bar segments and peaks into a few drawing calls instead of issuing one call per segment.
- Avoids .NET Framework's repeated point-by-point array conversion for lines and rectangles where compatible native handles are available. The ordinary drawing API remains a fallback.
- Reuses waveform arrays, colors, brushes, pens, fonts, band boundaries, and cached axis strips.
- Copies only the audio sample range needed by the current views, using bulk copies across ring-buffer wraparound.
- Reuses FFT results when the sample revision has not changed.
- Waits for Windows playback-buffer events rather than polling every 5 ms, with a polling fallback for endpoints that reject event initialization. [Microsoft loopback documentation](https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording).
- Stops repainting after silence and animation tails settle, then wakes at the next normal frame tick when audio returns. Settings, resize, exposure, and sample-rate changes still refresh the display.
- Reuses the settings preview's image buffers.

## Audio and correctness

All 72 checks passed. The Windows native-array fast path was confirmed available. Tests cover rendering, transparency, gradients, thresholds, cutoff behavior, labels, presets, settings migration, buffer wraparound, stale capture, idle/wake decisions, resource updates, and public drawing fallback equivalence.

A simultaneous live loopback comparison captured 111,840 frames in the original process and 112,800 in the optimized process during their independently timed probes. Recent RMS values were 0.0000199333 and 0.0000199190, within 0.1%. Frame totals differ because stream start times differ.

The full-resolution oscilloscope glow remains the main rendering cost in the scope-only configuration. Reducing glow or selecting 30 fps is an optional additional saving; this update leaves those choices to the user.

## Reproducing

`Amp.exe --benchmark report.tsv settings.xml` runs the offscreen Windows-runtime render benchmark and writes its comparison PNGs beside the report. `Amp.exe --self-test checks.txt` runs the checks. Both commands exit without opening the visualizer window. Source for the benchmark is included in **Source\Benchmark.cs**.

The previous unoptimized package is retained separately. No Windows security policy, power plan, driver, PATH, or system-wide setting was changed.
