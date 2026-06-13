from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


DURATION_UNITS = {"1/16": 1, "1/8": 2, "1/4": 4, "1/2": 8}
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


def meter_units(meter: str) -> int:
    if meter == "METER_3_4":
        return 12
    if meter == "METER_6_8":
        return 12
    return 16


def token_units(token: str) -> int:
    if token in {"<BOS>", "<EOS>"}:
        return 0
    if token.startswith("R:"):
        return DURATION_UNITS[token.split(":", 1)[1]]
    if token.startswith("D:"):
        return DURATION_UNITS[token.rsplit(":", 1)[1]]
    raise ValueError(f"Unknown phrase token: {token}")


def validate(path: Path) -> None:
    styles: Counter[str] = Counter()
    modes: Counter[str] = Counter()
    moods: Counter[str] = Counter()
    meters: Counter[str] = Counter()
    profiles: Counter[str] = Counter()
    densities: Counter[str] = Counter()
    contours: Counter[str] = Counter()
    positions: Counter[str] = Counter()
    sections: Counter[str] = Counter()
    octaves: Counter[int] = Counter()
    intervals: Counter[str] = Counter()
    examples = 0

    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        item = json.loads(line)
        required = {"style", "mode", "mood", "meter", "bars", "progression", "tokens"}
        missing = required.difference(item)
        if missing:
            raise RuntimeError(f"line={line_number} missing={sorted(missing)}")
        if item["tokens"][0] != "<BOS>" or item["tokens"][-1] != "<EOS>":
            raise RuntimeError(f"line={line_number} phrase must start with <BOS> and end with <EOS>")
        if "positions" in item and len(item["positions"]) != len(item["tokens"]):
            raise RuntimeError(f"line={line_number} positions length must match tokens length")
        if "sections" in item and len(item["sections"]) != len(item["tokens"]):
            raise RuntimeError(f"line={line_number} sections length must match tokens length")

        total = sum(token_units(token) for token in item["tokens"])
        expected = meter_units(item["meter"]) * int(item["bars"])
        if total != expected:
            raise RuntimeError(f"line={line_number} duration_units={total} expected={expected}")

        previous_midi: int | None = None
        for token in item["tokens"]:
            parsed = parse_note(token)
            if parsed is None:
                continue
            octave, midi = parsed
            octaves[octave] += 1
            if previous_midi is not None:
                distance = abs(midi - previous_midi)
                intervals[interval_bucket(distance)] += 1
            previous_midi = midi

        styles[item["style"]] += 1
        modes[item["mode"]] += 1
        moods[item["mood"]] += 1
        meters[item["meter"]] += 1
        if "profile" in item:
            profiles[item["profile"]] += 1
        if "density" in item:
            densities[item["density"]] += 1
        if "contour" in item:
            contours[item["contour"]] += 1
        positions.update(item.get("positions", []))
        sections.update(item.get("sections", []))
        examples += 1

    print(f"examples={examples}")
    print(f"styles={dict(styles)}")
    print(f"modes={dict(modes)}")
    print(f"moods={dict(moods)}")
    print(f"meters={dict(meters)}")
    if profiles:
        print(f"profiles={dict(profiles)}")
    if densities:
        print(f"densities={dict(densities)}")
    if contours:
        print(f"contours={dict(contours)}")
    if positions:
        print(f"positions={dict(positions)}")
    if sections:
        print(f"sections={dict(sections)}")
    print(f"octaves={dict(octaves)}")
    print(f"intervals={dict(intervals)}")
    print("status=ok")


def parse_note(token: str) -> tuple[int, int] | None:
    parts = token.split(":")
    if len(parts) == 4 and parts[0] == "D":
        octave = int(parts[2])
        return octave, octave * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    if len(parts) == 3 and parts[0] == "D":
        return 4, 4 * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    return None


def interval_bucket(distance: int) -> str:
    if distance <= 2:
        return "step"
    if distance <= 4:
        return "third"
    if distance <= 7:
        return "fourth_fifth"
    if distance <= 12:
        return "wide"
    return "too_wide"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", default="synthetic_melody_dataset.jsonl")
    return parser.parse_args()


if __name__ == "__main__":
    validate(Path(parse_args().dataset))
