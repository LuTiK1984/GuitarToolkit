from __future__ import annotations

import argparse
import json
import math
import random
from collections import Counter
from pathlib import Path

import torch

from context_v3 import build_context_tokens
from model import MelodyPhraseTransformer, MelodyVocabulary
from train import TrainConfig, load_checkpoint


PROMPTS = [
    ("STYLE_METAL", "MODE_NATURAL_MINOR", "MOOD_DARK", "METER_4_4", 2, "i,VI", "<BOS>,D:1:4:1/8,D:b3:4:1/8"),
    ("STYLE_METAL", "MODE_PHRYGIAN", "MOOD_TENSE", "METER_4_4", 1, "i,bII", "<BOS>,D:1:4:1/16,D:b2:4:1/16"),
    ("STYLE_ROCK", "MODE_MAJOR", "MOOD_EPIC", "METER_4_4", 2, "I,V,vi,IV", "<BOS>,D:1:4:1/8,D:5:4:1/8"),
    ("STYLE_POP", "MODE_MAJOR", "MOOD_BRIGHT", "METER_4_4", 2, "I,V,vi,IV", "<BOS>,D:1:4:1/4,D:3:4:1/8"),
    ("STYLE_AMBIENT", "MODE_DORIAN", "MOOD_CALM", "METER_3_4", 2, "i,IV", "<BOS>,D:5:3:1/4,R:1/8"),
    ("STYLE_BLUES", "MODE_MAJOR", "MOOD_TENSE", "METER_4_4", 1, "I,IV,V", "<BOS>,D:1:4:1/8,D:b3:4:1/8"),
    ("STYLE_ROCK", "MODE_NATURAL_MINOR", "MOOD_DARK", "METER_6_8", 2, "i,VII,VI", "<BOS>,D:1:4:1/8,D:b7:3:1/8"),
    ("STYLE_AMBIENT", "MODE_MAJOR", "MOOD_CALM", "METER_4_4", 4, "I,vi,IV", "<BOS>,R:1/4,D:5:3:1/2"),
    ("STYLE_POP", "MODE_DORIAN", "MOOD_BRIGHT", "METER_3_4", 1, "i,IV", "<BOS>,D:1:4:1/8,D:2:4:1/8"),
    ("STYLE_METAL", "MODE_HARMONIC_MINOR", "MOOD_EPIC", "METER_4_4", 2, "i,V", "<BOS>,D:1:4:1/16,D:7:4:1/16"),
]

MODE_DEGREES = {
    "MODE_MAJOR": {"1", "2", "3", "4", "5", "6", "7", "8"},
    "MODE_NATURAL_MINOR": {"1", "2", "b3", "4", "5", "b6", "b7", "8"},
    "MODE_DORIAN": {"1", "2", "b3", "4", "5", "6", "b7", "8"},
    "MODE_PHRYGIAN": {"1", "b2", "b3", "4", "5", "b6", "b7", "8"},
    "MODE_HARMONIC_MINOR": {"1", "2", "b3", "4", "5", "b6", "7", "8"},
}

MOOD_DEGREES = {
    "MOOD_DARK": {"1", "b3", "4", "5", "b6", "b7"},
    "MOOD_EPIC": {"1", "4", "5", "6", "b7", "8"},
    "MOOD_BRIGHT": {"1", "2", "3", "5", "6", "8"},
    "MOOD_CALM": {"1", "2", "3", "5", "6", "b7"},
    "MOOD_TENSE": {"b2", "b3", "#4", "5", "b6", "7", "b7"},
}

STYLE_DURATIONS = {
    "STYLE_METAL": {"1/16", "1/8"},
    "STYLE_ROCK": {"1/16", "1/8", "1/4"},
    "STYLE_POP": {"1/8", "1/4"},
    "STYLE_AMBIENT": {"1/4", "1/2"},
    "STYLE_BLUES": {"1/8", "1/4"},
}

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


