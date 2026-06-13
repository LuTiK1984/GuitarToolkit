from __future__ import annotations

import argparse
import json
import random
from pathlib import Path


GENERATION_PROFILES = {
    "focused": {
        "substitution_rate": 0.12,
        "cadence_rate": 0.52,
        "mutation_scale": 0.75,
        "bridge_rate": 0.08,
        "borrowed_rate": 0.06,
        "mood_rate": 0.08,
    },
    "balanced": {
        "substitution_rate": 0.18,
        "cadence_rate": 0.42,
        "mutation_scale": 1.0,
        "bridge_rate": 0.16,
        "borrowed_rate": 0.12,
        "mood_rate": 0.18,
    },
    "diverse": {
        "substitution_rate": 0.26,
        "cadence_rate": 0.34,
        "mutation_scale": 1.25,
        "bridge_rate": 0.28,
        "borrowed_rate": 0.20,
        "mood_rate": 0.35,
    },
    "mood": {
        "substitution_rate": 0.14,
        "cadence_rate": 0.38,
        "mutation_scale": 1.15,
        "bridge_rate": 0.14,
        "borrowed_rate": 0.10,
        "mood_rate": 0.92,
    },
}


MAJOR_PROGRESSIONS = [
    ["I", "V", "vi", "IV"],
    ["I", "vi", "IV", "V"],
    ["I", "IV", "V", "I"],
    ["vi", "IV", "I", "V"],
    ["I", "V", "IV", "V"],
    ["ii", "V", "I", "I"],
    ["I", "iii", "IV", "V"],
    ["I", "IV", "vi", "V"],
    ["IV", "V", "iii", "vi"],
    ["I", "IV", "I", "V"],
    ["I", "ii", "V", "I"],
    ["I", "vi", "ii", "V"],
    ["IV", "I", "V", "vi"],
    ["vi", "V", "IV", "I"],
    ["I", "V", "iii", "IV"],
    ["I", "IV", "V", "vi"],
    ["ii", "IV", "V", "I"],
    ["I", "V", "vi", "iii", "IV", "I", "IV", "V"],
    ["I", "bVII", "IV", "I"],
    ["I", "bVI", "bVII", "I"],
    ["I", "V", "bVII", "IV"],
]

MINOR_PROGRESSIONS = [
    ["i", "VI", "VII", "i"],
    ["i", "VII", "VI", "VII"],
    ["i", "iv", "V", "i"],
    ["i", "VI", "III", "VII"],
    ["i", "VII", "VI", "V"],
    ["i", "iv", "VII", "III"],
    ["i", "bII", "i", "VII"],
    ["i", "VI", "iv", "V"],
    ["i", "VI", "V", "i"],
    ["i", "VI", "bII", "V"],
    ["i", "VI", "VII", "III"],
    ["i", "III", "VII", "VI"],
    ["i", "iv", "VI", "VII"],
    ["i", "v", "iv", "VII"],
    ["i", "VII", "III", "VI"],
    ["i", "bVI", "bVII", "i"],
    ["i", "v", "VI", "VII"],
    ["i", "VI", "VII", "iv", "i", "VI", "V", "i"],
    ["i", "bII", "VII", "VI"],
    ["i", "bVI", "iv", "bVII"],
    ["i", "III", "bVII", "iv"],
]

DORIAN_PROGRESSIONS = [
    ["i", "IV", "i", "VII"],
    ["i", "ii", "III", "IV"],
    ["i", "VII", "IV", "i"],
    ["i", "v", "IV", "VII"],
    ["i", "IV", "VII", "i"],
    ["i", "ii", "IV", "i"],
    ["i", "v", "VII", "IV"],
    ["i", "III", "IV", "v"],
    ["i", "bVII", "IV", "i"],
    ["i", "IV", "bVII", "III"],
]

PHRYGIAN_PROGRESSIONS = [
    ["i", "bII", "i", "VII"],
    ["i", "VI", "bII", "i"],
    ["i", "VII", "VI", "bII"],
    ["i", "bII", "VII", "i"],
    ["i", "VII", "bII", "i"],
    ["i", "bII", "VI", "VII"],
    ["i", "VI", "VII", "bII"],
    ["i", "bII", "iv", "bII"],
    ["i", "bII", "bVII", "VI"],
]

