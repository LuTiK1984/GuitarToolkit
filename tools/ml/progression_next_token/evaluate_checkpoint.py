from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import torch

from model import ProgressionNextTokenModel, Vocabulary


MODE_ALLOWED_TOKENS: dict[str, set[str]] = {
    "MODE_MAJOR": {"I", "ii", "iii", "IV", "V", "vi", "vii°", "bVII", "bVI", "bII"},
    "MODE_NATURAL_MINOR": {"i", "ii°", "III", "iv", "v", "VI", "VII", "bII", "bVI", "bVII", "V"},
    "MODE_DORIAN": {"i", "ii", "III", "IV", "v", "vi°", "bVII", "VII"},
    "MODE_PHRYGIAN": {"i", "bII", "III", "iv", "v", "VI", "VII", "ii°"},
    "MODE_HARMONIC_MINOR": {"i", "ii°", "III+", "iv", "V", "VI", "vii°", "bII", "VII"},
}

MOOD_PREFERRED_TOKENS: dict[str, set[str]] = {
    "MOOD_DARK": {"i", "iv", "v", "VI", "VII", "bII", "bVI", "bVII"},
    "MOOD_EPIC": {"i", "I", "IV", "V", "VI", "VII", "bII"},
    "MOOD_BRIGHT": {"I", "IV", "V", "vi", "ii", "iii"},
    "MOOD_CALM": {"I", "vi", "IV", "ii", "i", "III", "VII", "bVII"},
    "MOOD_TENSE": {"V", "bII", "vii°", "ii°", "bVII", "VII", "iv"},
}

STYLE_PREFERRED_TOKENS: dict[str, set[str]] = {
    "STYLE_METAL": {"i", "iv", "V", "VI", "VII", "bII", "bVI"},
    "STYLE_ROCK": {"I", "IV", "V", "vi", "bVII", "bVI", "i", "VII"},
    "STYLE_POP": {"I", "V", "vi", "IV", "ii", "iii"},
    "STYLE_AMBIENT": {"I", "vi", "IV", "ii", "i", "III", "VII", "bVII"},
    "STYLE_BLUES": {"I", "IV", "V", "bVII", "i", "iv", "v"},
}


def add_group_metric(groups: dict[str, dict[str, list[float] | set[str]]], key: str, entropy: float, top3_mass: float, top1: str) -> None:
    bucket = groups.setdefault(key, {"entropy": [], "top3_mass": [], "top1": set()})
    bucket["entropy"].append(entropy)
    bucket["top3_mass"].append(top3_mass)
    bucket["top1"].add(top1)


def summarize_groups(groups: dict[str, dict[str, list[float] | set[str]]]) -> dict[str, dict[str, float | int]]:
    summary: dict[str, dict[str, float | int]] = {}
    for key, values in sorted(groups.items()):
        entropy_values = values["entropy"]
        top3_values = values["top3_mass"]
        top1_values = values["top1"]
        assert isinstance(entropy_values, list)
        assert isinstance(top3_values, list)
        assert isinstance(top1_values, set)
        summary[key] = {
            "count": len(entropy_values),
            "avg_entropy": round(sum(entropy_values) / max(len(entropy_values), 1), 4),
            "avg_top3_mass": round(sum(top3_values) / max(len(top3_values), 1), 4),
            "distinct_top1": len(top1_values),
        }

    return summary


def probability_mass(probabilities: torch.Tensor, vocab: Vocabulary, tokens: set[str]) -> float:
    return float(sum(float(probabilities[index]) for index, token in enumerate(vocab.id_to_token) if token in tokens))


def score_from_range(value: float, low: float, high: float) -> float:
    if low <= value <= high:
        return 1.0

    distance = low - value if value < low else value - high
    width = max(high - low, 1e-6)
    return max(0.0, 1.0 - distance / width)


def percent(value: float) -> int:
    return int(round(max(0.0, min(1.0, value)) * 100))