def evaluate(args: argparse.Namespace) -> None:
    prompts = build_prompts(args.generated_prompts, args.seed)
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

    output_ids = torch.tensor(vocab.output_token_ids, dtype=torch.long)
    rows = []
    entropy_values = []
    top3_values = []
    top1_tokens = []
    musical_mass = []
    mood_mass = []
    style_mass = []
    rhythm_mass = []
    interval_mass = []
    octave_mass = []
    anti_repeat_mass = []
    anti_rest_mass = []
    anti_duration_mass = []
    top1_musical_hits = 0
    top1_mood_hits = 0
    top1_style_hits = 0

    for prompt_index, (style, mode, mood, meter, bars, progression, previous) in enumerate(prompts):
        previous_tokens = split_csv(previous)
        profile, density, contour, note_count, section, position = prompt_context(style, mood, bars, previous_tokens, prompt_index)
        context = build_context_tokens(
            known_tokens=set(vocab.id_to_token),
            max_context_length=config.max_context_length,
            style=style,
            mode=mode,
            mood=mood,
            meter=meter,
            bars=bars,
            progression=split_csv(progression),
            profile=profile,
            density=density,
            contour=contour,
            note_count=note_count,
            section=section,
            position=position,
        )
        context = (context + ["<PAD>"] * config.max_context_length)[: config.max_context_length]
        context_ids = torch.tensor([[vocab.encode(token) for token in context]], dtype=torch.long)
        previous_ids = torch.tensor([[vocab.encode(token) for token in previous_tokens]], dtype=torch.long)

        with torch.no_grad():
            probabilities = model(context_ids, previous_ids).index_select(1, output_ids).softmax(dim=1)[0]

        top = probabilities.topk(k=min(args.top_k, probabilities.numel()))
        top_tokens = [vocab.id_to_token[vocab.output_token_ids[int(index)]] for index in top.indices]
        top_probabilities = [float(value) for value in top.values]
        token_scores = score_tokens(vocab, probabilities, mode, mood, style, previous_tokens)

        entropy = -sum(float(value) * math.log(max(float(value), 1e-9)) for value in probabilities)
        entropy_values.append(entropy)
        top3_values.append(sum(top_probabilities[:3]))
        top1_tokens.append(top_tokens[0])
        musical_mass.append(token_scores["musical_mass"])
        mood_mass.append(token_scores["mood_mass"])
        style_mass.append(token_scores["style_mass"])
        rhythm_mass.append(token_scores["rhythm_mass"])
        interval_mass.append(token_scores["interval_mass"])
        octave_mass.append(token_scores["octave_mass"])
        anti_repeat_mass.append(token_scores["anti_repeat_mass"])
        anti_rest_mass.append(token_scores["anti_rest_mass"])
        anti_duration_mass.append(token_scores["anti_duration_mass"])

        top1_musical_hits += int(is_degree_allowed(top_tokens[0], MODE_DEGREES[mode]))
        top1_mood_hits += int(is_degree_allowed(top_tokens[0], MOOD_DEGREES[mood]))
        top1_style_hits += int(is_duration_allowed(top_tokens[0], STYLE_DURATIONS[style]))

        rows.append(
            {
                "style": style,
                "mode": mode,
                "mood": mood,
                "meter": meter,
                "bars": bars,
                "progression": split_csv(progression),
                "profile": profile,
                "density": density,
                "contour": contour,
                "note_count": note_count,
                "section": section,
                "position": position,
                "previous": previous_tokens,
                "top": [
                    {"token": token, "probability": round(probability, 4)}
                    for token, probability in zip(top_tokens, top_probabilities)
                ],
                "entropy": round(entropy, 4),
            }
        )

    prompt_count = len(prompts)
    distinct_top1_percent = len(set(top1_tokens)) / prompt_count * 100.0
    avg_entropy = mean(entropy_values)
    avg_top3_mass = mean(top3_values)
    diversity_score = clamp((avg_entropy / 3.4) * 72.0 + (distinct_top1_percent / 100.0) * 28.0, 0.0, 100.0)
    musicality_score = mean(musical_mass) * 100.0
    mood_fit_score = mean(mood_mass) * 100.0
    style_fit_score = mean(style_mass) * 100.0
    rhythm_score = mean(rhythm_mass) * 100.0
    interval_score = mean(interval_mass) * 100.0
    octave_score = mean(octave_mass) * 100.0
    anti_repeat_score = mean(anti_repeat_mass) * 100.0
    anti_rest_score = mean(anti_rest_mass) * 100.0
    anti_duration_score = mean(anti_duration_mass) * 100.0
    phrase_life_score = anti_repeat_score * 0.45 + anti_rest_score * 0.30 + anti_duration_score * 0.25
    confidence_balance = clamp(100.0 - abs(avg_top3_mass - 0.68) * 180.0, 0.0, 100.0)
    overall = (
        musicality_score * 0.20
        + interval_score * 0.15
        + octave_score * 0.09
        + phrase_life_score * 0.16
        + mood_fit_score * 0.14
        + style_fit_score * 0.10
        + rhythm_score * 0.06
        + diversity_score * 0.07
        + confidence_balance * 0.03
    )

    summary = {
        "checkpoint": args.checkpoint,
        "prompt_count": prompt_count,
        "generated_prompt_count": max(0, prompt_count - len(PROMPTS)),
        "overall_score_percent": round(overall, 1),
        "diversity_score_percent": round(diversity_score, 1),
        "musicality_score_percent": round(musicality_score, 1),
        "mood_fit_score_percent": round(mood_fit_score, 1),
        "style_fit_score_percent": round(style_fit_score, 1),
        "rhythm_score_percent": round(rhythm_score, 1),
        "interval_score_percent": round(interval_score, 1),
        "octave_score_percent": round(octave_score, 1),
        "phrase_life_score_percent": round(phrase_life_score, 1),
        "anti_repeat_score_percent": round(anti_repeat_score, 1),
        "anti_rest_score_percent": round(anti_rest_score, 1),
        "anti_duration_score_percent": round(anti_duration_score, 1),
        "confidence_balance_percent": round(confidence_balance, 1),
        "distinct_top1_percent": round(distinct_top1_percent, 1),
        "avg_entropy": round(avg_entropy, 4),
        "avg_top3_mass": round(avg_top3_mass, 4),
        "top1_musical_hit_percent": round(top1_musical_hits / prompt_count * 100.0, 1),
        "top1_mood_hit_percent": round(top1_mood_hits / prompt_count * 100.0, 1),
        "top1_style_hit_percent": round(top1_style_hits / prompt_count * 100.0, 1),
        "top1_tokens": top1_tokens,
        "top1_token_counts": dict(Counter(top1_tokens)),
    }
    print(json.dumps({"summary": summary, "prompts": rows}, ensure_ascii=False, indent=2))


