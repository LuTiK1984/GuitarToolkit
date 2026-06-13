from __future__ import annotations

import argparse
import json
import math
import random
import wave
from pathlib import Path

import torch

from context_v3 import build_context_tokens
from model import MelodyPhraseTransformer, MelodyVocabulary
from train import TrainConfig, load_checkpoint


DEGREE_SEMITONES = {
    "1": 0,
    "b2": 1,
    "2": 2,
    "b3": 3,
    "3": 4,
    "4": 5,
    "#4": 6,
    "5": 7,
    "b6": 8,
    "6": 9,
    "b7": 10,
    "7": 11,
    "8": 12,
}

DURATION_BEATS = {
    "1/16": 0.25,
    "1/8": 0.5,
    "1/4": 1.0,
    "1/2": 2.0,
}


def generate(args: argparse.Namespace) -> None:
    rng = random.Random(args.seed)
    torch.manual_seed(args.seed)
    vocab = MelodyVocabulary.load(args.vocab)
    checkpoint = load_checkpoint(Path(args.checkpoint))
    ensure_vocabulary_matches(checkpoint, vocab, args.vocab)
    config = TrainConfig(**checkpoint["config"])
    model = MelodyPhraseTransformer(
        vocabulary_size=len(vocab.id_to_token),
        embedding_size=config.embedding_size,
        heads=config.heads,
        layers=config.layers,
        feedforward_size=config.feedforward_size,
        dropout=0.0,
        max_sequence_length=config.max_sequence_length + config.max_context_length,
    )
    model.load_state_dict(checkpoint["model_state"])
    model.eval()

    context = build_context_tokens(
        known_tokens=set(vocab.id_to_token),
        max_context_length=config.max_context_length,
        style=args.style,
        mode=args.mode,
        mood=args.mood,
        meter=args.meter,
        bars=args.bars,
        progression=split_csv(args.progression),
        profile=args.profile,
        density=args.density,
        contour=args.contour,
        note_count=args.note_count,
        section=args.section,
        position=args.position,
    )
    context = (context + ["<PAD>"] * config.max_context_length)[: config.max_context_length]
    generated = split_csv(args.previous)
    output_ids = torch.tensor(vocab.output_token_ids, dtype=torch.long)

    for _ in range(args.max_tokens):
        context_ids = torch.tensor([[vocab.encode(token) for token in context]], dtype=torch.long)
        previous_ids = torch.tensor([[vocab.encode(token) for token in generated[-config.max_sequence_length :]]], dtype=torch.long)
        with torch.no_grad():
            logits = model(context_ids, previous_ids).index_select(1, output_ids)[0]
            logits = apply_generation_guards(logits, generated, vocab)
            probabilities = sample_distribution(logits, args.temperature, args.top_k)
            pick = torch.multinomial(probabilities, num_samples=1).item()
        token = vocab.id_to_token[vocab.output_token_ids[pick]]
        generated.append(token)
        if token == "<EOS>":
            break

    phrase = [token for token in generated if token not in {"<BOS>", "<EOS>"}]
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    write_wav(output, phrase, args.bpm, args.root_frequency, args.sample_rate)
    print(json.dumps({"tokens": generated, "wav": str(output)}, ensure_ascii=False, indent=2))


def sample_distribution(logits: torch.Tensor, temperature: float, top_k: int) -> torch.Tensor:
    temperature = max(0.05, temperature)
    top_k = max(1, min(top_k, logits.numel()))
    values, indices = logits.topk(top_k)
    probabilities = torch.zeros_like(logits)
    probabilities[indices] = torch.softmax(values / temperature, dim=0)
    return probabilities


def apply_generation_guards(logits: torch.Tensor, previous_tokens: list[str], vocab: MelodyVocabulary) -> torch.Tensor:
    adjusted = logits.clone()
    previous_midi = last_note_midi(previous_tokens)
    repeat_count = trailing_note_repeat_count(previous_tokens)
    rest_streak = trailing_rest_count(previous_tokens)
    previous_duration = last_duration(previous_tokens)
    duration_streak = trailing_duration_count(previous_tokens)

    for index, token_id in enumerate(vocab.output_token_ids):
        token = vocab.id_to_token[token_id]
        penalty = 0.0
        parsed = parse_note(token)
        if previous_midi is not None and parsed is not None and parsed[1] == previous_midi:
            if repeat_count >= 2:
                penalty += 2.5
            elif repeat_count == 1:
                penalty += 0.8
        if rest_streak >= 1 and token.startswith("R:"):
            penalty += 2.0
        duration = token.split(":")[-1]
        if previous_duration is not None and duration_streak >= 3 and duration == previous_duration:
            penalty += 1.2
        if token == "<EOS>" and len([item for item in previous_tokens if item not in {"<BOS>", "<EOS>"}]) < 4:
            penalty += 3.0
        adjusted[index] -= penalty
    return adjusted


