# GuitarToolkit

[![CI](https://github.com/LuTiK1984/GuitarToolkit/actions/workflows/ci.yml/badge.svg)](https://github.com/LuTiK1984/GuitarToolkit/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-7B5CD6)
![Windows](https://img.shields.io/badge/platform-Windows-7C6F96)
![VST3](https://img.shields.io/badge/plugin-VST3-CBA6F7)
![License](https://img.shields.io/badge/license-MIT-green)
![Tests](https://img.shields.io/badge/tests-78%2F78-brightgreen)

[Русская версия](#guitartoolkit-ru)

**GuitarToolkit** is an open-source Windows toolkit for guitar practice, writing, music theory, and DAW work. It ships as both a standalone WPF desktop app and a VST3 plugin with the same shared interface.

The project is built around practical guitar workflows: tune, practice with a metronome, inspect chords and scales, train intervals, build progressions, view Guitar Pro / MusicXML tabs, and sketch harmonic ideas with a local ONNX-based inspiration model.

## Download

Get the latest Desktop, VST3, and ML Trainer builds from [GitHub Releases](https://github.com/LuTiK1984/GuitarToolkit/releases).

For installation details, use the [Quick Start guide](docs/user/QUICK_START.md).

## Highlights

- Standalone Windows desktop app and VST3 plugin.
- Real-time tuner with alternate tunings and adjustable A4 reference.
- Metronome with tap tempo, quick tempos, visual beat feedback, and playback.
- Chord library with diagrams, voicings, formulas, favorites, and synthesized playback.
- Scale fretboard with modes, pentatonic, blues, harmonic minor, melodic minor, and chromatic views.
- Interval trainer with statistics and repeat/auto-advance workflow.
- Progression builder with presets, custom saved progressions, playback, and looping.
- Circle of fifths with related keys, diatonic chords, and common progressions.
- Tabs viewer for Guitar Pro / MusicXML files through alphaTab, including GP3, GP4, GP5/GPX, and MusicXML when supported by the importer.
- Inspiration Engine for local ONNX progression ideas, plus a separate ML Trainer utility for dataset generation, training, checkpoint comparison, ONNX export, and model installation.
- Melody sketchbook with local ONNX Transformer generation, backing progressions, synchronized playback, editable notes, and a three-octave piano.
- Dark and light themes.

## Screenshots

| Tuner | Inspiration Engine |
| --- | --- |
| ![GuitarToolkit tuner](docs/images/tuner-dark.png) | ![GuitarToolkit progression builder](docs/images/progressions-dark.png) |

| Chords | Tabs |
| --- | --- |
| ![GuitarToolkit chord library](docs/images/chords-dark.png) | ![GuitarToolkit tab viewer](docs/images/tabs-dark.png) |

More screenshots are stored in [docs/images](docs/images/).

## Project Status

| Target | Status |
| --- | --- |
| Desktop app | Usable on Windows 10/11 x64 |
| VST3 plugin | Usable, with DAW compatibility still being collected |
| Tabs viewer | Active development; alphaTab import limits may apply |
| Inspiration Engine | Local ONNX progression generation and playback |
| Melody sketchbook | Local ONNX short-phrase generation with editable playback |
| ML Trainer | Separate utility for training, evaluating, comparing, exporting, and installing local models |
| Platform | Windows-only for now |

## Documentation

- [Documentation index](docs/README.md)
- [Quick Start](docs/user/QUICK_START.md)
- [Supported DAWs](docs/user/SUPPORTED_DAWS.md)
- [FL Studio setup](docs/user/FL_STUDIO.md)
- [Reaper setup](docs/user/REAPER.md)
- [Known tab import issues](docs/user/KNOWN_TAB_IMPORT_ISSUES.md)
- [Inspiration Engine model notes](docs/maintainer/INSPIRATION_ENGINE_MODEL.md)
- [Roadmap](docs/maintainer/ROADMAP.md)
- [Release process](docs/releases/RELEASE_PROCESS.md)

## Development

Open `GuitarToolkit.sln` in Visual Studio 2022 and use `x64`.

Command line:

```powershell
dotnet restore GuitarToolkit.sln
dotnet build GuitarToolkit.sln --configuration Debug
dotnet test GuitarToolkit.sln --configuration Debug
```

Release package build:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 1.7.0 -Configuration Release
```

Architecture:

```text
GuitarToolkit.Core      DSP, music theory, engines, settings
GuitarToolkit.UI        Shared WPF controls used by Desktop and VST3
GuitarToolkit.Desktop   Standalone Windows app integration
GuitarToolkit.Plugin    VST3 plugin integration
GuitarToolkit.Tests     xUnit tests for Core behavior
tools/ml                Local model tooling and ML Trainer utility
```

## Community

- [Releases](https://github.com/LuTiK1984/GuitarToolkit/releases)
- [Discussions](https://github.com/LuTiK1984/GuitarToolkit/discussions)
- [Support](docs/user/SUPPORT.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)

Use Discussions for questions, ideas, and DAW compatibility reports. Use Issues for reproducible bugs and scoped development tasks.

## License

GuitarToolkit is released under the [MIT License](LICENSE).

VST is a trademark of Steinberg Media Technologies GmbH. Third-party dependency notes are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

<a id="guitartoolkit-ru"></a>

# GuitarToolkit RU

[English version](#guitartoolkit)

**GuitarToolkit** - open-source набор инструментов для гитаристов под Windows: практика, теория, наброски идей и работа в DAW. Проект поставляется как standalone WPF-приложение и как VST3-плагин с общим интерфейсом.

Идея простая: держать основные гитарные инструменты в одном месте. Настройка, метроном, аккорды, гаммы, интервалы, прогрессии, круг квинт, табулатуры Guitar Pro / MusicXML и локальная ONNX-модель для генерации идей.

## Скачать

Актуальные сборки Desktop, VST3 и ML Trainer находятся в [GitHub Releases](https://github.com/LuTiK1984/GuitarToolkit/releases).

Подробная установка описана в [Quick Start](docs/user/QUICK_START.md).

## Что есть

- Desktop-приложение для Windows и VST3-плагин для DAW.
- Тюнер в реальном времени, альтернативные строи и настройка эталона A4.
- Метроном с tap tempo, быстрыми темпами, визуальными долями и воспроизведением.
- Справочник аккордов с аппликатурами, формулами, избранным и синтезом звука.
- Гриф с гаммами, ладами, пентатоникой, блюзовой гаммой, гармоническим и мелодическим минором.
- Тренажер интервалов со статистикой, повтором и авто-переходом.
- Конструктор прогрессий с пресетами, сохранением, воспроизведением и циклом.
- Круг квинт с родственными тональностями, диатоническими аккордами и популярными прогрессиями.
- Просмотр табулатур через alphaTab: GP3, GP4, GP5/GPX и MusicXML, если конкретный файл поддерживается импортёром.
- Inspiration Engine: локальная ONNX-модель для генерации прогрессий.
- Отдельная утилита ML Trainer для датасетов, обучения, сравнения чекпоинтов, экспорта ONNX и установки модели в программу.
- Вкладка “Мелодии” как заготовка под будущую Transformer-модель коротких фраз.
- Тёмная и светлая темы.

## Скриншоты

| Тюнер | Inspiration Engine |
| --- | --- |
| ![Тюнер GuitarToolkit](docs/images/tuner-dark.png) | ![Генерация прогрессий GuitarToolkit](docs/images/progressions-dark.png) |

| Аккорды | Табы |
| --- | --- |
| ![Аккорды GuitarToolkit](docs/images/chords-dark.png) | ![Табы GuitarToolkit](docs/images/tabs-dark.png) |

Больше изображений лежит в [docs/images](docs/images/).

## Статус

| Цель | Статус |
| --- | --- |
| Desktop | Можно использовать на Windows 10/11 x64 |
| VST3 | Можно использовать, совместимость с DAW ещё собирается |
| Табы | Активная разработка; возможны ограничения alphaTab |
| Inspiration Engine | Экспериментальный локальный ONNX-процесс |
| ML Trainer | Отдельная утилита для обучения и сравнения моделей |
| Платформа | Пока только Windows |

## Документация

- [Индекс документации](docs/README.md)
- [Быстрый старт](docs/user/QUICK_START.md)
- [Поддерживаемые DAW](docs/user/SUPPORTED_DAWS.md)
- [FL Studio](docs/user/FL_STUDIO.md)
- [Reaper](docs/user/REAPER.md)
- [Известные ограничения импорта табов](docs/user/KNOWN_TAB_IMPORT_ISSUES.md)
- [Заметки по Inspiration Engine](docs/maintainer/INSPIRATION_ENGINE_MODEL.md)
- [Roadmap](docs/maintainer/ROADMAP.md)
- [Процесс релиза](docs/releases/RELEASE_PROCESS.md)

## Разработка

Откройте `GuitarToolkit.sln` в Visual Studio 2022 и используйте `x64`.

```powershell
dotnet restore GuitarToolkit.sln
dotnet build GuitarToolkit.sln --configuration Debug
dotnet test GuitarToolkit.sln --configuration Debug
```

Сборка релизных архивов:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 1.7.0 -Configuration Release
```

Коротко по структуре:

```text
GuitarToolkit.Core      DSP, теория музыки, движки, настройки
GuitarToolkit.UI        Общие WPF-контролы для Desktop и VST3
GuitarToolkit.Desktop   Standalone Windows-приложение
GuitarToolkit.Plugin    VST3-интеграция
GuitarToolkit.Tests     xUnit-тесты Core
tools/ml                ML-инструменты и ML Trainer
```

## Сообщество

- [Релизы](https://github.com/LuTiK1984/GuitarToolkit/releases)
- [Discussions](https://github.com/LuTiK1984/GuitarToolkit/discussions)
- [Поддержка](docs/user/SUPPORT.md)
- [Участие в разработке](CONTRIBUTING.md)
- [Безопасность](SECURITY.md)
- [Changelog](CHANGELOG.md)

В Discussions лучше писать вопросы, идеи и отчёты по DAW. В Issues лучше заводить воспроизводимые баги и конкретные задачи.

## Лицензия

GuitarToolkit распространяется по [MIT License](LICENSE).

VST является товарным знаком Steinberg Media Technologies GmbH. Сведения о сторонних зависимостях перечислены в [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