HARMONIC_MINOR_PROGRESSIONS = [
    ["i", "iv", "V", "i"],
    ["i", "VI", "V", "i"],
    ["i", "III", "iv", "V"],
    ["i", "ii°", "V", "i"],
    ["i", "VI", "ii°", "V"],
    ["i", "iv", "VI", "V"],
    ["i", "III", "VI", "V"],
    ["i", "V", "VI", "V"],
    ["i", "bII", "V", "i"],
    ["i", "VI", "bII", "V"],
]

STYLE_WEIGHTS = {
    "STYLE_METAL": {
        "MODE_NATURAL_MINOR": MINOR_PROGRESSIONS,
        "MODE_PHRYGIAN": PHRYGIAN_PROGRESSIONS,
        "MODE_HARMONIC_MINOR": HARMONIC_MINOR_PROGRESSIONS,
    },
    "STYLE_ROCK": {
        "MODE_MAJOR": MAJOR_PROGRESSIONS,
        "MODE_NATURAL_MINOR": MINOR_PROGRESSIONS,
        "MODE_DORIAN": DORIAN_PROGRESSIONS,
    },
    "STYLE_POP": {
        "MODE_MAJOR": MAJOR_PROGRESSIONS,
        "MODE_NATURAL_MINOR": MINOR_PROGRESSIONS,
    },
    "STYLE_AMBIENT": {
        "MODE_MAJOR": MAJOR_PROGRESSIONS,
        "MODE_DORIAN": DORIAN_PROGRESSIONS,
        "MODE_NATURAL_MINOR": MINOR_PROGRESSIONS,
    },
    "STYLE_BLUES": {
        "MODE_MAJOR": [
            ["I", "IV", "I", "V"],
            ["I", "IV", "V", "IV"],
            ["I", "I", "IV", "V"],
            ["I", "IV", "I", "I", "IV", "IV", "I", "I", "V", "IV", "I", "V"],
        ],
        "MODE_DORIAN": DORIAN_PROGRESSIONS,
    },
}

MOODS = ["MOOD_DARK", "MOOD_EPIC", "MOOD_BRIGHT", "MOOD_CALM", "MOOD_TENSE"]
STYLE_MODE_PAIRS = [(style, mode) for style, modes in STYLE_WEIGHTS.items() for mode in modes]

MODE_ALLOWED_TOKENS = {
    "MODE_MAJOR": {"I", "ii", "iii", "IV", "V", "vi", "vii°", "bVII", "bVI", "bII"},
    "MODE_NATURAL_MINOR": {"i", "ii°", "III", "iv", "v", "VI", "VII", "bII", "bVI", "bVII", "V"},
    "MODE_DORIAN": {"i", "ii", "III", "IV", "v", "vi°", "bVII", "VII"},
    "MODE_PHRYGIAN": {"i", "bII", "III", "iv", "v", "VI", "VII", "ii°"},
    "MODE_HARMONIC_MINOR": {"i", "ii°", "III+", "iv", "V", "VI", "vii°", "bII", "VII"},
}

MOOD_PREFERRED_TOKENS = {
    "MOOD_DARK": ["i", "iv", "v", "VI", "VII", "bII", "bVI", "bVII"],
    "MOOD_EPIC": ["i", "I", "IV", "V", "VI", "VII", "bII"],
    "MOOD_BRIGHT": ["I", "IV", "V", "vi", "ii", "iii"],
    "MOOD_CALM": ["I", "vi", "IV", "ii", "i", "III", "VII", "bVII"],
    "MOOD_TENSE": ["V", "bII", "vii°", "ii°", "bVII", "VII", "iv"],
}

CADENCES = {
    "MODE_MAJOR": [["V", "I"], ["IV", "V", "I"], ["ii", "V", "I"], ["vi", "IV", "V", "I"]],
    "MODE_NATURAL_MINOR": [["V", "i"], ["VII", "i"], ["iv", "V", "i"], ["VI", "VII", "i"]],
    "MODE_DORIAN": [["IV", "i"], ["VII", "i"], ["v", "IV", "i"]],
    "MODE_PHRYGIAN": [["bII", "i"], ["VII", "i"], ["VI", "bII", "i"]],
    "MODE_HARMONIC_MINOR": [["V", "i"], ["ii°", "V", "i"], ["iv", "V", "i"]],
}

