# Release Checklist

[Русская версия](#ru)

Use this checklist before publishing a GuitarToolkit release. For the full step-by-step flow, see [Release Process](RELEASE_PROCESS.md).

## 1. Version

- Update `Directory.Build.props`.
- Update `GuitarToolkit.Plugin/GuitarToolkitPlugin.cs` plugin version.
- Update desktop window title if the version is visible in UI.
- Update `CHANGELOG.md`.
- Update README release archive names if needed.
- Check `THIRD_PARTY_NOTICES.md` when dependencies change.
- Check community files if contributor workflow changed:
  - `CONTRIBUTING.md`
  - `SECURITY.md`
  - `.github/PULL_REQUEST_TEMPLATE.md`
- Check DAW docs if VST behavior changed:
  - `docs/user/FL_STUDIO.md`
  - `docs/user/REAPER.md`
  - `docs/user/SUPPORTED_DAWS.md`
- Check dependency update notes if dependencies changed:
  - `docs/maintainer/DEPENDENCY_POLICY.md`

## 2. Build

Run:

```powershell
dotnet build GuitarToolkit.sln --no-restore --configuration Debug
dotnet test GuitarToolkit.sln --no-restore --configuration Debug
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version <version> -Configuration Release
```

Expected:

- Build has 0 errors and 0 warnings.
- All tests pass.
- Release ZIP files are created in `artifacts/release/`.
- `SHA256SUMS.txt` and per-asset `.sha256` files are created in `artifacts/release/`.
- If a certificate is available, release binaries are signed with `-CertificateThumbprint <thumbprint>`.

## 3. Manual Desktop Check

- Launch `GuitarToolkit.Desktop.exe`.
- Confirm the application icon appears in the window and taskbar.
- Select an input device.
- Check tuner input.
- Check metronome playback.
- Check chord/scale playback.
- Check the Tabs page: open a Guitar Pro file, switch tracks, play/pause/stop, solo/mute, resize maximize/restore, and verify auto-follow.
- Check Tabs recent files, favorites, library folder loading, and library refresh.

## 4. Manual VST3 Check

- Close the DAW.
- Deploy the plugin or copy release files to the VST3 folder.
- Open the DAW and rescan plugins.
- Add GuitarToolkit to an audio track.
- Assign or change the recording input after the plugin is loaded.
- Check tuner input, metronome playback, and chord/scale playback.
- Check Tabs page loading, playback, recent files, favorites, and library folder if the DAW can open the editor reliably.

## 5. GitHub Release

- Create a draft release.
- Upload:
  - `GuitarToolkit_VST3_v.<version>.zip`
  - `GuitarToolkit_DESKTOP_v.<version>.zip`
  - `GuitarToolkit_ML_TRAINER_v.<version>.zip`
  - `SHA256SUMS.txt`
  - per-asset `.sha256` files
- Paste release notes from `CHANGELOG.md` or the template in `docs/releases/RELEASE_PROCESS.md`.
- Review asset names and description.
- Publish the release.

## 6. Post-release Repository Check

- Confirm GitHub Community Standards still pass.
- Confirm repository description and topics are up to date.
- Confirm the latest release assets download correctly.
- Open a fresh issue form preview if issue templates changed.
- Confirm Discussions routes still point users to Q&A, Ideas, and DAW Compatibility.

---

<a id="ru"></a>

# Чеклист релиза

[English version](#release-checklist)

Используйте этот чеклист перед публикацией релиза GuitarToolkit. Полный пошаговый процесс описан в [Release Process](RELEASE_PROCESS.md).

## 1. Версия

- Обновить `Directory.Build.props`.
- Обновить версию плагина в `GuitarToolkit.Plugin/GuitarToolkitPlugin.cs`.
- Обновить title desktop-окна, если версия видна в UI.
- Обновить `CHANGELOG.md`.
- Обновить имена релизных архивов в README, если нужно.
- Проверить `THIRD_PARTY_NOTICES.md`, если менялись зависимости.
- Проверить community-файлы, если менялся workflow контрибьюторов.
- Проверить DAW-документацию, если менялось поведение VST:
  - `docs/user/FL_STUDIO.md`;
  - `docs/user/REAPER.md`;
  - `docs/user/SUPPORTED_DAWS.md`.
- Проверить заметки по зависимостям, если менялись dependencies:
  - `docs/maintainer/DEPENDENCY_POLICY.md`.

## 2. Сборка

Запустить:

```powershell
dotnet build GuitarToolkit.sln --no-restore --configuration Debug
dotnet test GuitarToolkit.sln --no-restore --configuration Debug
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version <version> -Configuration Release
```

Ожидаемый результат:

- Build без ошибок и предупреждений.
- Все тесты проходят.
- Релизные ZIP-файлы созданы в `artifacts/release/`.

## 3. Ручная проверка Desktop

- Запустить `GuitarToolkit.Desktop.exe`.
- Проверить иконку приложения в окне и на taskbar.
- Выбрать входное устройство.
- Проверить вход тюнера.
- Проверить воспроизведение метронома.
- Проверить воспроизведение аккордов/гамм.
- Проверить Tabs: открыть Guitar Pro файл, переключить дорожки, play/pause/stop, solo/mute, resize maximize/restore и auto-follow.

## 4. Ручная проверка VST3

- Закрыть DAW.
- Задеплоить плагин или скопировать релизные файлы в VST3-каталог.
- Открыть DAW и пересканировать плагины.
- Добавить GuitarToolkit на audio track.
- Назначить или изменить вход записи после загрузки плагина.
- Проверить вход тюнера, метроном и воспроизведение аккордов/гамм.
- Проверить Tabs: загрузку, playback, recent files, favorites и library folder, если DAW стабильно открывает editor.

## 5. GitHub Release

- Создать draft release.
- Загрузить:
  - `GuitarToolkit_VST3_v.<version>.zip`;
  - `GuitarToolkit_DESKTOP_v.<version>.zip`.
- Вставить release notes из `CHANGELOG.md` или шаблон из `docs/releases/RELEASE_PROCESS.md`.
- Проверить имена assets и описание.
- Опубликовать release.

## 6. Проверка репозитория после релиза

- Убедиться, что GitHub Community Standards всё ещё проходят.
- Проверить описание репозитория и topics.
- Проверить, что assets последнего релиза скачиваются.
- Открыть preview issue forms, если менялись issue templates.
- Убедиться, что Discussions routes всё ещё ведут пользователей в Q&A, Ideas и DAW Compatibility.