@torch.no_grad()
def evaluate(args: argparse.Namespace) -> None:
    checkpoint = torch.load(args.checkpoint, map_location="cpu", weights_only=False)
    config = checkpoint["config"]
    vocab = Vocabulary.load(args.vocab)
    model = ProgressionNextTokenModel(
        vocabulary_size=checkpoint["vocabulary_size"],
        embedding_size=config["embedding_size"],
        hidden_size=config["hidden_size"],
        layers=config["layers"],
        dropout=config["dropout"],
        rnn_type=config["rnn_type"],
    )
    model.load_state_dict(checkpoint["model_state"])
    model.eval()

    prompts = [json.loads(line) for line in Path(args.prompts).read_text(encoding="utf-8").splitlines() if line.strip()]
    output_mask = torch.full((len(vocab.id_to_token),), -1_000_000.0, dtype=torch.float32)
    output_mask[vocab.output_token_ids] = 0.0

    rows = []
    top1_tokens: list[str] = []
    entropy_values: list[float] = []
    top3_mass_values: list[float] = []
    musical_fit_values: list[float] = []
    mood_fit_values: list[float] = []
    style_fit_values: list[float] = []
    top1_musical_hits = 0
    top1_mood_hits = 0
    top1_style_hits = 0
    by_style: dict[str, dict[str, list[float] | set[str]]] = {}
    by_mode: dict[str, dict[str, list[float] | set[str]]] = {}
    by_mood: dict[str, dict[str, list[float] | set[str]]] = {}

    for prompt in prompts:
        previous = [vocab.encode(token) for token in prompt["previous"]]
        logits = model(
            torch.tensor([vocab.encode(prompt["style"])], dtype=torch.long),
            torch.tensor([vocab.encode(prompt["mode"])], dtype=torch.long),
            torch.tensor([vocab.encode(prompt["mood"])], dtype=torch.long),
            torch.tensor([previous], dtype=torch.long),
        )
        probabilities = torch.softmax(logits + output_mask.unsqueeze(0), dim=1)[0]
        top = probabilities.topk(k=args.top_k)
        predictions = [
            {
                "token": vocab.id_to_token[int(index)],
                "probability": round(float(probability), 4),
            }
            for probability, index in zip(top.values, top.indices)
        ]

        entropy = -sum(float(prob) * math.log(max(float(prob), 1e-12)) for prob in probabilities if float(prob) > 0)
        top3_mass = float(top.values[: min(3, len(top.values))].sum().item())
        top1_token = predictions[0]["token"]
        top1_tokens.append(top1_token)
        entropy_values.append(entropy)
        top3_mass_values.append(top3_mass)

        allowed_tokens = MODE_ALLOWED_TOKENS.get(prompt["mode"], {*vocab.progression_tokens, "<EOS>"})
        mood_tokens = MOOD_PREFERRED_TOKENS.get(prompt["mood"], set())
        style_tokens = STYLE_PREFERRED_TOKENS.get(prompt["style"], set())
        musical_fit = probability_mass(probabilities, vocab, allowed_tokens)
        mood_fit = probability_mass(probabilities, vocab, mood_tokens)
        style_fit = probability_mass(probabilities, vocab, style_tokens)
        musical_fit_values.append(musical_fit)
        mood_fit_values.append(mood_fit)
        style_fit_values.append(style_fit)
        top1_musical_hits += int(top1_token in allowed_tokens)
        top1_mood_hits += int(top1_token in mood_tokens)
        top1_style_hits += int(top1_token in style_tokens)

        add_group_metric(by_style, prompt["style"], entropy, top3_mass, top1_token)
        add_group_metric(by_mode, prompt["mode"], entropy, top3_mass, top1_token)
        add_group_metric(by_mood, prompt["mood"], entropy, top3_mass, top1_token)

        rows.append(
            {
                "name": prompt["name"],
                "context": {
                    "style": prompt["style"],
                    "mode": prompt["mode"],
                    "mood": prompt["mood"],
                    "previous": prompt["previous"],
                },
                "entropy": round(entropy, 4),
                "top3_mass": round(top3_mass, 4),
                "musical_fit_mass": round(musical_fit, 4),
                "mood_fit_mass": round(mood_fit, 4),
                "style_fit_mass": round(style_fit, 4),
                "top1_musical_hit": top1_token in allowed_tokens,
                "top1_mood_hit": top1_token in mood_tokens,
                "top1_style_hit": top1_token in style_tokens,
                "predictions": predictions,
            }
        )

    output_count = max(len(vocab.output_token_ids), 1)
    normalized_entropy = (sum(entropy_values) / max(len(entropy_values), 1)) / math.log(output_count)
    confidence_balance = score_from_range(sum(top3_mass_values) / max(len(top3_mass_values), 1), 0.55, 0.75)
    diversity_score = percent((normalized_entropy + confidence_balance) / 2)
    musical_fit_score = percent(sum(musical_fit_values) / max(len(musical_fit_values), 1))
    mood_fit_score = percent(sum(mood_fit_values) / max(len(mood_fit_values), 1))
    style_fit_score = percent(sum(style_fit_values) / max(len(style_fit_values), 1))
    top1_musical_rate = top1_musical_hits / max(len(rows), 1)
    top1_mood_rate = top1_mood_hits / max(len(rows), 1)
    top1_style_rate = top1_style_hits / max(len(rows), 1)
    musicality_score = percent((sum(musical_fit_values) / max(len(musical_fit_values), 1) + top1_musical_rate) / 2)
    overall_score = round(
        diversity_score * 0.30
        + musicality_score * 0.35
        + mood_fit_score * 0.15
        + style_fit_score * 0.10
        + percent(confidence_balance) * 0.10,
        1,
    )

    summary = {
        "checkpoint": args.checkpoint,
        "prompt_count": len(rows),
        "avg_entropy": round(sum(entropy_values) / max(len(entropy_values), 1), 4),
        "normalized_entropy": round(normalized_entropy, 4),
        "avg_top3_mass": round(sum(top3_mass_values) / max(len(top3_mass_values), 1), 4),
        "distinct_top1": len(set(top1_tokens)),
        "distinct_top1_percent": percent(len(set(top1_tokens)) / max(len(rows), 1)),
        "diversity_score_percent": diversity_score,
        "confidence_balance_percent": percent(confidence_balance),
        "musicality_score_percent": musicality_score,
        "musical_fit_mass": round(sum(musical_fit_values) / max(len(musical_fit_values), 1), 4),
        "top1_musical_hit_percent": percent(top1_musical_rate),
        "mood_fit_score_percent": mood_fit_score,
        "mood_fit_mass": round(sum(mood_fit_values) / max(len(mood_fit_values), 1), 4),
        "top1_mood_hit_percent": percent(top1_mood_rate),
        "style_fit_score_percent": style_fit_score,
        "style_fit_mass": round(sum(style_fit_values) / max(len(style_fit_values), 1), 4),
        "top1_style_hit_percent": percent(top1_style_rate),
        "overall_score_percent": overall_score,
        "top1_tokens": top1_tokens,
        "by_style": summarize_groups(by_style),
        "by_mode": summarize_groups(by_mode),
        "by_mood": summarize_groups(by_mood),
    }

    result = {"summary": summary, "prompts": rows}
    print(json.dumps(result, ensure_ascii=False, indent=2))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate checkpoint diversity on fixed musical prompts.")
    parser.add_argument("--checkpoint", default="runs/progression_next_token/best_model.pt")
    parser.add_argument("--vocab", default="vocab.json")
    parser.add_argument("--prompts", default="eval_prompts_full.jsonl")
    parser.add_argument("--top-k", type=int, default=8)
    return parser.parse_args()


if __name__ == "__main__":
    evaluate(parse_args())
