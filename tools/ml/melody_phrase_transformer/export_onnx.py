from __future__ import annotations

import argparse
from pathlib import Path

import torch

from context_v3 import build_context_tokens
from model import MelodyPhraseTransformer, MelodyVocabulary
from train import TrainConfig, load_checkpoint


def export(args: argparse.Namespace) -> None:
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

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    context = build_context_tokens(
        known_tokens=set(vocab.id_to_token),
        max_context_length=args.context_length,
        style="STYLE_METAL",
        mode="MODE_NATURAL_MINOR",
        mood="MOOD_DARK",
        meter="METER_4_4",
        bars=2,
        progression=["i", "VI"],
        profile="PROFILE_BALANCED",
        density="DENSITY_MEDIUM",
        contour="CONTOUR_ARCH",
        note_count="NOTE_COUNT_8_12",
        section="SECTION_BEGIN",
        position="POS_BAR_START",
    )
    context = (context + ["<PAD>"] * args.context_length)[: args.context_length]
    context_tokens = torch.tensor([[vocab.encode(token) for token in context]], dtype=torch.long)
    seed_token = "D:1:4:1/8" if "D:1:4:1/8" in vocab.token_to_id else "D:1:1/8"
    previous_length = max(2, args.previous_length)
    previous_tokens = torch.tensor([[
        *([vocab.pad_id] * (previous_length - 2)),
        vocab.encode("<BOS>"),
        vocab.encode(seed_token),
    ]], dtype=torch.long)

    torch.onnx.export(
        model,
        (context_tokens, previous_tokens),
        output,
        input_names=["context_tokens", "previous_tokens"],
        output_names=["next_token_logits"],
        opset_version=17,
        dynamo=False,
    )
    print(f"exported={output}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", default="runs/melody_phrase_transformer/best_model.pt")
    parser.add_argument("--vocab", default="vocab_v3.json")
    parser.add_argument("--output", default="runs/melody_phrase_transformer/MelodyPhraseTransformer.onnx")
    parser.add_argument("--previous-length", type=int, default=32)
    parser.add_argument("--context-length", type=int, default=16)
    return parser.parse_args()


def ensure_vocabulary_matches(checkpoint: dict, vocab: MelodyVocabulary, vocab_path: str) -> None:
    checkpoint_size = int(checkpoint.get("vocabulary_size", -1))
    if checkpoint_size != len(vocab.id_to_token):
        raise RuntimeError(
            "Checkpoint vocabulary size does not match the selected vocab. "
            f"checkpoint={checkpoint_size} vocab={len(vocab.id_to_token)} vocab_path={vocab_path}. "
            "Use vocab.json for old v1 checkpoints, vocab_v2.json for v2, or train/export a fresh v3 checkpoint with vocab_v3.json."
        )


if __name__ == "__main__":
    export(parse_args())