def score_tokens(vocab: MelodyVocabulary, probabilities: torch.Tensor, mode: str, mood: str, style: str, previous: list[str]) -> dict[str, float]:
    musical = 0.0
    mood_fit = 0.0
    style_fit = 0.0
    rhythm_fit = 0.0
    interval_fit = 0.0
    octave_fit = 0.0
    anti_repeat = 0.0
    anti_rest = 0.0
    anti_duration = 0.0
    previous_midi = last_note_midi(previous)
    repeat_count = trailing_note_repeat_count(previous)
    rest_streak = trailing_rest_count(previous)
    previous_duration = last_duration(previous)
    duration_streak = trailing_duration_count(previous)
    for probability, token_id in zip(probabilities, vocab.output_token_ids):
        token = vocab.id_to_token[token_id]
        value = float(probability)
        if is_degree_allowed(token, MODE_DEGREES[mode]):
            musical += value
        if is_degree_allowed(token, MOOD_DEGREES[mood]):
            mood_fit += value
        if is_duration_allowed(token, STYLE_DURATIONS[style]):
            style_fit += value
        if token == "<EOS>" or token.startswith("R:") or token.startswith("D:"):
            rhythm_fit += value
        if is_interval_good(token, previous_midi):
            interval_fit += value
        if is_octave_good(token, mood):
            octave_fit += value
        if not is_repeated_note_spam(token, previous_midi, repeat_count):
            anti_repeat += value
        if not is_rest_spam(token, rest_streak):
            anti_rest += value
        if not is_duration_spam(token, previous_duration, duration_streak):
            anti_duration += value
    return {
        "musical_mass": musical,
        "mood_mass": mood_fit,
        "style_mass": style_fit,
        "rhythm_mass": rhythm_fit,
        "interval_mass": interval_fit,
        "octave_mass": octave_fit,
        "anti_repeat_mass": anti_repeat,
        "anti_rest_mass": anti_rest,
        "anti_duration_mass": anti_duration,
    }


