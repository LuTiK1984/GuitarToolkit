# Changelog

All notable changes to GuitarToolkit are documented here.

## [Unreleased]

## [1.8.1] - 2026-07-01

### Added

- Bundled ready-to-use ONNX runtime models with the Desktop and VST3 builds: `ProgressionNextTokenModel.onnx`, `MelodyPhraseTransformer.onnx`, and `MelodyPhraseTransformer.vocab.json`.

### Changed

- The Inspiration and Melody tabs now load user-installed models from `%AppData%\GuitarToolkit\models` when present, otherwise they fall back to the bundled release models next to the app.
- The release script now verifies that Desktop and VST3 packages contain the bundled ONNX model files before archives are created.

## [1.8.0] - 2026-06-13

### Added

- Added ML Trainer model comparison, evaluation history, and a wider full checkpoint prompt suite for more objective model selection.
- Added a mood-targeted synthetic dataset profile and a dataset file picker in ML Trainer for focused mood fine-tuning.
- Added a Melody tab placeholder for the future short-phrase Transformer model.
- Added the first command-line Melody Phrase Transformer training skeleton with dataset generation, validation, checkpoint inspection, and ONNX export.
- Added full Melody Transformer workflow support to ML Trainer, including dataset controls, training progress, checkpoint inspection, evaluation, and ONNX export.
- Added Melody Transformer checkpoint evaluation metrics for diversity, musical fit, mood fit, style/rhythm fit, confidence balance, entropy, and top-3 mass.
- Added Melody Transformer musical constraint penalties for mode, mood, style durations, and entropy balance during training.
- Added Melody Transformer WAV preview generation from ML Trainer for quick listening checks.
- Added Melody Transformer generation training that runs multiple candidate recipes, evaluates them, and preserves theoretical, balanced, and art-house champions.
- Added runtime Melody Transformer ONNX inference in the main app, with a finished Melody tab for generating, displaying, and auditioning short phrases.
- Added an ML Trainer install button that copies Melody Transformer ONNX and vocabulary files into the app runtime model folder.
- Added Melody tab sketchbook controls for chip-based backing progressions, separate/together playback, generated note display, piano-key highlighting, and basic melody edits.
- Added Melody tab BPM and volume controls, synchronized progression/melody playback, clickable piano input, keyboard note hotkeys, and a compact-window horizontal overflow guard.
- Added Melody piano free-play/record mode and made the piano plus generated playback use the selected tonic as the start of one continuous working octave.
- Reworked the Melody piano into a fixed C-based three-octave keyboard with center-octave hotkeys, restored an always reachable stop button, and themed the progression-add menu.
- Improved Melody tab piano ergonomics, note preview sustain, and single-press hotkey playback to avoid repeated crackling when keys are held.
- Added Melody Transformer v2 training support with octave-aware melody tokens, interval/octave behavior penalties, v2 evaluation metrics, and ML Trainer vocabulary selection.
- Added ML Trainer controls for Melody v2 architecture, interval/octave penalties, DataLoader workers, and AMP mixed-precision training.
- Fixed Melody v2 DataLoader worker startup on Windows and added clearer checkpoint/vocabulary mismatch diagnostics.
- Optimized Melody v2 training by precomputing constraint, interval, and octave penalty tables for large-batch GPU runs.
- Added Melody v2 anti-repeat, anti-rest, and anti-duration training penalties plus expanded generated-prompt evaluation metrics.
- Improved Melody runtime generation by feeding a longer phrase history to ONNX and applying musical sampling guards against note/rest/rhythm spam.
- Fixed Melody ONNX export/runtime sequence length alignment and left-padded melody history so the model sees the latest real token instead of padding.
- Tuned Melody runtime duration guards to reduce dense short-note runs in longer generated phrases.
- Fixed Melody generation during playback by stopping the active preview and highlighting a snapshot of the phrase instead of the editable live list.
- Fixed mojibake in Melody model status messages.
- Added Melody Transformer v3 dataset/vocabulary support with phrase profiles, density, contour, note-count, phrase-section, and beat-position context tokens while keeping the output melody token set stable.
- Added SHA-256 checksum generation for release archives and optional Authenticode signing support in the release script.
- Added a visible MIT License link and license note to the Windows About view.

### Fixed

- Streamlined the public README into a shorter bilingual project landing page with current Inspiration Engine and ML Trainer links.
- Fixed the toolkit minimum window/editor size so dense tabs do not collapse under the sidebar.
- Fixed the Inspiration tab compact layout so the sampling controls stay in their own column.
- Fixed desktop resize tracking so the custom window chrome respects the minimum size.
- Increased the desktop minimum height so the tuner string row is visible at startup.
- Fixed Melody Transformer runtime inference for the current ONNX export shape so generated phrases fall back less often and avoid long reshape errors in the UI.

