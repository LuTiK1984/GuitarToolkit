# MelodyPhraseTransformer

This is the first local training skeleton for the future GuitarToolkit melody/riff model.

The model does not generate audio. It generates symbolic phrase tokens, for example:

```text
D:b3:4:1/8
R:1/8
```

Meaning:

- `D:b3:4:1/8` - play scale degree `b3` in octave `4` for one eighth note;
- `R:1/8` - rest for one eighth note.

GuitarToolkit will later map these tokens to real notes, fretboard positions, playback, and MIDI/export tools.

## Dataset format

JSONL: one phrase per line.

```json
{"style":"STYLE_METAL","mode":"MODE_NATURAL_MINOR","mood":"MOOD_DARK","meter":"METER_4_4","bars":2,"profile":"PROFILE_RIFF","density":"DENSITY_MEDIUM","contour":"CONTOUR_ARCH","note_count":"NOTE_COUNT_8_12","progression":["i","VI"],"tokens":["<BOS>","D:1:4:1/8","D:b3:4:1/8","<EOS>"],"positions":["POS_OFFGRID","POS_BAR_START","POS_WEAK_BEAT","POS_BAR_END"],"sections":["SECTION_BEGIN","SECTION_BEGIN","SECTION_BEGIN","SECTION_END"]}
```

Important fields:

- `style` - broad musical style.
- `mode` - scale/mode used for degree mapping.
- `mood` - emotional target.
- `meter` - phrase meter: `METER_4_4`, `METER_3_4`, or `METER_6_8`.
- `bars` - phrase length: `1`, `2`, or `4`.
- `profile` - phrase behavior target: balanced, sparse, hook, riff, ambient, or call/response.
- `density` - expected phrase density.
- `contour` - melodic shape: rise, fall, arch, or static.
- `note_count` - coarse note-count bucket.
- `progression` - optional chord context.
- `tokens` - phrase tokens with `<BOS>` and `<EOS>`.
- `positions` and `sections` - per-token musical position hints used as dynamic training context.

## First smoke run

```powershell
cd tools\ml\melody_phrase_transformer
python generate_synthetic_dataset.py --output synthetic_melody_dataset.jsonl --count 5000
python validate_dataset.py --dataset synthetic_melody_dataset.jsonl
python train.py --dataset synthetic_melody_dataset.jsonl --vocab vocab_v3.json --epochs 10 --batch-size 256 --amp
python inspect_checkpoint.py --checkpoint runs\melody_phrase_transformer\best_model.pt
python export_onnx.py --checkpoint runs\melody_phrase_transformer\best_model.pt
```

If training is slow, try:

```powershell
python train.py --dataset synthetic_melody_dataset.jsonl --vocab vocab_v3.json --epochs 10 --batch-size 1024 --learning-rate 0.0003 --amp
```

If CUDA memory is tight, lower batch size:

```powershell
python train.py --dataset synthetic_melody_dataset.jsonl --vocab vocab_v3.json --epochs 10 --batch-size 128
```

## How to read progress

- `train_loss` should go down during training.
- `val_loss` should go down or stabilize.
- `acc` means exact next-token hit rate.
- `top3` means the correct token was among the three most likely model answers.

For melodies, exact `acc` is naturally harder than for progressions because there are more plausible answers. A useful early model should show improving `val_loss`, decent `top3`, and inspection output that stays inside musical phrase tokens instead of random symbols.

## What counts as a first win

This version is still trained on synthetic phrases, so the first win is controlled musical behavior rather than finished composer quality:

- dataset validates;
- training finishes;
- checkpoint inspection returns phrase tokens;
- ONNX export succeeds;
- generated durations can be checked against meter/bar length.
- evaluation shows healthy phrase-life, interval, octave, mood, and style scores.

The ONNX model is connected to the Melody tab through the ML Trainer install action. Further work focuses on dataset quality, phrase structure, octave/interval behavior, and more reliable musical evaluation.