def build_prompts(generated_count: int, seed: int) -> list[tuple[str, str, str, str, int, str, str]]:
    prompts = list(PROMPTS)
    if generated_count <= 0:
        return prompts

    rng = random.Random(seed)
    styles = list(STYLE_DURATIONS)
    modes = list(MODE_DEGREES)
    moods = list(MOOD_DEGREES)
    meters = ["METER_4_4", "METER_3_4", "METER_6_8"]
    progressions_by_mode = {
        "MODE_MAJOR": ["I,V,vi,IV", "I,IV,V", "vi,IV,I,V", "I,iii,IV,V"],
        "MODE_NATURAL_MINOR": ["i,VI,VII", "i,iv,V", "i,VII,VI,VII", "i,bVI,bVII"],
        "MODE_DORIAN": ["i,IV", "i,ii,IV", "i,bVII,IV", "i,vi,IV"],
        "MODE_PHRYGIAN": ["i,bII", "i,bII,VII", "i,bII,VI", "i,VII,bII"],
        "MODE_HARMONIC_MINOR": ["i,V", "i,iv,V", "i,bVI,V", "i,VII,V"],
    }

    for _ in range(generated_count):
        style = rng.choice(styles)
        mode = rng.choice(modes)
        mood = rng.choice(moods)
        meter = rng.choice(meters)
        bars = rng.choice([1, 2, 4])
        progression = rng.choice(progressions_by_mode[mode])
        previous = build_previous_prompt(mode, mood, style, rng)
        prompts.append((style, mode, mood, meter, bars, progression, previous))

    return prompts


def build_previous_prompt(mode: str, mood: str, style: str, rng: random.Random) -> str:
    degrees = sorted(MODE_DEGREES[mode] & MOOD_DEGREES[mood])
    if not degrees:
        degrees = sorted(MODE_DEGREES[mode])
    durations = sorted(STYLE_DURATIONS[style])
    octave = rng.choice([3, 4, 4, 4, 5])
    first = rng.choice(degrees)
    second = rng.choice(degrees)
    first_duration = rng.choice(durations)
    second_duration = rng.choice(durations)
    if rng.random() < 0.18:
        return f"<BOS>,R:{first_duration},D:{second}:{octave}:{second_duration}"
    return f"<BOS>,D:{first}:{octave}:{first_duration},D:{second}:{octave}:{second_duration}"


def prompt_context(
    style: str,
    mood: str,
    bars: int,
    previous_tokens: list[str],
    index: int,
) -> tuple[str, str, str, str, str, str]:
    if style == "STYLE_AMBIENT":
        profile = "PROFILE_AMBIENT"
    elif style == "STYLE_METAL" and index % 2 == 0:
        profile = "PROFILE_RIFF"
    elif index % 5 == 0:
        profile = "PROFILE_CALL_RESPONSE"
    elif index % 3 == 0:
        profile = "PROFILE_HOOK"
    else:
        profile = "PROFILE_BALANCED"

    density = "DENSITY_SPARSE" if bars >= 4 or profile == "PROFILE_AMBIENT" else "DENSITY_MEDIUM"
    if profile == "PROFILE_RIFF":
        density = "DENSITY_DENSE"
    contour = "CONTOUR_RISE" if mood in {"MOOD_EPIC", "MOOD_BRIGHT"} else "CONTOUR_FALL" if mood in {"MOOD_DARK", "MOOD_CALM"} else "CONTOUR_ARCH"
    note_count = "NOTE_COUNT_6_8" if density == "DENSITY_SPARSE" else "NOTE_COUNT_12_16" if density == "DENSITY_DENSE" else "NOTE_COUNT_8_12"
    event_count = sum(1 for token in previous_tokens if token.startswith("D:") or token.startswith("R:"))
    section = "SECTION_BEGIN" if event_count <= 2 else "SECTION_MIDDLE"
    position = "POS_BAR_START" if event_count <= 1 else "POS_WEAK_BEAT"
    return profile, density, contour, note_count, section, position


