from __future__ import annotations


PHRASE_PROFILE_TOKENS = [
    "PROFILE_BALANCED",
    "PROFILE_SPARSE",
    "PROFILE_HOOK",
    "PROFILE_RIFF",
    "PROFILE_AMBIENT",
    "PROFILE_CALL_RESPONSE",
]

DENSITY_TOKENS = [
    "DENSITY_VERY_SPARSE",
    "DENSITY_SPARSE",
    "DENSITY_MEDIUM",
    "DENSITY_DENSE",
]

CONTOUR_TOKENS = [
    "CONTOUR_RISE",
    "CONTOUR_FALL",
    "CONTOUR_ARCH",
    "CONTOUR_STATIC",
]

PHRASE_SECTION_TOKENS = [
    "SECTION_BEGIN",
    "SECTION_MIDDLE",
    "SECTION_END",
]

BEAT_POSITION_TOKENS = [
    "POS_BAR_START",
    "POS_STRONG_BEAT",
    "POS_WEAK_BEAT",
    "POS_BAR_END",
    "POS_OFFGRID",
]

NOTE_COUNT_TOKENS = [
    "NOTE_COUNT_4_6",
    "NOTE_COUNT_6_8",
    "NOTE_COUNT_8_12",
    "NOTE_COUNT_12_16",
    "NOTE_COUNT_DENSE",
]

OPTIONAL_CONTEXT_KEYS = [
    "phrase_profile_tokens",
    "density_tokens",
    "contour_tokens",
    "phrase_section_tokens",
    "beat_position_tokens",
    "note_count_tokens",
]

DEFAULT_PROFILE = "PROFILE_BALANCED"
DEFAULT_DENSITY = "DENSITY_MEDIUM"
DEFAULT_CONTOUR = "CONTOUR_ARCH"
DEFAULT_SECTION = "SECTION_MIDDLE"
DEFAULT_POSITION = "POS_WEAK_BEAT"
DEFAULT_NOTE_COUNT = "NOTE_COUNT_8_12"


def add_if_known(tokens: list[str], token: str, known_tokens: set[str]) -> None:
    if token in known_tokens:
        tokens.append(token)


def build_context_tokens(
    *,
    known_tokens: set[str],
    max_context_length: int,
    style: str,
    mode: str,
    mood: str,
    meter: str,
    bars: int,
    progression: list[str],
    profile: str | None = None,
    density: str | None = None,
    contour: str | None = None,
    note_count: str | None = None,
    section: str | None = None,
    position: str | None = None,
) -> list[str]:
    tokens = [
        style,
        mode,
        mood,
        meter,
        f"BARS_{bars}",
    ]
    add_if_known(tokens, profile or DEFAULT_PROFILE, known_tokens)
    add_if_known(tokens, density or DEFAULT_DENSITY, known_tokens)
    add_if_known(tokens, contour or DEFAULT_CONTOUR, known_tokens)
    add_if_known(tokens, note_count or DEFAULT_NOTE_COUNT, known_tokens)
    add_if_known(tokens, section or DEFAULT_SECTION, known_tokens)
    add_if_known(tokens, position or DEFAULT_POSITION, known_tokens)
    tokens.extend(progression)
    return tokens[:max_context_length]


def note_count_token(note_count: int) -> str:
    if note_count <= 6:
        return "NOTE_COUNT_4_6"
    if note_count <= 8:
        return "NOTE_COUNT_6_8"
    if note_count <= 12:
        return "NOTE_COUNT_8_12"
    if note_count <= 16:
        return "NOTE_COUNT_12_16"
    return "NOTE_COUNT_DENSE"