SUBSTITUTIONS = {
    "MODE_MAJOR": {"I": ["I", "vi"], "IV": ["IV", "ii"], "V": ["V"], "vi": ["vi", "I"], "bVII": ["bVII", "IV"]},
    "MODE_NATURAL_MINOR": {"i": ["i"], "VI": ["VI", "III", "bVI"], "VII": ["VII", "v", "bVII"], "iv": ["iv"], "V": ["V", "VII"]},
    "MODE_DORIAN": {"i": ["i"], "IV": ["IV", "ii"], "VII": ["VII", "v", "bVII"], "v": ["v", "III"]},
    "MODE_PHRYGIAN": {"i": ["i"], "bII": ["bII"], "VII": ["VII", "bVII"], "VI": ["VI", "bVI"]},
    "MODE_HARMONIC_MINOR": {"i": ["i"], "iv": ["iv"], "V": ["V"], "VI": ["VI", "bVI"], "ii°": ["ii°"], "bII": ["bII"]},
}

BRIDGE_TOKENS = {
    "MODE_MAJOR": ["ii", "iii", "IV", "V", "vi", "bVII"],
    "MODE_NATURAL_MINOR": ["III", "iv", "v", "VI", "VII", "bII", "bVI", "bVII"],
    "MODE_DORIAN": ["ii", "III", "IV", "v", "VII", "bVII"],
    "MODE_PHRYGIAN": ["bII", "iv", "VI", "VII", "bVII"],
    "MODE_HARMONIC_MINOR": ["ii°", "III", "iv", "V", "VI", "bII"],
}

BORROWED_MOVES = {
    "MODE_MAJOR": [["I", "bVII", "IV"], ["I", "bVI", "bVII"], ["vi", "bVII", "IV"]],
    "MODE_NATURAL_MINOR": [["i", "bII", "VII"], ["i", "bVI", "bVII"], ["VI", "bII", "V"]],
    "MODE_DORIAN": [["i", "IV", "bVII"], ["i", "bVII", "III"]],
    "MODE_PHRYGIAN": [["i", "bII", "bVII"], ["i", "bII", "VI"]],
    "MODE_HARMONIC_MINOR": [["i", "bII", "V"], ["i", "VI", "bII"]],
}


def rotate(tokens: list[str], shift: int) -> list[str]:
    shift %= len(tokens)
    return tokens[shift:] + tokens[:shift]


def stretch(tokens: list[str], target_length: int) -> list[str]:
    result: list[str] = []
    while len(result) < target_length:
        result.extend(tokens)
    return result[:target_length]


def reharmonize(tokens: list[str], mode: str, rng: random.Random, substitution_rate: float) -> list[str]:
    substitutions = SUBSTITUTIONS.get(mode, {})
    result: list[str] = []
    for token in tokens:
        choices = substitutions.get(token)
        result.append(rng.choice(choices) if choices and rng.random() < substitution_rate else token)
    return result


def add_cadence(tokens: list[str], mode: str, rng: random.Random, cadence_rate: float) -> list[str]:
    cadences = CADENCES.get(mode, [])
    if not cadences or rng.random() > cadence_rate:
        return tokens

    cadence = rng.choice(cadences)
    keep = max(1, len(tokens) - len(cadence))
    return [*tokens[:keep], *cadence]


def add_bridge(tokens: list[str], mode: str, rng: random.Random, bridge_rate: float) -> list[str]:
    bridge_tokens = BRIDGE_TOKENS.get(mode, [])
    if len(tokens) < 4 or not bridge_tokens or rng.random() > bridge_rate:
        return tokens

    result = list(tokens)
    index = rng.randrange(1, max(2, len(result) - 1))
    result[index] = rng.choice(bridge_tokens)
    return result


def add_borrowed_move(tokens: list[str], mode: str, rng: random.Random, borrowed_rate: float) -> list[str]:
    moves = BORROWED_MOVES.get(mode, [])
    if len(tokens) < 4 or not moves or rng.random() > borrowed_rate:
        return tokens

    result = list(tokens)
    move = rng.choice(moves)
    index = rng.randrange(0, max(1, len(result) - len(move) + 1))
    result[index:index + len(move)] = move
    return result


def mutate(tokens: list[str], mode: str, mood: str, rng: random.Random, mutation_scale: float) -> list[str]:
    result = list(tokens)
    if mood == "MOOD_TENSE" and "V" in result and rng.random() < 0.35 * mutation_scale:
        result.insert(max(1, len(result) - 1), "bII")
    if mood == "MOOD_EPIC" and "VI" in result and rng.random() < 0.30 * mutation_scale:
        result.append("VII")
    if mood == "MOOD_CALM" and len(result) > 4 and rng.random() < 0.45 * mutation_scale:
        result = result[:4]
    if mood == "MOOD_DARK" and mode in {"MODE_NATURAL_MINOR", "MODE_PHRYGIAN", "MODE_HARMONIC_MINOR"} and rng.random() < 0.28 * mutation_scale:
        result = ["i", *result[1:]]
    if mood == "MOOD_BRIGHT" and mode == "MODE_MAJOR" and rng.random() < 0.30 * mutation_scale:
        result = ["I", *result[1:]]
    return result


