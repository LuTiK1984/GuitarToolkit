from __future__ import annotations

import argparse
import copy
import json
import shutil
import subprocess
import sys
from dataclasses import asdict, dataclass
from pathlib import Path


@dataclass(frozen=True)
class CandidateRecipe:
    name: str
    learning_rate: float
    label_smoothing: float
    mode_penalty: float
    mood_penalty: float
    style_penalty: float
    entropy_penalty: float


def run_generation(args: argparse.Namespace) -> None:
    generation_dir = Path(args.output_dir) / f"generation_{args.generation:03d}"
    generation_dir.mkdir(parents=True, exist_ok=True)
    recipes = build_recipes(args.population, args)
    candidates = []

    for index, recipe in enumerate(recipes, start=1):
        candidate_dir = generation_dir / f"candidate_{index:02d}_{recipe.name}"
        candidate_dir.mkdir(parents=True, exist_ok=True)
        checkpoint = candidate_dir / "best_model.pt"
        print(f"generation_candidate={index}/{len(recipes)} name={recipe.name} output={candidate_dir}", flush=True)

        command = [
            sys.executable,
            "train.py",
            "--dataset",
            args.dataset,
            "--vocab",
            args.vocab,
            "--epochs",
            str(args.epochs),
            "--batch-size",
            str(args.batch_size),
            "--learning-rate",
            format_float(recipe.learning_rate),
            "--label-smoothing",
            format_float(recipe.label_smoothing),
            "--mode-penalty",
            format_float(recipe.mode_penalty),
            "--mood-penalty",
            format_float(recipe.mood_penalty),
            "--style-penalty",
            format_float(recipe.style_penalty),
            "--entropy-penalty",
            format_float(recipe.entropy_penalty),
            "--interval-penalty",
            format_float(args.interval_penalty),
            "--octave-penalty",
            format_float(args.octave_penalty),
            "--repeat-penalty",
            format_float(args.repeat_penalty),
            "--rest-penalty",
            format_float(args.rest_penalty),
            "--duration-penalty",
            format_float(args.duration_penalty),
            "--embedding-size",
            str(args.embedding_size),
            "--heads",
            str(args.heads),
            "--layers",
            str(args.layers),
            "--feedforward-size",
            str(args.feedforward_size),
            "--dropout",
            format_float(args.dropout),
            "--output-dir",
            str(candidate_dir),
            "--save-every",
            str(args.save_every),
            "--progress-every",
            str(args.progress_every),
            "--num-workers",
            str(args.num_workers),
            "--seed",
            str(args.seed + index),
        ]
        if args.resume:
            command.extend(["--resume", args.resume])
        if args.reset_optimizer:
            command.append("--reset-optimizer")
        if args.cpu:
            command.append("--cpu")
        if args.amp:
            command.append("--amp")

        run_streaming(command)
        if not checkpoint.exists():
            checkpoint = candidate_dir / "MelodyPhraseTransformer.pt"

        evaluation = evaluate_checkpoint(checkpoint, args.top_k, args.vocab)
        summary = evaluation["summary"]
        candidate = {
            "name": recipe.name,
            "index": index,
            "checkpoint": str(checkpoint),
            "output_dir": str(candidate_dir),
            "recipe": asdict(recipe),
            "summary": summary,
            "scores": role_scores(summary),
        }
        candidates.append(candidate)
        (candidate_dir / "evaluation.json").write_text(json.dumps(evaluation, ensure_ascii=False, indent=2), encoding="utf-8")
        print(
            "generation_evaluated="
            f"{recipe.name} overall={summary['overall_score_percent']} "
            f"diversity={summary['diversity_score_percent']} "
            f"mood={summary['mood_fit_score_percent']} "
            f"life={summary.get('phrase_life_score_percent', 0)} "
            f"confidence={summary['confidence_balance_percent']}",
            flush=True,
        )

    champions = select_champions(candidates)
    champions_dir = Path(args.output_dir) / "champions"
    champions_dir.mkdir(parents=True, exist_ok=True)
    for role, candidate in champions.items():
        target = champions_dir / f"{role}_best_model.pt"
        shutil.copy2(candidate["checkpoint"], target)
        candidate["champion_checkpoint"] = str(target)

    result = {
        "generation": args.generation,
        "dataset": args.dataset,
        "parent": args.resume,
        "candidate_count": len(candidates),
        "champions": champions,
        "candidates": candidates,
    }
    summary_path = Path(args.output_dir) / "generation_summary.json"
    history_path = Path(args.output_dir) / "generation_history.jsonl"
    summary_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    with history_path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(result, ensure_ascii=False) + "\n")

    print("generation_champions:", flush=True)
    for role, candidate in champions.items():
        summary = candidate["summary"]
        print(
            f"{role}={candidate['name']} checkpoint={candidate['champion_checkpoint']} "
            f"overall={summary['overall_score_percent']} "
            f"musical={summary['musicality_score_percent']} "
            f"mood={summary['mood_fit_score_percent']} "
            f"style={summary['style_fit_score_percent']} "
            f"diversity={summary['diversity_score_percent']} "
            f"top3_mass={summary['avg_top3_mass']}",
            flush=True,
        )
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)


