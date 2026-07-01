# Inspiration Engine Model

[Русская версия](#ru)

This document describes the first ONNX-ready skeleton for the GuitarToolkit Inspiration Engine.

The goal is intentionally narrow: the model suggests symbolic musical material, and GuitarToolkit turns that material into playable or visible app data using the existing music-theory and synthesis code.

The first target model is:

```text
ProgressionNextTokenModel.onnx
```

It predicts the next roman-numeral chord token for a short progression.

## Runtime contract

The app-side contract lives in `GuitarToolkit.Core/Generation/`.

Important types:

- `GenerationRequest` - user intent: root, mode, style, mood, length, temperature, top-k, and optional seed tokens.
- `IProgressionNextTokenModel` - model interface used by the generation service.
- `ProgressionModelInput` - normalized symbolic input sent to the model adapter.
- `ProgressionModelOutput` - model probabilities for the next token.
- `GeneratedProgression` - validated result returned to the UI.
- `ProgressionInspirationService` - orchestration layer that samples tokens and maps them to GuitarToolkit chords.
- `OnnxProgressionModel` - ONNX Runtime adapter for an installed local model file.
- `DemoProgressionNextTokenModel` - deterministic fallback used while no trained model is installed.

The UI tab does not expect audio from the model. It expects roman-numeral tokens such as:

```text
i, VI, VII, iv, V
```

Then the app maps those tokens through:

```text
ProgressionBuilder -> ChordLibrary -> ChordPlayer -> IAudioPlayback
```

That means the model can stay small, symbolic, and safe. GuitarToolkit remains responsible for rendering chords, playback, tabs, fretboard views, and later note-generation tools.

## Runtime model files

Release builds include ready-to-use model files next to the app:

```text
models\ProgressionNextTokenModel.onnx
models\MelodyPhraseTransformer.onnx
models\MelodyPhraseTransformer.vocab.json
```

User-installed models still live in:

```text
%AppData%\GuitarToolkit\models\ProgressionNextTokenModel.onnx
%AppData%\GuitarToolkit\models\MelodyPhraseTransformer.onnx
%AppData%\GuitarToolkit\models\MelodyPhraseTransformer.vocab.json
```

At runtime the app prefers the user-installed model pair from `%AppData%`. If those files are missing, it falls back to the bundled release model in the application directory. If loading or inference fails, the app falls back to the deterministic demo generator and shows that status in the UI.

The helper script for the current training sandbox is:

```powershell
cd tools\ml\progression_next_token
powershell -ExecutionPolicy Bypass -File .\install_model.ps1 -Checkpoint runs\progression_rich_ft\best_model.pt -Python C:\Users\anikj\AppData\Local\Programs\Python\Python311\python.exe
```

That script exports the checkpoint to ONNX and copies it to the user runtime model folder, overriding the bundled release model without changing the app files.

Do not commit ad-hoc checkpoints, datasets, or experimental model files. Only curated release ONNX models with known provenance should be committed under the application model assets.

## Recommended first architecture

Start with a GRU or LSTM.

Suggested size:

```text
vocabulary: 100-300 tokens
embedding: 64-128
hidden size: 128-256
layers: 1-2
parameters: under 1-5 million
```

This is enough for short chord progressions and easy to export to ONNX. A small Transformer can come later after the token pipeline and evaluation loop are stable.

## Suggested vocabulary

Keep the first vocabulary boring and explicit:

```text
STYLE_METAL, STYLE_ROCK, STYLE_POP, STYLE_AMBIENT
MODE_MAJOR, MODE_NATURAL_MINOR, MODE_DORIAN, MODE_PHRYGIAN, MODE_HARMONIC_MINOR
MOOD_DARK, MOOD_EPIC, MOOD_BRIGHT, MOOD_CALM, MOOD_TENSE
I, ii, iii, IV, V, vi, vii°
i, ii°, III, iv, v, VI, VII
bII, bVI, bVII
<BOS>, <EOS>, <PAD>
```

Keep a stable `vocab.json` beside the training code and export metadata with the ONNX model. The C# adapter must use the same vocabulary order as the exported logits.

## Training workflow

1. Collect license-clean symbolic progression data.
2. Normalize every example to roman-numeral tokens plus metadata: style, mode, mood.
3. Split data into train/validation/test sets.
4. Train `ProgressionNextTokenModel` as next-token prediction.
5. Export to ONNX with fixed input names and output names.
6. Validate ONNX outputs against PyTorch outputs on the same examples.
7. Export and install the candidate with `install_model.ps1`.
8. Compare app behavior against checkpoint inspection examples.
9. Only ship a model file in a release when its license, dataset source, and quality notes are clear.

Recommended ONNX input names for the first adapter:

```text
style_id: int64[1]
mode_id: int64[1]
mood_id: int64[1]
previous_tokens: int64[1, sequence_length]
```

Recommended output:

```text
next_token_logits: float32[1, vocabulary_size]
```

The app will apply temperature and top-k sampling on the C# side.

## Validation rules

The model must not directly produce sound, file paths, commands, or UI markup. It should only produce symbolic tokens that the app validates before use.

The app must reject or remap unknown tokens before they reach playback. The current skeleton maps unsupported roman numerals to a safe diatonic fallback.

For melody generation, use the same rule: model returns symbolic notes, octaves, durations, and phrase tokens, and GuitarToolkit plays or displays them through internal engines.

---

<a id="ru"></a>

# Модель Inspiration Engine

[English version](#inspiration-engine-model)

Этот документ описывает первый ONNX-ready скелет для Inspiration Engine в GuitarToolkit.

Цель специально узкая: модель предлагает символический музыкальный материал, а GuitarToolkit превращает его в звучание и отображение уже существующими средствами программы.

Первая целевая модель:

```text
ProgressionNextTokenModel.onnx
```

Она предсказывает следующий аккордовый токен в римских ступенях.

## Контракт во время работы

Контракт со стороны приложения лежит в `GuitarToolkit.Core/Generation/`.

Главные типы:

- `GenerationRequest` - запрос пользователя: тоника, лад, стиль, настроение, длина, temperature, top-k и seed-ступени.
- `IProgressionNextTokenModel` - интерфейс модели для сервиса генерации.
- `ProgressionModelInput` - нормализованный символический вход для адаптера модели.
- `ProgressionModelOutput` - вероятности следующего токена.
- `GeneratedProgression` - проверенный результат для UI.
- `ProgressionInspirationService` - слой, который сэмплирует токены и мапит их на аккорды GuitarToolkit.
- `OnnxProgressionModel` - ONNX Runtime адаптер для установленной или bundled-модели прогрессий.
- `DemoProgressionNextTokenModel` - fallback, если ONNX-модель недоступна или не загрузилась.

Вкладка UI не ждет от модели аудио. Она ждет римские ступени:

```text
i, VI, VII, iv, V
```

Дальше приложение проводит их через:

```text
ProgressionBuilder -> ChordLibrary -> ChordPlayer -> IAudioPlayback
```

То есть модель остается маленькой, символической и безопасной. GuitarToolkit отвечает за аккорды, проигрывание, табы, гриф и будущие инструменты генерации нот.

## Где лежат runtime-модели

Релизные сборки включают готовые модели рядом с приложением:

```text
models\ProgressionNextTokenModel.onnx
models\MelodyPhraseTransformer.onnx
models\MelodyPhraseTransformer.vocab.json
```

Пользовательские модели для быстрой проверки после дообучения лежат в:

```text
%AppData%\GuitarToolkit\models\ProgressionNextTokenModel.onnx
%AppData%\GuitarToolkit\models\MelodyPhraseTransformer.onnx
%AppData%\GuitarToolkit\models\MelodyPhraseTransformer.vocab.json
```

Во время работы программа сначала пытается использовать пользовательскую модель из `%AppData%`. Если её нет, берёт bundled-модель из папки приложения. Если модель не загрузилась или inference завершился ошибкой, программа переключается на встроенный fallback-генератор и показывает этот статус в интерфейсе.

Не коммить случайные checkpoint-файлы, датасеты и экспериментальные модели. В репозиторий должны попадать только отобранные релизные ONNX-модели с понятным происхождением.

## Рекомендуемая первая архитектура

Начать лучше с GRU или LSTM.

Примерный размер:

```text
словарь: 100-300 токенов
embedding: 64-128
hidden size: 128-256
layers: 1-2
параметров: меньше 1-5 млн
```

Этого хватит для коротких прогрессий, модель быстро обучается и нормально экспортируется в ONNX. Маленький Transformer лучше добавить позже, когда пайплайн токенов и валидация уже стабильны.

## Первый словарь

На старте словарь лучше держать простым и явным:

```text
STYLE_METAL, STYLE_ROCK, STYLE_POP, STYLE_AMBIENT
MODE_MAJOR, MODE_NATURAL_MINOR, MODE_DORIAN, MODE_PHRYGIAN, MODE_HARMONIC_MINOR
MOOD_DARK, MOOD_EPIC, MOOD_BRIGHT, MOOD_CALM, MOOD_TENSE
I, ii, iii, IV, V, vi, vii°
i, ii°, III, iv, v, VI, VII
bII, bVI, bVII
<BOS>, <EOS>, <PAD>
```

Рядом с тренировочным кодом нужен стабильный `vocab.json`, а вместе с ONNX надо хранить метаданные. C# адаптер обязан использовать тот же порядок словаря, что и logits на выходе модели.

## Рабочий процесс обучения

1. Собрать лицензионно чистые символические прогрессии.
2. Нормализовать примеры в римские ступени плюс metadata: стиль, лад, настроение.
3. Разделить данные на train/validation/test.
4. Обучить `ProgressionNextTokenModel` на предсказание следующего токена.
5. Экспортировать в ONNX с фиксированными именами входов и выходов.
6. Сравнить ONNX-ответы с PyTorch-ответами на одинаковых примерах.
7. Добавить ONNX Runtime пакет и реализовать `OnnxProgressionModel`.
8. Добавить тесты на маппинг токенов, sampling, fallback и валидацию выхода модели.
9. Только после этого класть модель в релиз или документировать ручную установку.

Рекомендуемые имена входов ONNX:

```text
style_id: int64[1]
mode_id: int64[1]
mood_id: int64[1]
previous_tokens: int64[1, sequence_length]
```

Рекомендуемый выход:

```text
next_token_logits: float32[1, vocabulary_size]
```

Temperature и top-k лучше применять на стороне C#.

## Правила валидации

Модель не должна напрямую производить звук, пути к файлам, команды или UI-разметку. Она должна отдавать только символические токены, которые программа проверяет перед использованием.

Приложение должно отклонять или безопасно мапить неизвестные токены до проигрывания. Текущий скелет мапит неподдержанные римские ступени на безопасный диатонический fallback.

Для генерации мелодий правило такое же: модель возвращает символические ноты, октавы, длительности и фразовые токены, а GuitarToolkit проигрывает и отображает их внутренними движками.