def mood_choices(mode: str, mood: str) -> list[str]:
    allowed = MODE_ALLOWED_TOKENS.get(mode, set())
    return [token for token in MOOD_PREFERRED_TOKENS.get(mood, []) if token in allowed]


def apply_mood_shape(tokens: list[str], mode: str, mood: str, rng: random.Random, mood_rate: float) -> list[str]:
    choices = mood_choices(mode, mood)
    if len(tokens) < 3 or not choices or rng.random() > mood_rate:
        return tokens

    result = list(tokens)
    replacement_count = 1 if len(result) < 6 else rng.choice([1, 2])
    protected_positions = {0, len(result) - 1}
    candidate_positions = [index for index in range(len(result)) if index not in protected_positions]
    rng.shuffle(candidate_positions)

    for index in candidate_positions[:replacement_count]:
        result[index] = rng.choice(choices)

    if mood == "MOOD_TENSE" and rng.random() < 0.55 and len(result) >= 4:
        result[-2] = rng.choice(choices)
    elif mood == "MOOD_CALM" and rng.random() < 0.45:
        result = result[:4]
    elif mood == "MOOD_EPIC" and rng.random() < 0.45 and len(result) <= 8:
        result.append(rng.choice(choices))
    elif mood == "MOOD_BRIGHT" and mode == "MODE_MAJOR" and rng.random() < 0.50:
        result[0] = "I"
    elif mood == "MOOD_DARK" and "i" in choices and rng.random() < 0.50:
        result[0] = "i"

    return result


def choose_context(rng: random.Random, balanced: bool, index: int) -> tuple[str, str, str]:
    if balanced:
        style, mode = STYLE_MODE_PAIRS[index % len(STYLE_MODE_PAIRS)]
        mood = MOODS[(index // len(STYLE_MODE_PAIRS)) % len(MOODS)]
        return style, mode, mood

    style = rng.choice(list(STYLE_WEIGHTS.keys()))
    mode = rng.choice(list(STYLE_WEIGHTS[style].keys()))
    mood = rng.choice(MOODS)
    return style, mode, mood


def build_examples(count: int, seed: int, balanced: bool, profile_name: str) -> list[dict[str, object]]:
    rng = random.Random(seed)
    profile = GENERATION_PROFILES[profile_name]
    examples: list[dict[str, object]] = []

    while len(examples) < count:
        style, mode, mood = choose_context(rng, balanced, len(examples))
        base = rng.choice(STYLE_WEIGHTS[style][mode])
        length = rng.choice([4, 4, 6, 8, 8, 12])
        tokens = stretch(rotate(base, rng.randrange(len(base))), length)
        tokens = reharmonize(tokens, mode, rng, profile["substitution_rate"])
        tokens = add_bridge(tokens, mode, rng, profile["bridge_rate"])
        tokens = add_borrowed_move(tokens, mode, rng, profile["borrowed_rate"])
        tokens = add_cadence(tokens, mode, rng, profile["cadence_rate"])
        tokens = mutate(tokens, mode, mood, rng, profile["mutation_scale"])
        tokens = apply_mood_shape(tokens, mode, mood, rng, profile.get("mood_rate", 0.0))
        examples.append(
            {
                "style": style,
                "mode": mode,
                "mood": mood,
                "tokens": ["<BOS>", *tokens, "<EOS>"],
            }
        )

    return examples


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a license-clean synthetic progression dataset.")
    parser.add_argument("--output", default="synthetic_dataset.jsonl")
    parser.add_argument("--count", type=int, default=5000)
    parser.add_argument("--seed", type=int, default=1984)
    parser.add_argument("--profile", choices=GENERATION_PROFILES.keys(), default="balanced")
    parser.add_argument("--random-context", action="store_true", help="Use random style/mode/mood sampling instead of balanced coverage.")
    args = parser.parse_args()

    examples = build_examples(args.count, args.seed, balanced=not args.random_context, profile_name=args.profile)
    output = Path(args.output)
    output.write_text(
        "\n".join(json.dumps(example, ensure_ascii=False, separators=(",", ":")) for example in examples) + "\n",
        encoding="utf-8",
    )
    print(f"written={output} examples={len(examples)} balanced={not args.random_context} profile={args.profile}")


if __name__ == "__main__":
    main()