def build_recipes(population: int, args: argparse.Namespace) -> list[CandidateRecipe]:
    base = CandidateRecipe(
        name="balanced",
        learning_rate=args.learning_rate,
        label_smoothing=args.label_smoothing,
        mode_penalty=args.mode_penalty,
        mood_penalty=args.mood_penalty,
        style_penalty=args.style_penalty,
        entropy_penalty=args.entropy_penalty,
    )
    templates = [
        base,
        CandidateRecipe("theorist", args.learning_rate * 0.8, max(0.0, args.label_smoothing * 0.7), args.mode_penalty * 1.55, args.mood_penalty * 1.2, args.style_penalty * 1.2, args.entropy_penalty * 1.35),
        CandidateRecipe("art_house", args.learning_rate * 1.15, args.label_smoothing * 1.6, args.mode_penalty * 0.65, args.mood_penalty * 0.65, args.style_penalty * 0.75, args.entropy_penalty * 0.35),
        CandidateRecipe("decisive", args.learning_rate * 0.7, args.label_smoothing * 0.9, args.mode_penalty, args.mood_penalty, args.style_penalty, args.entropy_penalty * 1.9),
        CandidateRecipe("mood_hunter", args.learning_rate, args.label_smoothing, args.mode_penalty, args.mood_penalty * 1.8, args.style_penalty, args.entropy_penalty),
        CandidateRecipe("style_hunter", args.learning_rate, args.label_smoothing, args.mode_penalty, args.mood_penalty, args.style_penalty * 1.8, args.entropy_penalty * 0.9),
        CandidateRecipe("low_lr_polish", args.learning_rate * 0.45, args.label_smoothing * 0.8, args.mode_penalty * 1.1, args.mood_penalty * 1.1, args.style_penalty * 1.1, args.entropy_penalty * 1.1),
        CandidateRecipe("wide_context", args.learning_rate * 1.25, args.label_smoothing * 1.25, args.mode_penalty * 0.9, args.mood_penalty * 1.05, args.style_penalty * 0.9, args.entropy_penalty * 0.75),
    ]
    return templates[: max(1, min(population, len(templates)))]


def run_streaming(command: list[str]) -> None:
    process = subprocess.Popen(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    assert process.stdout is not None
    for line in process.stdout:
        print(line.rstrip(), flush=True)
    exit_code = process.wait()
    if exit_code != 0:
        raise RuntimeError(f"Command failed with exit code {exit_code}: {' '.join(command)}")


def evaluate_checkpoint(checkpoint: Path, top_k: int, vocab: str) -> dict:
    completed = subprocess.run(
        [
            sys.executable,
            "evaluate_checkpoint.py",
            "--checkpoint",
            str(checkpoint),
            "--vocab",
            vocab,
            "--top-k",
            str(top_k),
        ],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return json.loads(completed.stdout)


def role_scores(summary: dict) -> dict[str, float]:
    top3_mass = float(summary["avg_top3_mass"]) * 100.0
    entropy = float(summary["avg_entropy"])
    theoretical = (
        summary["musicality_score_percent"] * 0.32
        + summary["mood_fit_score_percent"] * 0.22
        + summary["style_fit_score_percent"] * 0.16
        + summary.get("phrase_life_score_percent", 0.0) * 0.14
        + summary["confidence_balance_percent"] * 0.10
        + top3_mass * 0.06
    )
    balanced = summary["overall_score_percent"]
    art_house = (
        summary["diversity_score_percent"] * 0.34
        + min(entropy / 3.6 * 100.0, 100.0) * 0.22
        + summary["musicality_score_percent"] * 0.18
        + summary.get("phrase_life_score_percent", 0.0) * 0.12
        + summary["mood_fit_score_percent"] * 0.08
        + summary["style_fit_score_percent"] * 0.08
        + summary["confidence_balance_percent"] * 0.06
    )
    return {
        "theoretical": round(theoretical, 4),
        "balanced": round(balanced, 4),
        "art_house": round(art_house, 4),
    }


def select_champions(candidates: list[dict]) -> dict[str, dict]:
    champions: dict[str, dict] = {}
    for role in ["theoretical", "balanced", "art_house"]:
        champions[role] = copy.deepcopy(max(candidates, key=lambda candidate: candidate["scores"][role]))
    return champions


def format_float(value: float) -> str:
    return f"{value:.8f}".rstrip("0").rstrip(".")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", default="synthetic_melody_dataset_v3.jsonl")
    parser.add_argument("--vocab", default="vocab_v3.json")
    parser.add_argument("--output-dir", default="runs/melody_generations")
    parser.add_argument("--resume")
    parser.add_argument("--generation", type=int, default=1)
    parser.add_argument("--population", type=int, default=6)
    parser.add_argument("--epochs", type=int, default=8)
    parser.add_argument("--batch-size", type=int, default=128)
    parser.add_argument("--learning-rate", type=float, default=0.00015)
    parser.add_argument("--label-smoothing", type=float, default=0.02)
    parser.add_argument("--mode-penalty", type=float, default=0.12)
    parser.add_argument("--mood-penalty", type=float, default=0.08)
    parser.add_argument("--style-penalty", type=float, default=0.06)
    parser.add_argument("--entropy-penalty", type=float, default=0.025)
    parser.add_argument("--interval-penalty", type=float, default=0.100)
    parser.add_argument("--octave-penalty", type=float, default=0.060)
    parser.add_argument("--repeat-penalty", type=float, default=0.120)
    parser.add_argument("--rest-penalty", type=float, default=0.100)
    parser.add_argument("--duration-penalty", type=float, default=0.060)
    parser.add_argument("--embedding-size", type=int, default=128)
    parser.add_argument("--heads", type=int, default=4)
    parser.add_argument("--layers", type=int, default=2)
    parser.add_argument("--feedforward-size", type=int, default=384)
    parser.add_argument("--dropout", type=float, default=0.1)
    parser.add_argument("--seed", type=int, default=3000)
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument("--save-every", type=int, default=0)
    parser.add_argument("--progress-every", type=int, default=100)
    parser.add_argument("--num-workers", type=int, default=0)
    parser.add_argument("--amp", action="store_true")
    parser.add_argument("--reset-optimizer", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


if __name__ == "__main__":
    run_generation(parse_args())