## [1.7.0] - 2026-05-08

### Added

- Added diverse progression dataset generation profiles, checkpoint diversity evaluation prompts, and label smoothing support for the next Inspiration Engine training passes.
- Added Inspiration Engine playback controls for tempo, volume, and looped audition playback.
- Added checkpoint evaluation summaries by style, mode, and mood, plus explicit training-device logging for GPU training checks.
- Added a standalone WinForms ML Trainer utility for dataset generation, training runs, checkpoint inspection, ONNX export/install, and the future melody Transformer workflow.
- Added in-epoch training progress output and clearer ML Trainer labels/tooltips for optimizer reset, CPU mode, learning rate, and label smoothing.
- Added model quality scores for diversity, confidence balance, musical fit, mood fit, style fit, and overall checkpoint comparison in the ML Trainer.
- Added the ML Trainer as a separate release archive in the release build script.

### Fixed

- Fixed progression-model label smoothing so loss is computed only across valid output tokens instead of masked vocabulary entries.

## [1.6.0] - 2026-05-07

### Added

- Added MIT license, contributing guide, code of conduct, security policy, pull request template, CODEOWNERS, Dependabot config, and EditorConfig.
- Added DAW documentation for FL Studio, Reaper, and supported-host compatibility tracking.
- Added bilingual English/Russian README, contributor documentation, security policy, code of conduct, PR template, issue templates, release checklist, quick-start setup guide, and DAW setup guides.
- Added refreshed GitHub screenshot set for dark and light themes.
- Added the Ideas tab skeleton for ONNX-first progression inspiration, with Core generation contracts, a demo fallback model, and playback through existing chord synthesis.
- Added maintainer documentation for the Inspiration Engine model contract, runtime model location, vocabulary shape, and training/export workflow.
- Added the first ML tooling layer for `ProgressionNextTokenModel`: stable vocabulary, sample JSONL dataset, GRU/LSTM training script, ONNX export script, and local artifact ignores.
- Added a 5,000-example synthetic progression dataset, dataset validator, checkpoint inspector, output-token masking, validation metrics, and a verified first local GRU training/export pass.
- Added resumable progression-model training with optimizer-state checkpoints, `best_model.pt`, metrics history, and optional periodic checkpoint snapshots.
- Expanded the synthetic progression generator with balanced context coverage, richer harmonic templates, cadences, substitutions, and mood-aware variation for the next training passes.
- Added ONNX Runtime inference for the Inspiration Engine progression model and a helper script for installing freshly exported models into the app runtime folder.

### Changed

- Linked community, security, release, and DAW documentation from the README and release checklist.
- Reworked README as a full bilingual project overview with release links, architecture notes, feature descriptions, VST3 deployment guidance, and updated screenshots.
- Clarified the Inspiration Engine roadmap around two specialized ONNX model tracks: GRU/LSTM for progressions and a small Transformer for short melody/riff phrases.
- Improved generated progression mapping for borrowed/altered roman-numeral tokens such as `bII`, `bVI`, and `bVII`.

## [1.5.0] - 2026-05-03

### Added

- Added a desktop custom title bar using WPF `WindowChrome`, with custom minimize, maximize, and close controls.
- Added a Settings tab with theme controls and a language-selection placeholder.

### Fixed

- Fixed a startup crash in the first light-theme mapper caused by duplicate reverse color mappings.
- Fixed custom title-bar maximize glyph encoding and enlarged the window-control icons.
- Restored custom title-bar hover hit testing for minimize, maximize, and close buttons.
- Fixed sidebar tab text encoding and protected selected-tab highlighting from global theme remapping.
- Replaced the fragile global element recoloring pass with resource-only theme application so dark theme keeps the original UI colors.
- Moved custom title-bar dragging out of the `WindowChrome` caption area so window-control hover states remain interactive.
- Applied theme resources across the shared tabs instead of limiting the light/dark switch to the Settings surface.
- Rebuilt the About tab with theme-aware resources and clean text encoding.
- Applied theme resources directly to each tab view so unselected tabs also pick up the active palette when opened.
- Added a narrow legacy-color remap for code-created controls, diagrams, beat indicators, tuner string cards, and other non-XAML brushes.
- Made the custom desktop title bar use the active dark/light palette so the light theme also changes the window frame.
- Made code-created Interval Trainer, Scales, Progressions, and Circle of Fifths controls redraw from the active theme palette.
- Polished light-theme tuner, metronome, chord, scale, and interval details before the 1.5 release pass.
- Refined the chord header alignment, fret-number spacing, current-progression outline, and theme-aware window frame styling.
- Softened the light-theme window frame and removed the duplicate centered title-bar label.
- Fixed custom maximized desktop windows so they respect the Windows taskbar work area.