def is_degree_allowed(token: str, allowed: set[str]) -> bool:
    if token == "<EOS>" or token.startswith("R:"):
        return True
    parts = token.split(":")
    return len(parts) >= 3 and parts[0] == "D" and parts[1] in allowed


def is_duration_allowed(token: str, allowed: set[str]) -> bool:
    if token == "<EOS>":
        return True
    parts = token.split(":")
    duration = parts[-1] if len(parts) >= 2 else ""
    return duration in allowed


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
    last_midi = last_note_midi(tokens)
    if last_midi is None:
        return 0

    count = 0
    for token in reversed(tokens):
        parsed = parse_note(token)
        if parsed is None:
            if token.startswith("R:"):
                break
            continue
        if parsed[1] != last_midi:
            break
        count += 1
    return count


def trailing_rest_count(tokens: list[str]) -> int:
    count = 0
    for token in reversed(tokens):
        if token.startswith("R:"):
            count += 1
            continue
        if token not in {"<BOS>", "<PAD>", "<UNK>"}:
            break
    return count


def last_duration(tokens: list[str]) -> str | None:
    for token in reversed(tokens):
        duration = token.split(":")[-1]
        if duration in {"1/16", "1/8", "1/4", "1/2"}:
            return duration
    return None


def trailing_duration_count(tokens: list[str]) -> int:
    duration = last_duration(tokens)
    if duration is None:
        return 0

    count = 0
    for token in reversed(tokens):
        if token in {"<BOS>", "<PAD>", "<UNK>"}:
            continue
        if token.split(":")[-1] != duration:
            break
        count += 1
    return count


def is_interval_good(token: str, previous_midi: int | None) -> bool:
    parsed = parse_note(token)
    if previous_midi is None or parsed is None:
        return True
    return abs(parsed[1] - previous_midi) <= 12


def is_octave_good(token: str, mood: str) -> bool:
    parsed = parse_note(token)
    if parsed is None:
        return True
    octave = parsed[0]
    if octave == 4:
        return True
    if octave == 5 and mood in {"MOOD_EPIC", "MOOD_TENSE"}:
        return True
    if octave == 3 and mood in {"MOOD_DARK", "MOOD_CALM"}:
        return True
    return False


def is_repeated_note_spam(token: str, previous_midi: int | None, repeat_count: int) -> bool:
    parsed = parse_note(token)
    if previous_midi is None or parsed is None or repeat_count < 2:
        return False
    return parsed[1] == previous_midi


def is_rest_spam(token: str, rest_streak: int) -> bool:
    return rest_streak >= 1 and token.startswith("R:")


def is_duration_spam(token: str, previous_duration: str | None, duration_streak: int) -> bool:
    if previous_duration is None or duration_streak < 3:
        return False
    duration = token.split(":")[-1]
    return duration == previous_duration


def split_csv(value: str) -> list[str]:
    return [part.strip() for part in value.split(",") if part.strip()]


def mean(values: list[float]) -> float:
    return sum(values) / max(len(values), 1)


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def ensure_vocabulary_matches(checkpoint: dict, vocab: MelodyVocabulary, vocab_path: str) -> None:
    checkpoint_size = int(checkpoint.get("vocabulary_size", -1))
    if checkpoint_size != len(vocab.id_to_token):
        raise RuntimeError(
            "Checkpoint vocabulary size does not match the selected vocab. "
            f"checkpoint={checkpoint_size} vocab={len(vocab.id_to_token)} vocab_path={vocab_path}. "
            "Use vocab.json for old v1 checkpoints, vocab_v2.json for v2, or train/export a fresh v3 checkpoint with vocab_v3.json."
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", default="runs/melody_phrase_transformer/best_model.pt")
    parser.add_argument("--vocab", default="vocab_v3.json")
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument("--generated-prompts", type=int, default=120)
    parser.add_argument("--seed", type=int, default=1984)
    return parser.parse_args()


if __name__ == "__main__":
    evaluate(parse_args())
