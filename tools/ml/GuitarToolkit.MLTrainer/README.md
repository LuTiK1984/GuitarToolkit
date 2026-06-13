# GuitarToolkit ML Trainer

Windows Forms utility for local GuitarToolkit model training.

## What It Does

The trainer wraps the Python model scripts in one window:

- generates and validates JSONL datasets;
- previews dataset rows;
- starts and stops training runs;
- shows in-epoch progress and epoch metrics;
- inspects a checkpoint on a single prompt;
- evaluates model quality with comparable summary metrics;
- runs Melody Transformer generations with several competing candidates;
- exports ONNX files for the main app;
- generates a quick WAV preview for Melody Transformer checkpoints;
- opens the relevant `runs` and tools folders.

## Model Tabs

`Progression GRU/LSTM` trains the Inspiration Engine progression model. It predicts the next chord degree from style, mode, mood, and previous degrees.

`Melody Transformer` trains the short melody/riff model. The v3 workflow predicts note, octave, rest, and duration tokens from style, mode, mood, meter, bar length, progression context, phrase profile, density, melodic contour, expected note count, phrase section, beat position, and previous phrase tokens.

## How To Read Training

`train_loss` is how well the model fits the training dataset. `val_loss` is the more important number because it checks held-out examples. If `train_loss` keeps falling but `val_loss` stops improving, the model has probably reached a plateau for that dataset.

`acc` is exact next-token accuracy. Melody accuracy is naturally lower than progression accuracy because many different next notes can be musically valid.

`top3` means the right answer was inside the model's three strongest guesses. For melody, this is often the friendlier quality signal.

## Practical Starting Points

For RTX 3060 Ti:

- progression model: batch `256`, learning rate `0.00005-0.0001` for fine-tune;
- melody v3 model: batch `2048` on RTX 3060 Ti when VRAM allows it, learning rate `0.0002-0.0003`, label smoothing around `0.015-0.02`, AMP enabled;
- if CUDA runs out of memory, lower batch to `1024`, then `512`; if GPU use is low and VRAM is free, increase batch gradually;
- keep `Data workers` at `2` for the current picklable loader; use `0` only if your Python build has multiprocessing issues;
- use `Resume` to continue from an existing checkpoint;
- use optimizer reset when changing dataset profile or learning rate.

For the melody model, the extra penalty fields are a small musical judge on top of normal next-token learning:

- mode penalty: punishes probability mass outside the selected mode;
- mood penalty: punishes notes that do not fit the selected mood color;
- style penalty: punishes durations that do not fit the selected style;
- entropy penalty: punishes an overly smeared distribution so the model becomes more decisive;
- interval penalty: punishes too much probability on random wide jumps between neighboring notes;
- octave penalty: punishes octave choices that do not fit the phrase mood/register.
- repeat penalty: punishes repeating the same note too much;
- rest penalty: punishes chains of pauses;
- duration penalty: punishes long runs of one rhythmic value.

Keep these values gentle. Good first v3 values are mode `0.12`, mood `0.08`, style `0.06`, entropy `0.015`, interval `0.10`, octave `0.06`, repeat `0.12`, rest `0.10`, duration `0.06`. If the model becomes too repetitive, lower entropy penalty first.

The architecture fields are exposed for experiments. The v3 default `embedding 128`, `heads 4`, `layers 3`, `feedforward 512`, `dropout 0.10` is the current quality baseline. For a faster smoke run, use `layers 1-2`, `feedforward 128-384`, and a small dataset.

Use `Preview WAV` after training to generate and open a simple synthesized phrase from the selected checkpoint. It is not the final app playback engine, but it is enough to hear whether the phrase logic is alive.

Important: v1 melody checkpoints use `vocab.json`, v2 octave-aware checkpoints use `vocab_v2.json`, and v3 contextual checkpoints use `vocab_v3.json`. If preview/export reports a checkpoint/vocab size mismatch, the selected checkpoint and vocabulary belong to different generations and should not be mixed.

## Melody Generations

`Запустить поколение` is a small tournament mode for Melody Transformer. It starts several candidates from the same parent checkpoint, trains each one with slightly different learning and penalty settings, evaluates them, and saves three champions:

- `theoretical_best_model.pt` - strongest rule-following candidate;
- `balanced_best_model.pt` - best overall candidate and the default parent for the next generation;
- `art_house_best_model.pt` - most interesting/diverse candidate that still stays musical enough.

After a generation finishes, the trainer automatically puts the balanced champion into `Resume` and `Checkpoint`, then increments the generation number. This lets the next click continue the line from the current best balanced model.

## Run

From the repository root:

```powershell
dotnet run --project tools\ml\GuitarToolkit.MLTrainer\GuitarToolkit.MLTrainer.csproj --configuration Debug
```

Install Python dependencies from the model folder you want to train:

```powershell
python -m pip install -r tools\ml\progression_next_token\requirements.txt
python -m pip install -r tools\ml\melody_phrase_transformer\requirements.txt
```
