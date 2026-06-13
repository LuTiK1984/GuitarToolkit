# Release Process

[Русская версия](#ru)

This document describes the preferred release flow for GuitarToolkit. Use it together with [Release Checklist](RELEASE_CHECKLIST.md).

GuitarToolkit ships as both a standalone Windows desktop app and a VST3 plugin, so every release should verify build output, documentation, and basic audio/DAW behavior.

## Release goals

A good release should be:

- reproducible;
- clearly versioned;
- tested in CI;
- manually smoke-tested where CI cannot help;
- documented in `CHANGELOG.md`;
- published with clear GitHub Release notes and assets.

## 1. Prepare the release branch

Recommended branch name:

```text
release/v<version>
```

## 2. Update version references

Check and update:

- `Directory.Build.props`;
- `GuitarToolkit.Plugin/GuitarToolkitPlugin.cs` if the VST3 plugin version is defined there;
- desktop window title or About page if the app version is visible;
- README download asset names if they mention a specific version;
- `CHANGELOG.md`.

## 3. Update changelog

Move relevant items from `Unreleased` into a version section:

```md
## [1.6.0] - YYYY-MM-DD
```

Suggested sections:

- Added;
- Changed;
- Fixed;
- Known issues;
- Verified.

## 4. Build and test

Run:

```powershell
dotnet restore GuitarToolkit.sln
dotnet build GuitarToolkit.sln --configuration Debug
dotnet test GuitarToolkit.sln --configuration Debug
```

## 5. Build release packages

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version <version> -Configuration Release
```

Expected assets:

```text
GuitarToolkit_DESKTOP_v.<version>.zip
GuitarToolkit_VST3_v.<version>.zip
GuitarToolkit_ML_TRAINER_v.<version>.zip
SHA256SUMS.txt
```

If a Windows code-signing certificate is available, pass its thumbprint so release binaries are Authenticode-signed before packaging:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version <version> -Configuration Release -CertificateThumbprint <thumbprint>
```

The script also writes per-asset `.sha256` files. Upload both the ZIP files and checksum files to GitHub Releases.

## 6. Manual Desktop smoke test

Check:

- app launches;
- About/version is correct;
- Tuner opens and input selection works;
- tuner receives input;
- metronome starts and stops;
- chord or scale playback works;
- Tabs page opens;
- logs folder is accessible if relevant.

## 7. Manual VST3 smoke test

Check in at least one DAW when possible:

- DAW rescans VST3 plugins;
- GuitarToolkit appears in plugin list;
- plugin loads on a track;
- editor opens;
- tuner input works;
- metronome/chord/scale playback works;
- Tabs page opens if supported;
- logs do not show new startup errors.

## 8. Create GitHub Release

Create a tag:

```text
v<version>
```

Upload:

- `GuitarToolkit_DESKTOP_v.<version>.zip`;
- `GuitarToolkit_VST3_v.<version>.zip`.
- `GuitarToolkit_ML_TRAINER_v.<version>.zip`;
- `SHA256SUMS.txt`;
- per-asset `.sha256` files.

Release description should include summary, highlights, installation notes, known issues, verification status, and links to Quick Start and Discussions.

## 9. Post-release checks

After publishing:

- download both ZIP files from GitHub Release;
- confirm archive names are correct;
- confirm README links point to the current repository;
- check CI on `master`;
- open or update follow-up issues for known problems.

---

<a id="ru"></a>

# Процесс релиза

[English version](#release-process)

Этот документ описывает рекомендуемый процесс релиза GuitarToolkit. Используйте его вместе с [Release Checklist](RELEASE_CHECKLIST.md).

GuitarToolkit поставляется как standalone Windows desktop app и VST3 plugin, поэтому каждый релиз должен проверять сборку, документацию и базовое audio/DAW-поведение.

## Цели релиза

Хороший релиз должен быть воспроизводимым, с понятной версией, проверенным через CI, вручную проверенным там, где CI не помогает, описанным в `CHANGELOG.md` и опубликованным с понятными GitHub Release notes/assets.

## Шаги

1. Создать ветку `release/v<version>`.
2. Обновить версии в `Directory.Build.props`, plugin metadata, UI/About и `CHANGELOG.md`.
3. Собрать и протестировать:

```powershell
dotnet restore GuitarToolkit.sln
dotnet build GuitarToolkit.sln --configuration Debug
dotnet test GuitarToolkit.sln --configuration Debug
```

4. Собрать release packages:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version <version> -Configuration Release
```

5. Проверить Desktop вручную.
6. Проверить VST3 в DAW вручную, если возможно.
7. Создать GitHub Release с тегом `v<version>`.
8. Загрузить Desktop ZIP и VST3 ZIP.
9. После релиза скачать оба архива и проверить ссылки.
