from __future__ import annotations

import argparse
import json
import random
from pathlib import Path
from typing import TypeVar

from context_v3 import (
    BEAT_POSITION_TOKENS,
    CONTOUR_TOKENS,
    DENSITY_TOKENS,
    NOTE_COUNT_TOKENS,
    PHRASE_PROFILE_TOKENS,
    PHRASE_SECTION_TOKENS,
    note_count_token,
)


STYLES = ["STYLE_METAL", "STYLE_ROCK", "STYLE_POP", "STYLE_AMBIENT", "STYLE_BLUES"]
MODES = ["MODE_MAJOR", "MODE_NATURAL_MINOR", "MODE_DORIAN", "MODE_PHRYGIAN", "MODE_HARMONIC_MINOR"]
MOODS = ["MOOD_DARK", "MOOD_EPIC", "MOOD_BRIGHT", "MOOD_CALM", "MOOD_TENSE"]
METERS = ["METER_4_4", "METER_3_4", "METER_6_8"]
BARS = [1, 2, 4]
DURATIONS = ["1/16", "1/8", "1/4", "1/2"]
OCTAVES = [3, 4, 5]

MODE_DEGREES = {
    "MODE_MAJOR": ["1", "2", "3", "4", "5", "6", "7", "8"],
    "MODE_NATURAL_MINOR": ["1", "2", "b3", "4", "5", "b6", "b7", "8"],
    "MODE_DORIAN": ["1", "2", "b3", "4", "5", "6", "b7", "8"],
    "MODE_PHRYGIAN": ["1", "b2", "b3", "4", "5", "b6", "b7", "8"],
    "MODE_HARMONIC_MINOR": ["1", "2", "b3", "4", "5", "b6", "7", "8"],
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

STYLE_PROGRESSIONS = {
    "STYLE_METAL": [["i", "bII"], ["i", "VI", "VII"], ["i", "VI", "bII", "VII"]],
    "STYLE_ROCK": [["I", "V", "vi", "IV"], ["i", "VII", "VI"], ["I", "bVII", "IV"]],
    "STYLE_POP": [["I", "V", "vi", "IV"], ["I", "vi", "IV", "V"], ["vi", "IV", "I", "V"]],
    "STYLE_AMBIENT": [["I", "vi", "IV"], ["i", "III", "VII"], ["i", "bVII", "IV"]],
    "STYLE_BLUES": [["I", "IV", "V"], ["i", "iv", "v"], ["I", "bVII", "IV"]],
}

MOOD_CENTER = {
    "MOOD_DARK": ["1", "b3", "4", "5", "b6", "b7"],
    "MOOD_EPIC": ["1", "4", "5", "6", "b7", "8"],
    "MOOD_BRIGHT": ["1", "2", "3", "5", "6", "8"],
    "MOOD_CALM": ["1", "2", "3", "5", "6", "b7"],
    "MOOD_TENSE": ["b2", "b3", "#4", "5", "b6", "7", "b7"],
}

ROMAN_ROOT_DEGREES = {
    "I": "1",
    "ii": "2",
    "iii": "3",
    "IV": "4",
    "V": "5",
    "vi": "6",
    "vii_dim": "7",
    "i": "1",
    "ii_dim": "2",
    "III": "b3",
    "iv": "4",
    "v": "5",
    "VI": "b6",
    "VII": "b7",
    "bII": "b2",
    "bVI": "b6",
    "bVII": "b7",
}

DURATION_UNITS = {"1/16": 1, "1/8": 2, "1/4": 4, "1/2": 8}
T = TypeVar("T")

PROFILE_DENSITIES = {
    "PROFILE_BALANCED": ["DENSITY_MEDIUM", "DENSITY_SPARSE"],
    "PROFILE_SPARSE": ["DENSITY_VERY_SPARSE", "DENSITY_SPARSE"],
    "PROFILE_HOOK": ["DENSITY_SPARSE", "DENSITY_MEDIUM"],
    "PROFILE_RIFF": ["DENSITY_MEDIUM", "DENSITY_DENSE"],
    "PROFILE_AMBIENT": ["DENSITY_VERY_SPARSE", "DENSITY_SPARSE"],
    "PROFILE_CALL_RESPONSE": ["DENSITY_SPARSE", "DENSITY_MEDIUM"],
}

DENSITY_NOTE_RANGE = {
    "DENSITY_VERY_SPARSE": (4, 6),
    "DENSITY_SPARSE": (6, 8),
    "DENSITY_MEDIUM": (8, 12),
    "DENSITY_DENSE": (12, 18),
}

PROFILE_DURATION_WEIGHTS = {
    "PROFILE_BALANCED": {"1/16": 0.12, "1/8": 0.42, "1/4": 0.34, "1/2": 0.12},
    "PROFILE_SPARSE": {"1/16": 0.02, "1/8": 0.16, "1/4": 0.48, "1/2": 0.34},
    "PROFILE_HOOK": {"1/16": 0.10, "1/8": 0.48, "1/4": 0.34, "1/2": 0.08},
    "PROFILE_RIFF": {"1/16": 0.42, "1/8": 0.42, "1/4": 0.14, "1/2": 0.02},
    "PROFILE_AMBIENT": {"1/16": 0.02, "1/8": 0.12, "1/4": 0.42, "1/2": 0.44},
    "PROFILE_CALL_RESPONSE": {"1/16": 0.08, "1/8": 0.38, "1/4": 0.42, "1/2": 0.12},
}


def meter_units(meter: str) -> int:
    if meter == "METER_3_4":
        return 12
    if meter == "METER_6_8":
        return 12
    return 16


def choose_duration(remaining: int, style: str, rng: random.Random) -> str:
    candidates = [duration for duration, units in DURATION_UNITS.items() if units <= remaining]
    if remaining <= 2:
        return candidates[-1]
    if style == "STYLE_METAL":
        weights = [0.42, 0.40, 0.15, 0.03]
    elif style == "STYLE_AMBIENT":
        weights = [0.06, 0.20, 0.44, 0.30]
    else:
        weights = [0.18, 0.38, 0.34, 0.10]
    available = [(duration, weights[DURATIONS.index(duration)]) for duration in candidates]
    return weighted_choice(available, rng)


def choose_profile(style: str, mood: str, rng: random.Random) -> str:
    options = [(profile, 1.0) for profile in PHRASE_PROFILE_TOKENS]
    if style == "STYLE_METAL":
        options.extend([("PROFILE_RIFF", 2.4), ("PROFILE_HOOK", 1.3)])
    if style == "STYLE_AMBIENT":
        options.extend([("PROFILE_AMBIENT", 2.8), ("PROFILE_SPARSE", 1.5)])
    if mood == "MOOD_CALM":
        options.extend([("PROFILE_SPARSE", 1.6), ("PROFILE_AMBIENT", 1.4)])
    if mood in {"MOOD_EPIC", "MOOD_TENSE"}:
        options.extend([("PROFILE_RIFF", 1.2), ("PROFILE_HOOK", 1.2)])
    return weighted_choice(options, rng)


def choose_v3_duration(remaining: int, profile: str, density: str, events_left: int, rng: random.Random) -> str:
    candidates = [duration for duration, units in DURATION_UNITS.items() if units <= remaining]
    if not candidates:
        return "1/16"

    weights = PROFILE_DURATION_WEIGHTS[profile].copy()
    if density == "DENSITY_VERY_SPARSE":
        weights["1/4"] *= 1.4
        weights["1/2"] *= 1.8
        weights["1/16"] *= 0.25
    elif density == "DENSITY_DENSE":
        weights["1/16"] *= 1.8
        weights["1/8"] *= 1.35
        weights["1/2"] *= 0.25

    average_needed = remaining / max(1, events_left)
    if average_needed >= 6:
        weights["1/2"] *= 2.2
        weights["1/4"] *= 1.4
    elif average_needed <= 2:
        weights["1/16"] *= 1.5
        weights["1/8"] *= 1.4

    available = [(duration, weights[duration]) for duration in candidates]
    return weighted_choice(available, rng)


def generate_phrase_v3(
    style: str,
    mode: str,
    mood: str,
    meter: str,
    bars: int,
    progression: list[str],
    rng: random.Random,
) -> dict[str, object]:
    total_units = meter_units(meter) * bars
    profile = choose_profile(style, mood, rng)
    density = rng.choice(PROFILE_DENSITIES[profile])
    contour = weighted_choice(
        [
            ("CONTOUR_RISE", 1.0 if mood in {"MOOD_EPIC", "MOOD_BRIGHT"} else 0.7),
            ("CONTOUR_FALL", 1.0 if mood in {"MOOD_DARK", "MOOD_CALM"} else 0.7),
            ("CONTOUR_ARCH", 1.5),
            ("CONTOUR_STATIC", 1.2 if profile in {"PROFILE_AMBIENT", "PROFILE_SPARSE"} else 0.7),
        ],
        rng,
    )
    min_notes, max_notes = DENSITY_NOTE_RANGE[density]
    target_notes = min(total_units, rng.randint(min_notes, max_notes))
    note_count_group = note_count_token(target_notes)

    tokens = ["<BOS>"]
    positions = ["POS_OFFGRID"]
    sections = ["SECTION_BEGIN"]
    remaining = total_units
    position = 0
    previous_midi: int | None = None
    previous_direction = 0
    note_events = 0
    motif: list[tuple[str, int]] = []

    while remaining > 0:
        events_left = max(1, target_notes - note_events)
        duration = choose_v3_duration(remaining, profile, density, events_left, rng)
        units = DURATION_UNITS[duration]
        strong = is_strong_position(position, meter)
        chord = current_chord(progression, position, total_units)
        section = section_token(position, total_units)
        beat_position = beat_position_token(position, units, meter)
        rest_rate = rest_probability(profile, density, mood, strong, note_events, target_notes)

        if rng.random() < rest_rate and remaining > 2 and not strong:
            tokens.append(f"R:{duration}")
        else:
            degree, octave, previous_midi, previous_direction = choose_note_v3(
                style,
                mode,
                mood,
                chord,
                strong,
                previous_midi,
                previous_direction,
                profile,
                contour,
                position / max(1, total_units),
                motif,
                rng,
            )
            token_degree = degree
            if profile in {"PROFILE_HOOK", "PROFILE_CALL_RESPONSE"} and strong and motif and rng.random() < 0.32:
                token_degree, octave = rng.choice(motif)
                previous_midi = degree_to_midi(token_degree, octave)
            if len(motif) < 5 and rng.random() < 0.75:
                motif.append((token_degree, octave))
            tokens.append(f"D:{token_degree}:{octave}:{duration}")
            note_events += 1

        positions.append(beat_position)
        sections.append(section)
        remaining -= units
        position += units

    tokens.append("<EOS>")
    positions.append("POS_BAR_END")
    sections.append("SECTION_END")
    return {
        "tokens": tokens,
        "positions": positions,
        "sections": sections,
        "profile": profile,
        "density": density,
        "contour": contour,
        "note_count": note_count_group,
    }


def generate_phrase(style: str, mode: str, mood: str, meter: str, bars: int, progression: list[str], rng: random.Random) -> list[str]:
    total_units = meter_units(meter) * bars
    remaining = total_units
    position = 0
    tokens = ["<BOS>"]
    previous_midi: int | None = None

    while remaining > 0:
        duration = choose_duration(remaining, style, rng)
        units = DURATION_UNITS[duration]
        strong = is_strong_position(position, meter)
        chord = current_chord(progression, position, total_units)
        rest_rate = 0.03 if style in {"STYLE_METAL", "STYLE_ROCK"} else 0.08
        rest_rate += 0.05 if mood == "MOOD_CALM" and not strong else 0.0

        if rng.random() < rest_rate and remaining > 2 and not strong:
            tokens.append(f"R:{duration}")
        else:
            degree, octave, previous_midi = choose_note(style, mode, mood, chord, strong, previous_midi, rng)
            tokens.append(f"D:{degree}:{octave}:{duration}")

        remaining -= units
        position += units

    tokens.append("<EOS>")
    return tokens


def choose_note(
    style: str,
    mode: str,
    mood: str,
    chord: str,
    strong: bool,
    previous_midi: int | None,
    rng: random.Random,
) -> tuple[str, int, int]:
    candidates: list[tuple[tuple[str, int, int], float]] = []
    allowed = MODE_DEGREES[mode]
    chord_tones = chord_tone_degrees(chord, mode)
    mood_degrees = set(MOOD_CENTER[mood])

    for degree in allowed:
        for octave in OCTAVES:
            midi = degree_to_midi(degree, octave)
            interval = 0 if previous_midi is None else midi - previous_midi
            distance = abs(interval)
            weight = 1.0
            if degree in mood_degrees:
                weight *= 1.65
            if degree in chord_tones:
                weight *= 2.2 if strong else 1.35
            if strong and degree in {"1", "3", "b3", "5", "8"}:
                weight *= 1.35
            weight *= interval_weight(distance, style, mood, strong, previous_midi)
            weight *= octave_weight(octave, style, mood, strong)
            if previous_midi is not None and distance >= 8:
                # After a wide leap, nearby compensating notes become more attractive on the next choice.
                weight *= 0.82
            candidates.append(((degree, octave, midi), weight))

    degree, octave, midi = weighted_choice(candidates, rng)
    return degree, octave, midi


def choose_note_v3(
    style: str,
    mode: str,
    mood: str,
    chord: str,
    strong: bool,
    previous_midi: int | None,
    previous_direction: int,
    profile: str,
    contour: str,
    progress: float,
    motif: list[tuple[str, int]],
    rng: random.Random,
) -> tuple[str, int, int, int]:
    candidates: list[tuple[tuple[str, int, int], float]] = []
    allowed = MODE_DEGREES[mode]
    chord_tones = chord_tone_degrees(chord, mode)
    mood_degrees = set(MOOD_CENTER[mood])

    for degree in allowed:
        for octave in OCTAVES:
            midi = degree_to_midi(degree, octave)
            distance = 0 if previous_midi is None else abs(midi - previous_midi)
            direction = 0 if previous_midi is None else (1 if midi > previous_midi else -1 if midi < previous_midi else 0)
            weight = 1.0
            if degree in mood_degrees:
                weight *= 1.55
            if degree in chord_tones:
                weight *= 2.45 if strong else 1.30
            if strong and degree in {"1", "3", "b3", "5", "8"}:
                weight *= 1.25
            if motif and (degree, octave) in motif and profile in {"PROFILE_HOOK", "PROFILE_CALL_RESPONSE", "PROFILE_RIFF"}:
                weight *= 1.42
            weight *= interval_weight_v3(distance, previous_direction, direction, style, mood, profile, strong)
            weight *= octave_weight(octave, style, mood, strong)
            weight *= contour_weight(midi, contour, progress)
            candidates.append(((degree, octave, midi), weight))

    degree, octave, midi = weighted_choice(candidates, rng)
    direction = 0 if previous_midi is None else (1 if midi > previous_midi else -1 if midi < previous_midi else 0)
    return degree, octave, midi, direction


def interval_weight(distance: int, style: str, mood: str, strong: bool, previous_midi: int | None) -> float:
    if previous_midi is None:
        return 1.0
    if distance == 0:
        return 0.72 if strong else 0.9
    if distance <= 2:
        return 2.4
    if distance <= 4:
        return 2.0
    if distance <= 7:
        return 1.15 if style != "STYLE_AMBIENT" else 0.75
    if distance <= 12:
        if strong and mood in {"MOOD_EPIC", "MOOD_TENSE"}:
            return 0.72
        return 0.38
    return 0.04


def interval_weight_v3(distance: int, previous_direction: int, direction: int, style: str, mood: str, profile: str, strong: bool) -> float:
    if distance == 0:
        return 0.35 if strong else 0.58
    if distance <= 2:
        weight = 2.55
    elif distance <= 4:
        weight = 2.0
    elif distance <= 7:
        weight = 1.22
    elif distance <= 12:
        weight = 0.38 if strong else 0.20
    else:
        weight = 0.015

    if profile == "PROFILE_RIFF" and distance in {1, 2, 3, 4}:
        weight *= 1.3
    if profile == "PROFILE_AMBIENT" and distance > 7:
        weight *= 0.35
    if mood in {"MOOD_EPIC", "MOOD_TENSE"} and strong and 5 <= distance <= 12:
        weight *= 1.25
    if previous_direction != 0 and direction != 0 and previous_direction != direction and distance <= 4:
        weight *= 1.18
    return weight


def contour_weight(midi: int, contour: str, progress: float) -> float:
    low = 43
    center = 53
    high = 65
    if contour == "CONTOUR_RISE":
        target = low + (high - low) * progress
    elif contour == "CONTOUR_FALL":
        target = high - (high - low) * progress
    elif contour == "CONTOUR_ARCH":
        target = low + (high - low) * (1.0 - abs(progress - 0.5) * 2.0)
    else:
        target = center
    distance = abs(midi - target)
    return max(0.18, 1.0 - distance / 24.0)


def rest_probability(profile: str, density: str, mood: str, strong: bool, note_events: int, target_notes: int) -> float:
    rate = {
        "PROFILE_BALANCED": 0.04,
        "PROFILE_SPARSE": 0.10,
        "PROFILE_HOOK": 0.03,
        "PROFILE_RIFF": 0.015,
        "PROFILE_AMBIENT": 0.14,
        "PROFILE_CALL_RESPONSE": 0.07,
    }[profile]
    if density == "DENSITY_VERY_SPARSE":
        rate += 0.04
    if density == "DENSITY_DENSE":
        rate *= 0.45
    if mood == "MOOD_CALM":
        rate += 0.04
    if strong:
        rate *= 0.20
    if note_events < max(2, target_notes // 3):
        rate *= 0.55
    return min(rate, 0.22)


def octave_weight(octave: int, style: str, mood: str, strong: bool) -> float:
    if octave == 4:
        return 1.0
    if octave == 5:
        if mood == "MOOD_EPIC" or (style == "STYLE_METAL" and strong):
            return 0.70
        return 0.42
    if mood == "MOOD_DARK" or style in {"STYLE_METAL", "STYLE_AMBIENT"}:
        return 0.62
    return 0.38


def current_chord(progression: list[str], position: int, total_units: int) -> str:
    if not progression:
        return "i"
    slot = min(len(progression) - 1, int(position / max(1, total_units) * len(progression)))
    return progression[slot]


def chord_tone_degrees(chord: str, mode: str) -> set[str]:
    root = ROMAN_ROOT_DEGREES.get(chord, "1")
    root_semitone = DEGREE_SEMITONES[root]
    minor = chord[:1].islower() or chord.endswith("_dim")
    third = 3 if minor else 4
    fifth = 6 if chord.endswith("_dim") else 7
    tones = {(root_semitone + offset) % 12 for offset in (0, third, fifth)}
    return {degree for degree in MODE_DEGREES[mode] if DEGREE_SEMITONES[degree] % 12 in tones}


def is_strong_position(position: int, meter: str) -> bool:
    if meter == "METER_6_8":
        return position % 6 == 0
    return position % 4 == 0


def beat_position_token(position: int, units: int, meter: str) -> str:
    bar_units = meter_units(meter)
    in_bar = position % bar_units
    if in_bar == 0:
        return "POS_BAR_START"
    if in_bar + units >= bar_units:
        return "POS_BAR_END"
    if is_strong_position(position, meter):
        return "POS_STRONG_BEAT"
    if position % 2 == 0:
        return "POS_WEAK_BEAT"
    return "POS_OFFGRID"


def section_token(position: int, total_units: int) -> str:
    ratio = position / max(1, total_units)
    if ratio < 0.28:
        return "SECTION_BEGIN"
    if ratio > 0.72:
        return "SECTION_END"
    return "SECTION_MIDDLE"


def degree_to_midi(degree: str, octave: int) -> int:
    return octave * 12 + DEGREE_SEMITONES[degree]


def weighted_choice(items: list[tuple[T, float]], rng: random.Random) -> T:
    total = sum(max(0.0, weight) for _, weight in items)
    if total <= 0:
        return items[-1][0]
    pick = rng.random() * total
    running = 0.0
    for item, weight in items:
        running += max(0.0, weight)
        if pick <= running:
            return item
    return items[-1][0]


def phrase_tokens(version: int) -> list[str]:
    if version == 1:
        return [f"D:{degree}:{duration}" for degree in all_degrees() for duration in DURATIONS] + [f"R:{duration}" for duration in DURATIONS]
    return [f"D:{degree}:{octave}:{duration}" for degree in all_degrees() for octave in OCTAVES for duration in DURATIONS] + [f"R:{duration}" for duration in DURATIONS]


def all_degrees() -> list[str]:
    return ["1", "b2", "2", "b3", "3", "4", "#4", "5", "b6", "6", "b7", "7", "8"]


def write_vocabulary(path: Path, version: int) -> None:
    data = {
        "special_tokens": ["<PAD>", "<UNK>", "<BOS>", "<EOS>"],
        "style_tokens": STYLES,
        "mode_tokens": MODES,
        "mood_tokens": MOODS,
        "meter_tokens": METERS,
        "bar_tokens": [f"BARS_{bars}" for bars in BARS],
        "progression_tokens": [
            "I",
            "ii",
            "iii",
            "IV",
            "V",
            "vi",
            "vii_dim",
            "i",
            "ii_dim",
            "III",
            "iv",
            "v",
            "VI",
            "VII",
            "bII",
            "bVI",
            "bVII",
        ],
        "phrase_tokens": phrase_tokens(version),
    }
    if version >= 3:
        data["phrase_profile_tokens"] = PHRASE_PROFILE_TOKENS
        data["density_tokens"] = DENSITY_TOKENS
        data["contour_tokens"] = CONTOUR_TOKENS
        data["phrase_section_tokens"] = PHRASE_SECTION_TOKENS
        data["beat_position_tokens"] = BEAT_POSITION_TOKENS
        data["note_count_tokens"] = NOTE_COUNT_TOKENS
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def generate_dataset(output: Path, count: int, seed: int, version: int, vocab_output: Path | None) -> None:
    rng = random.Random(seed)
    with output.open("w", encoding="utf-8") as handle:
        for _ in range(count):
            style = rng.choice(STYLES)
            mode = rng.choice(MODES)
            mood = rng.choice(MOODS)
            meter = rng.choice(METERS)
            bars = rng.choice(BARS)
            progression = rng.choice(STYLE_PROGRESSIONS[style])
            phrase = generate_phrase_v3(style, mode, mood, meter, bars, progression, rng) if version >= 3 else {
                "tokens": generate_phrase(style, mode, mood, meter, bars, progression, rng)
            }
            tokens = list(phrase["tokens"])
            if version == 1:
                tokens = downgrade_tokens(tokens)
            item = {
                "style": style,
                "mode": mode,
                "mood": mood,
                "meter": meter,
                "bars": bars,
                "progression": progression,
                "tokens": tokens,
            }
            if version >= 3:
                item["profile"] = phrase["profile"]
                item["density"] = phrase["density"]
                item["contour"] = phrase["contour"]
                item["note_count"] = phrase["note_count"]
                item["positions"] = phrase["positions"]
                item["sections"] = phrase["sections"]
            handle.write(json.dumps(item, ensure_ascii=False) + "\n")

    if vocab_output:
        write_vocabulary(vocab_output, version)


def downgrade_tokens(tokens: list[str]) -> list[str]:
    downgraded: list[str] = []
    for token in tokens:
        parts = token.split(":")
        if len(parts) == 4 and parts[0] == "D":
            downgraded.append(f"D:{parts[1]}:{parts[3]}")
        else:
            downgraded.append(token)
    return downgraded


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="synthetic_melody_dataset_v3.jsonl")
    parser.add_argument("--count", type=int, default=5000)
    parser.add_argument("--seed", type=int, default=1984)
    parser.add_argument("--version", type=int, choices=[1, 2, 3], default=3)
    parser.add_argument("--vocab-output", default="vocab_v3.json")
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    vocab_output = Path(args.vocab_output) if args.vocab_output else None
    generate_dataset(Path(args.output), args.count, args.seed, args.version, vocab_output)
    print(f"written={args.output} examples={args.count} version={args.version} vocab={vocab_output}")