### Changed

- Added a saved light theme option that keeps the main purple identity and accent color while remapping the interface to a lighter palette.
- Restyled the desktop custom title bar toward a glassy purple frame with soft highlights and rounded window controls.
- Reworked theme application toward explicit dark/light resource palettes instead of relying on fragile reverse color inference.
- Converted the host sidebar and settings surface to dynamic theme resources for a more predictable dark/light split.

## [1.4.0] - 2026-05-03

### Changed

- Started interface redesign with refreshed About and Tabs layouts.
- Tabs file selection now uses one explicit source selector and one file selector instead of parallel library/recent/favorites lists.
- Removed the non-working visual sync control from the Tabs toolbar.
- Updated Tabs checkbox and slider styling to better match the dark purple theme.
- Refined the Tabs toolbar toward a compact 28px control rhythm, moved auto-follow next to track mute/solo, and improved the collapsed-window layout.
- Normalized Tabs toolbar spacing around a tighter 26px control size and lowered compact-window notation scale to show more usable tab area.
- Fine-tuned Tabs auto-follow and speed row vertical alignment for the compact toolbar layout.
- Nudged the Tabs speed controls down slightly to balance the lower toolbar spacing.
- Moved the Tabs auto-follow toggle beside the speed controls for a steadier compact layout.
- Rebuilt the Tabs speed-control row with fixed grid spacing to match the target compact toolbar layout.
- Equalized speed-row gaps and rounded the speed percentage field to match neighboring controls.
- Shifted the Tabs file controls right to balance compact-window side padding and reduce over-wide fullscreen file selectors.
- Redesigned the Circle of Fifths tab with the compact dark panel style established by the Tabs redesign.
- Enlarged Circle of Fifths note nodes and unified selection color: green for the selected key, purple for the key notes list.
- Added purple Circle of Fifths highlights for diatonic major and minor chord nodes around the selected key.
- Fixed Circle of Fifths diatonic highlighting for flat major keys such as Db, Ab, Eb, and Bb.
- Increased all Circle of Fifths note nodes for stronger readability.
- Redesigned the Progressions tab around a compact builder layout with separated built-in and saved presets.
- Progression preset saving now requires an explicit name and updates same-named saved presets instead of creating endless defaults.
- Styled Progressions combo box, slider, and loop checkbox to match the redesigned Tabs controls.
- Reworked the Progressions top toolbar into two rows so mode and loop controls remain visible in compact windows.
- Aligned the Progressions mode selector to the left and replaced preset button clouds with built-in and saved preset dropdowns.
- Redesigned the Interval Trainer tab with the same compact dark panel style, clearer answer grid, and stronger result highlighting.
- Redesigned the Scales tab with compact controls, info panels, and a controlled-height fretboard instead of the old oversized layout.
- Added the 17th-fret scale marker and redesigned the Chords tab with compact panels, cleaner filters, and a refreshed diagram area.
- Fixed a Chords filter crash, stabilized left navigation hover highlighting, and guarded Tabs track rendering/playback-mode errors.
- Quarantined tab files that fail during alphaTab import and documented the known GP4 import crash.
- Redesigned the Metronome tab with compact controls, animated pendulum feedback, beat indicators, and quick tempo presets.
- Fixed the Metronome pendulum so the rod and bob swing together from the same pivot.
- Centered the Metronome BPM label under the tempo number and redesigned the Tuner tab with compact panels, styled controls, and local input-device selection.
- Moved desktop input-device selection from the global header into the Tuner tab.
- Recentered the Tuner needle layout, stacked the detected note above the cents gauge, and normalized string cards into an even six-column row.
- Fixed Tuner text encoding, enlarged the frequency readout, centered the pitch display vertically, and removed note-label fade jitter.
- Normalized Chords type selector buttons to a fixed width so every chord type has the same visual size.

## [1.3.3] - 2026-04-30

### Added

- Basic application/plugin logging to `%AppData%\GuitarToolkit\logs`.
- About page with version, GitHub link, log folder shortcut, feature summary, and third-party library notes.
- Roadmap for product direction, near-term priorities, bughunting, and commercial-readiness work.
- Tabs page now remembers the last file, selected track, volume, speed, auto-follow, sync offset, and solo/mute state.
- Tabs page now restores the last playback cursor position for the remembered file.
- Tabs page now keeps a recent files list for faster reopening.
- Tabs page now supports a simple favorites list for frequently used tab files.
- Tabs page now supports a simple library folder scan for Guitar Pro and MusicXML files.
- Tabs library can now be refreshed without selecting the folder again.
- Added `THIRD_PARTY_NOTICES.md` with dependency/license notes.