def ensure_vocabulary_matches(checkpoint: dict, vocab: MelodyVocabulary, vocab_path: str) -> None:
    checkpoint_size = int(checkpoint.get("vocabulary_size", -1))
    if checkpoint_size != len(vocab.id_to_token):
        raise RuntimeError(
            "Checkpoint vocabulary size does not match the selected vocab. "
            f"checkpoint={checkpoint_size} vocab={len(vocab.id_to_token)} vocab_path={vocab_path}. "
            "Use vocab.json for old v1 checkpoints, vocab_v2.json for v2, or train/export a fresh v3 checkpoint with vocab_v3.json."
        )


def write_wav(path: Path, tokens: list[str], bpm: int, root_frequency: float, sample_rate: int) -> None:
    samples: list[int] = []
    seconds_per_beat = 60.0 / bpm
    phase = 0.0

    for token in tokens:
        duration = token.split(":")[-1]
        seconds = DURATION_BEATS.get(duration, 0.5) * seconds_per_beat
        count = max(1, int(seconds * sample_rate))
        if token.startswith("R:"):
            samples.extend([0] * count)
            continue
        if not token.startswith("D:"):
            continue

        parts = token.split(":")
        degree = parts[1]
        octave = int(parts[2]) if len(parts) == 4 else 4
        frequency = root_frequency * (2.0 ** ((DEGREE_SEMITONES.get(degree, 0) + (octave - 4) * 12) / 12.0))
        for index in range(count):
            envelope = min(1.0, index / max(1, sample_rate * 0.01), (count - index) / max(1, sample_rate * 0.03))
            value = math.sin(phase) * 0.38 + math.sin(phase * 2.0) * 0.08
            samples.append(int(max(-1.0, min(1.0, value * envelope)) * 32767))
            phase += 2.0 * math.pi * frequency / sample_rate

    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(sample_rate)
        handle.writeframes(b"".join(sample.to_bytes(2, "little", signed=True) for sample in samples))


def split_csv(value: str) -> list[str]:
    return [part.strip() for part in value.split(",") if part.strip()]


def parse_note(token: str) -> tuple[int, int] | None:
    parts = token.split(":")
    if len(parts) == 4 and parts[0] == "D":
        octave = int(parts[2])
        return octave, octave * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    if len(parts) == 3 and parts[0] == "D":
        return 4, 4 * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    return None


def last_note_midi(tokens: list[str]) -> int | None:
    for token in reversed(tokens):
        parsed = parse_note(token)
        if parsed is not None:
            return parsed[1]
    return None


def trailing_note_repeat_count(tokens: list[str]) -> int:
    previous_midi = last_note_midi(tokens)
    if previous_midi is None:
        return 0
    count = 0
    for token in reversed(tokens):
        if token.startswith("R:"):
            break
        parsed = parse_note(token)
        if parsed is None:
            continue
        if parsed[1] != previous_midi:
            break
        count += 1
    return count


def trailing_rest_count(tokens: list[str]) -> int:
    count = 0
    for token in reversed(tokens):
        if token.startswith("R:"):
            count += 1
            continue
        if token not in {"<BOS>", "<EOS>"}:
            break
    return count


def last_duration(tokens: list[str]) -> str | None:
    for token in reversed(tokens):
        duration = token.split(":")[-1]
        if duration in DURATION_BEATS:
            return duration
    return None


def trailing_duration_count(tokens: list[str]) -> int:
    duration = last_duration(tokens)
    if duration is None:
        return 0
    count = 0
    for token in reversed(tokens):
        if token in {"<BOS>", "<EOS>"}:
            continue
        if token.split(":")[-1] != duration:
            break
        count += 1
    return count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", default="runs/melody_phrase_transformer/best_model.pt")
    parser.add_argument("--vocab", default="vocab_v3.json")
    parser.add_argument("--style", default="STYLE_METAL")
    parser.add_argument("--mode", default="MODE_NATURAL_MINOR")
    parser.add_argument("--mood", default="MOOD_DARK")
    parser.add_argument("--meter", default="METER_4_4")
    parser.add_argument("--bars", type=int, default=2)
    parser.add_argument("--progression", default="i,VI")
    parser.add_argument("--profile", default="PROFILE_BALANCED")
    parser.add_argument("--density", default="DENSITY_MEDIUM")
    parser.add_argument("--contour", default="CONTOUR_ARCH")
    parser.add_argument("--note-count", default="NOTE_COUNT_8_12")
    parser.add_argument("--section", default="SECTION_BEGIN")
    parser.add_argument("--position", default="POS_BAR_START")
    parser.add_argument("--previous", default="<BOS>")
    parser.add_argument("--output", default="runs/melody_preview.wav")
    parser.add_argument("--bpm", type=int, default=100)
    parser.add_argument("--temperature", type=float, default=0.85)
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument("--max-tokens", type=int, default=32)
    parser.add_argument("--root-frequency", type=float, default=220.0)
    parser.add_argument("--sample-rate", type=int, default=44100)
    parser.add_argument("--seed", type=int, default=1984)
    return parser.parse_args()


if __name__ == "__main__":
    generate(parse_args())