### Changed

- VST install documentation now emphasizes copying the complete plugin folder, including dependencies and `runtimes`.
- README installation notes now mention diagnostics and the log folder.
- Desktop window minimum size now matches the startup size to protect the UI layout.
- Tabs toolbar spacing was improved when controls wrap to a second row.

## [1.3.2] - 2026-04-30

### Added

- Tabs page is now enabled in the VST3 plugin after FL Studio testing with the complete dependency set.
- README screenshots and feature descriptions now include the Tabs page.

### Changed

- VST deployment now copies the full plugin output folder, including transitive dependencies and runtime folders.
- README positioning was updated from an educational project to a personal passion project.
- Updated project, desktop, and VST plugin version to `1.3.2`.
- Updated release archive names for `v1.3.2`.

### Verified

- Build passes with 0 errors and 0 warnings.
- Test suite passes: 73/73.
- FL Studio loads the VST3 plugin with the Tabs page when the complete dependency set is deployed.

## [1.3.1] - 2026-04-30

### Fixed

- VST3 release package now includes transitive alphaTab/AlphaSkia dependencies required by the shared UI assembly.
- The VST3 plugin no longer instantiates the Tabs page inside DAW hosts; the alphaTab-based Tabs page remains desktop-only for now.

### Changed

- Updated project, desktop, and VST plugin version to `1.3.1`.
- Updated release archive names for `v1.3.1`.

### Verified

- Build passes with 0 errors and 0 warnings.
- Test suite passes: 73/73.
- VST3 Release output contains `AlphaTab.dll`, `AlphaTab.Windows.dll`, `AlphaSkia.dll`, and `AlphaSkia.Native.Windows.dll`.

## [1.3.0] - 2026-04-30

### Added

- New Tabs page prototype powered by alphaTab.
- Guitar Pro and MusicXML loading for desktop tab viewing.
- Track selection, selected-track solo/mute, play/pause/stop controls, volume control, and flexible playback speed input.
- Keyboard shortcuts on the active Tabs page: Space for play/pause and Esc for stop.
- Automatic cursor following for long tablatures.

### Changed

- Improved Tabs page layout for smaller windows.
- Removed prototype notice panel from the Tabs page to give more space to notation.
- Added adaptive alphaTab rendering for window resize, maximize, and restore.
- Updated project, desktop, and VST plugin version to `1.3.0`.
- Updated release archive names for `v1.3.0`.

### Known Follow-Up

- The Tabs page can visibly re-render a few times while adapting notation size after window resize/maximize/restore.

### Verified

- Build passes with 0 errors and 0 warnings.
- Test suite passes: 73/73.

## [1.2.0] - 2026-04-29

### Added

- Application icon for the standalone desktop executable and main window.
- GitHub README badges.
- README screenshots for tuner, chord library, and fretboard/scales.
- Release checklist for repeatable publishing.
- GitHub issue templates for bug reports, DAW compatibility reports, and feature requests.

### Changed

- Updated project, desktop, and VST plugin version to `1.2.0`.
- Updated installation docs and release archive names for `v1.2.0`.

### Verified

- Build passes with 0 errors and 0 warnings.
- Test suite passes: 73/73.

## [1.1.0] - 2026-04-29

### Added

- Bilingual GitHub README with English and Russian project descriptions.
- `AGENTS.md` with development notes for VST, verification, and project boundaries.
- `Directory.Build.props` with shared project metadata and version `1.1.0`.
- Additional `MetronomeEngine` unit tests.
- Release notes for `v1.1.0`.

### Changed

- Updated VST plugin version to `1.1.0`.
- Updated desktop window title to `GuitarToolkit v1.1.0`.
- Improved `deploy-vst.bat` with required-file checks and clearer error messages.
- User settings load/save failures now write diagnostic messages to debug output.

### Verified

- Build passes with 0 errors and 0 warnings.
- Test suite passes: 73/73.
- VST plugin no longer crashes in the tested DAW scenario where recording inputs are assigned after the plugin is loaded.

## [1.0.0] - 2026-04-20

### Added

- Initial GuitarToolkit release.
- VST3 plugin via AudioPlugSharp.
- Standalone WPF desktop application via NAudio.
- Shared WPF UI library.
- Core DSP and theory library with tuner, metronome, chord library, fretboard scales, interval trainer, progression builder, and circle of fifths.
- xUnit test project for pitch detection and core music theory models.
