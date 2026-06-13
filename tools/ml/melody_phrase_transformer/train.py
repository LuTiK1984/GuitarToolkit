from __future__ import annotations

import argparse
import json
from dataclasses import asdict, dataclass
from pathlib import Path

import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset, random_split

from context_v3 import (
    DEFAULT_CONTOUR,
    DEFAULT_DENSITY,
    DEFAULT_NOTE_COUNT,
    DEFAULT_POSITION,
    DEFAULT_PROFILE,
    DEFAULT_SECTION,
    build_context_tokens,
    note_count_token,
)
from model import MelodyPhraseTransformer, MelodyVocabulary


@dataclass(frozen=True)
class TrainConfig:
    embedding_size: int = 128
    heads: int = 4
    layers: int = 2
    feedforward_size: int = 384
    dropout: float = 0.1
    epochs: int = 30
    batch_size: int = 64
    learning_rate: float = 0.0003
    label_smoothing: float = 0.02
    max_sequence_length: int = 96
    max_context_length: int = 16
    validation_ratio: float = 0.15
    seed: int = 1984
    mode_penalty: float = 0.0
    mood_penalty: float = 0.0
    style_penalty: float = 0.0
    entropy_penalty: float = 0.0
    interval_penalty: float = 0.0
    octave_penalty: float = 0.0
    repeat_penalty: float = 0.0
    rest_penalty: float = 0.0
    duration_penalty: float = 0.0
    amp: bool = False


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

DURATION_INDEX = {
    "1/16": 0,
    "1/8": 1,
    "1/4": 2,
    "1/2": 3,
}


class MelodyDataset(Dataset):
    def __init__(self, dataset_path: Path, vocab: MelodyVocabulary, config: TrainConfig) -> None:
        self.examples: list[tuple[list[int], list[int], int, int, int, int, int, int]] = []
        self.vocab = vocab
        self.config = config
        known_tokens = set(vocab.id_to_token)

        for line in dataset_path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            item = json.loads(line)
            tokens = [vocab.encode(token) for token in item["tokens"]]
            item_tokens = item["tokens"]
            positions = item.get("positions", [])
            sections = item.get("sections", [])
            note_count = item.get("note_count") or note_count_token(sum(1 for token in item_tokens if token.startswith("D:")))

            for index in range(1, len(tokens)):
                context = build_context_tokens(
                    known_tokens=known_tokens,
                    max_context_length=config.max_context_length,
                    style=item["style"],
                    mode=item["mode"],
                    mood=item["mood"],
                    meter=item["meter"],
                    bars=int(item["bars"]),
                    progression=item.get("progression", []),
                    profile=item.get("profile", DEFAULT_PROFILE),
                    density=item.get("density", DEFAULT_DENSITY),
                    contour=item.get("contour", DEFAULT_CONTOUR),
                    note_count=note_count or DEFAULT_NOTE_COUNT,
                    section=sections[index] if index < len(sections) else DEFAULT_SECTION,
                    position=positions[index] if index < len(positions) else DEFAULT_POSITION,
                )
                context = (context + ["<PAD>"] * config.max_context_length)[: config.max_context_length]
                context_ids = [vocab.encode(token) for token in context]
                previous = tokens[:index][-config.max_sequence_length :]
                target = tokens[index]
                previous_token_values = [vocab.id_to_token[token_id] for token_id in previous]
                previous_midi = last_note_midi(previous_token_values)
                repeat_count = trailing_note_repeat_count(previous_token_values)
                rest_streak = trailing_rest_count(previous_token_values)
                previous_duration = last_duration_index(previous_token_values)
                duration_streak = trailing_duration_count(previous_token_values)
                self.examples.append((
                    context_ids,
                    previous,
                    target,
                    previous_midi if previous_midi is not None else -1,
                    repeat_count,
                    rest_streak,
                    previous_duration,
                    duration_streak,
                ))

    def __len__(self) -> int:
        return len(self.examples)

    def __getitem__(self, index: int) -> tuple[list[int], list[int], int, int, int, int, int, int]:
        return self.examples[index]


def collate_batch(batch: list[tuple[list[int], list[int], int, int, int, int, int, int]], pad_id: int) -> tuple[torch.Tensor, ...]:
    max_context = max(len(item[0]) for item in batch)
    max_previous = max(len(item[1]) for item in batch)
    contexts: list[list[int]] = []
    previous_tokens: list[list[int]] = []
    targets: list[int] = []
    previous_midi_values: list[int] = []
    repeat_counts: list[int] = []
    rest_streaks: list[int] = []
    previous_durations: list[int] = []
    duration_streaks: list[int] = []

    for context, previous, target, previous_midi, repeat_count, rest_streak, previous_duration, duration_streak in batch:
        contexts.append(context + [pad_id] * (max_context - len(context)))
        previous_tokens.append([pad_id] * (max_previous - len(previous)) + previous)
        targets.append(target)
        previous_midi_values.append(previous_midi)
        repeat_counts.append(repeat_count)
        rest_streaks.append(rest_streak)
        previous_durations.append(previous_duration)
        duration_streaks.append(duration_streak)

    return (
        torch.tensor(contexts, dtype=torch.long),
        torch.tensor(previous_tokens, dtype=torch.long),
        torch.tensor(targets, dtype=torch.long),
        torch.tensor(previous_midi_values, dtype=torch.long),
        torch.tensor(repeat_counts, dtype=torch.float32),
        torch.tensor(rest_streaks, dtype=torch.float32),
        torch.tensor(previous_durations, dtype=torch.long),
        torch.tensor(duration_streaks, dtype=torch.float32),
    )


class PadCollator:
    def __init__(self, pad_id: int) -> None:
        self.pad_id = pad_id

    def __call__(self, batch: list[tuple[list[int], list[int], int, int, int, int, int, int]]) -> tuple[torch.Tensor, ...]:
        return collate_batch(batch, self.pad_id)


def build_output_index_map(vocabulary_size: int, output_token_ids: list[int], device: torch.device) -> torch.Tensor:
    index_map = torch.full((vocabulary_size,), -1, dtype=torch.long, device=device)
    for index, token_id in enumerate(output_token_ids):
        index_map[token_id] = index
    return index_map


def build_constraint_masks(vocab: MelodyVocabulary, device: torch.device) -> dict[str, dict[int, torch.Tensor]]:
    return {
        "mode": {
            vocab.encode(token): build_degree_mask(vocab, degrees, device)
            for token, degrees in MODE_DEGREES.items()
        },
        "mood": {
            vocab.encode(token): build_degree_mask(vocab, degrees, device)
            for token, degrees in MOOD_DEGREES.items()
        },
        "style": {
            vocab.encode(token): build_duration_mask(vocab, durations, device)
            for token, durations in STYLE_DURATIONS.items()
        },
    }


def build_constraint_tables(vocab: MelodyVocabulary, device: torch.device) -> dict[str, torch.Tensor]:
    masks = build_constraint_masks(vocab, device)
    output_count = len(vocab.output_token_ids)
    vocabulary_size = len(vocab.id_to_token)
    tables: dict[str, torch.Tensor] = {}
    for name, mask_by_context in masks.items():
        table = torch.ones((vocabulary_size, output_count), dtype=torch.bool, device=device)
        for context_id, mask in mask_by_context.items():
            table[context_id] = mask
        tables[name] = table
    return tables


def build_degree_mask(vocab: MelodyVocabulary, allowed_degrees: set[str], device: torch.device) -> torch.Tensor:
    values = [is_degree_allowed(vocab.id_to_token[token_id], allowed_degrees) for token_id in vocab.output_token_ids]
    return torch.tensor(values, dtype=torch.bool, device=device)


def build_duration_mask(vocab: MelodyVocabulary, allowed_durations: set[str], device: torch.device) -> torch.Tensor:
    values = [is_duration_allowed(vocab.id_to_token[token_id], allowed_durations) for token_id in vocab.output_token_ids]
    return torch.tensor(values, dtype=torch.bool, device=device)


def constraint_loss(
    logits: torch.Tensor,
    context_tokens: torch.Tensor,
    constraint_tables: dict[str, torch.Tensor],
    config: TrainConfig,
) -> torch.Tensor:
    if (
        config.mode_penalty <= 0
        and config.mood_penalty <= 0
        and config.style_penalty <= 0
        and config.entropy_penalty <= 0
    ):
        return logits.new_tensor(0.0)

    probabilities = logits.softmax(dim=1)
    loss = logits.new_tensor(0.0)
    if config.style_penalty > 0:
        style_masks = constraint_tables["style"][context_tokens[:, 0]]
        loss = loss + config.style_penalty * bad_probability_mass(probabilities, style_masks)
    if config.mode_penalty > 0:
        mode_masks = constraint_tables["mode"][context_tokens[:, 1]]
        loss = loss + config.mode_penalty * bad_probability_mass(probabilities, mode_masks)
    if config.mood_penalty > 0:
        mood_masks = constraint_tables["mood"][context_tokens[:, 2]]
        loss = loss + config.mood_penalty * bad_probability_mass(probabilities, mood_masks)
    if config.entropy_penalty > 0:
        entropy = -(probabilities * (probabilities + 1e-9).log()).sum(dim=1).mean()
        loss = loss + config.entropy_penalty * entropy
    return loss


def build_behavior_tables(vocab: MelodyVocabulary, device: torch.device) -> dict[str, torch.Tensor]:
    parsed_tokens = [parse_output_token(vocab.id_to_token[token_id]) for token_id in vocab.output_token_ids]
    interval_rows = [[0.0 for _ in parsed_tokens]]
    interval_rows.extend(
        [[interval_cost(previous_midi, parsed) for parsed in parsed_tokens] for previous_midi in range(128)]
    )
    repeat_rows = [[0.0 for _ in parsed_tokens]]
    repeat_rows.extend(
        [[same_midi_cost(previous_midi, parsed) for parsed in parsed_tokens] for previous_midi in range(128)]
    )

    octave_rows: list[list[float]] = []
    for token in vocab.id_to_token:
        octave_rows.append([octave_cost(token, parsed) for parsed in parsed_tokens])

    rest_costs = [1.0 if vocab.id_to_token[token_id].startswith("R:") else 0.0 for token_id in vocab.output_token_ids]
    duration_rows = [[0.0 for _ in parsed_tokens]]
    duration_rows.extend(
        [[same_duration_cost(duration_index, vocab.id_to_token[token_id]) for token_id in vocab.output_token_ids] for duration_index in range(len(DURATION_INDEX))]
    )

    return {
        "interval": torch.tensor(interval_rows, dtype=torch.float32, device=device),
        "repeat": torch.tensor(repeat_rows, dtype=torch.float32, device=device),
        "octave": torch.tensor(octave_rows, dtype=torch.float32, device=device),
        "rest": torch.tensor(rest_costs, dtype=torch.float32, device=device),
        "duration": torch.tensor(duration_rows, dtype=torch.float32, device=device),
    }


def behavior_loss(
    logits: torch.Tensor,
    previous_midi: torch.Tensor,
    repeat_count: torch.Tensor,
    rest_streak: torch.Tensor,
    previous_duration: torch.Tensor,
    duration_streak: torch.Tensor,
    context_tokens: torch.Tensor,
    behavior_tables: dict[str, torch.Tensor],
    config: TrainConfig,
) -> torch.Tensor:
    if (
        config.interval_penalty <= 0
        and config.octave_penalty <= 0
        and config.repeat_penalty <= 0
        and config.rest_penalty <= 0
        and config.duration_penalty <= 0
    ):
        return logits.new_tensor(0.0)

    probabilities = logits.softmax(dim=1)
    loss = logits.new_tensor(0.0)

    if config.interval_penalty > 0:
        midi_indices = previous_midi.clamp(min=-1, max=127) + 1
        interval_costs = behavior_tables["interval"][midi_indices].to(probabilities.dtype)
        loss = loss + config.interval_penalty * (probabilities * interval_costs).sum(dim=1).mean()

    if config.octave_penalty > 0:
        octave_costs = behavior_tables["octave"][context_tokens[:, 2]].to(probabilities.dtype)
        loss = loss + config.octave_penalty * (probabilities * octave_costs).sum(dim=1).mean()

    if config.repeat_penalty > 0:
        midi_indices = previous_midi.clamp(min=-1, max=127) + 1
        repeat_costs = behavior_tables["repeat"][midi_indices].to(probabilities.dtype)
        repeat_scale = (repeat_count - 1.0).clamp(min=0.0, max=3.0).unsqueeze(1)
        loss = loss + config.repeat_penalty * (probabilities * repeat_costs * repeat_scale).sum(dim=1).mean()

    if config.rest_penalty > 0:
        rest_costs = behavior_tables["rest"].to(probabilities.dtype).unsqueeze(0)
        rest_scale = rest_streak.clamp(min=0.0, max=3.0).unsqueeze(1)
        loss = loss + config.rest_penalty * (probabilities * rest_costs * rest_scale).sum(dim=1).mean()

    if config.duration_penalty > 0:
        duration_indices = previous_duration.clamp(min=-1, max=len(DURATION_INDEX) - 1) + 1
        duration_costs = behavior_tables["duration"][duration_indices].to(probabilities.dtype)
        duration_scale = (duration_streak - 1.0).clamp(min=0.0, max=3.0).unsqueeze(1)
        loss = loss + config.duration_penalty * (probabilities * duration_costs * duration_scale).sum(dim=1).mean()

    return loss


def bad_probability_mass(probabilities: torch.Tensor, allowed_mask: torch.Tensor) -> torch.Tensor:
    return (probabilities * (~allowed_mask).to(probabilities.dtype)).sum(dim=1).mean()


def is_degree_allowed(token: str, allowed_degrees: set[str]) -> bool:
    if token == "<EOS>" or token.startswith("R:"):
        return True
    parts = token.split(":")
    return len(parts) >= 3 and parts[0] == "D" and parts[1] in allowed_degrees


def is_duration_allowed(token: str, allowed_durations: set[str]) -> bool:
    if token == "<EOS>":
        return True
    parts = token.split(":")
    return len(parts) >= 2 and parts[-1] in allowed_durations


def parse_output_token(token: str) -> tuple[int, int] | None:
    parts = token.split(":")
    if len(parts) == 4 and parts[0] == "D":
        return int(parts[2]), int(parts[2]) * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    if len(parts) == 3 and parts[0] == "D":
        return 4, 4 * 12 + DEGREE_SEMITONES.get(parts[1], 0)
    return None


def last_note_midi(tokens: list[str]) -> int | None:
    for token in reversed(tokens):
        parsed = parse_output_token(token)
        if parsed is not None:
            return parsed[1]
    return None


def trailing_note_repeat_count(tokens: list[str]) -> int:
    last_midi = last_note_midi(tokens)
    if last_midi is None:
        return 0

    count = 0
    for token in reversed(tokens):
        parsed = parse_output_token(token)
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


def last_duration_index(tokens: list[str]) -> int:
    for token in reversed(tokens):
        duration = token.split(":")[-1]
        if duration in DURATION_INDEX:
            return DURATION_INDEX[duration]
    return -1


def trailing_duration_count(tokens: list[str]) -> int:
    duration_index = last_duration_index(tokens)
    if duration_index < 0:
        return 0

    expected = next(duration for duration, index in DURATION_INDEX.items() if index == duration_index)
    count = 0
    for token in reversed(tokens):
        if token in {"<BOS>", "<PAD>", "<UNK>"}:
            continue
        if token.split(":")[-1] != expected:
            break
        count += 1
    return count


def interval_cost(previous_midi: int | None, parsed: tuple[int, int] | None) -> float:
    if previous_midi is None or parsed is None:
        return 0.0
    distance = abs(parsed[1] - previous_midi)
    if distance <= 7:
        return 0.0
    if distance <= 12:
        return 0.25
    return 1.0


def same_midi_cost(previous_midi: int, parsed: tuple[int, int] | None) -> float:
    if parsed is None:
        return 0.0
    return 1.0 if parsed[1] == previous_midi else 0.0


def same_duration_cost(previous_duration: int, token: str) -> float:
    duration = token.split(":")[-1]
    if duration not in DURATION_INDEX:
        return 0.0
    return 1.0 if DURATION_INDEX[duration] == previous_duration else 0.0


def octave_cost(mood: str, parsed: tuple[int, int] | None) -> float:
    if parsed is None:
        return 0.0
    octave = parsed[0]
    if octave == 4:
        return 0.0
    if octave == 5 and mood in {"MOOD_EPIC", "MOOD_TENSE"}:
        return 0.08
    if octave == 3 and mood in {"MOOD_DARK", "MOOD_CALM"}:
        return 0.08
    return 0.22


def train(args: argparse.Namespace) -> None:
    checkpoint = load_checkpoint(Path(args.resume)) if args.resume else None
    checkpoint_config = checkpoint.get("config") if checkpoint else None
    config = TrainConfig(
        embedding_size=args.embedding_size,
        heads=args.heads,
        layers=args.layers,
        feedforward_size=args.feedforward_size,
        dropout=args.dropout,
        epochs=args.epochs,
        batch_size=args.batch_size,
        learning_rate=args.learning_rate,
        label_smoothing=args.label_smoothing,
        validation_ratio=args.validation_ratio,
        seed=args.seed,
        mode_penalty=args.mode_penalty,
        mood_penalty=args.mood_penalty,
        style_penalty=args.style_penalty,
        entropy_penalty=args.entropy_penalty,
        interval_penalty=args.interval_penalty,
        octave_penalty=args.octave_penalty,
        repeat_penalty=args.repeat_penalty,
        rest_penalty=args.rest_penalty,
        duration_penalty=args.duration_penalty,
        amp=args.amp,
    )
    if checkpoint_config:
        resumed = dict(checkpoint_config)
        resumed["epochs"] = args.epochs
        resumed["batch_size"] = args.batch_size
        resumed["learning_rate"] = args.learning_rate
        resumed["label_smoothing"] = args.label_smoothing
        resumed["validation_ratio"] = args.validation_ratio
        resumed["seed"] = args.seed
        resumed["mode_penalty"] = args.mode_penalty
        resumed["mood_penalty"] = args.mood_penalty
        resumed["style_penalty"] = args.style_penalty
        resumed["entropy_penalty"] = args.entropy_penalty
        resumed["interval_penalty"] = args.interval_penalty
        resumed["octave_penalty"] = args.octave_penalty
        resumed["repeat_penalty"] = args.repeat_penalty
        resumed["rest_penalty"] = args.rest_penalty
        resumed["duration_penalty"] = args.duration_penalty
        resumed["amp"] = args.amp
        config = TrainConfig(**resumed)

    if config.embedding_size % config.heads != 0:
        raise RuntimeError("embedding-size must be divisible by heads.")

    torch.manual_seed(config.seed)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    if device.type == "cuda":
        torch.set_float32_matmul_precision("high")
    device_name = torch.cuda.get_device_name(0) if device.type == "cuda" else "cpu"
    print(f"device={device} name={device_name} amp={config.amp and device.type == 'cuda'} num_workers={args.num_workers}")

    vocab = MelodyVocabulary.load(args.vocab)
    dataset = MelodyDataset(Path(args.dataset), vocab, config)
    if len(dataset) == 0:
        raise RuntimeError("Dataset is empty.")

    collator = PadCollator(vocab.pad_id)
    validation_size = max(1, int(len(dataset) * config.validation_ratio))
    train_size = max(1, len(dataset) - validation_size)
    train_dataset, validation_dataset = random_split(
        dataset,
        [train_size, validation_size],
        generator=torch.Generator().manual_seed(config.seed),
    )
    train_loader = DataLoader(
        train_dataset,
        batch_size=config.batch_size,
        shuffle=True,
        collate_fn=collator,
        num_workers=args.num_workers,
        pin_memory=device.type == "cuda",
        persistent_workers=args.num_workers > 0,
    )
    validation_loader = DataLoader(
        validation_dataset,
        batch_size=config.batch_size,
        shuffle=False,
        collate_fn=collator,
        num_workers=args.num_workers,
        pin_memory=device.type == "cuda",
        persistent_workers=args.num_workers > 0,
    )
    model = MelodyPhraseTransformer(
        vocabulary_size=len(vocab.id_to_token),
        embedding_size=config.embedding_size,
        heads=config.heads,
        layers=config.layers,
        feedforward_size=config.feedforward_size,
        dropout=config.dropout,
        max_sequence_length=config.max_sequence_length + config.max_context_length,
    ).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=config.learning_rate)
    use_amp = config.amp and device.type == "cuda"
    scaler = torch.amp.GradScaler("cuda", enabled=use_amp)
    start_epoch = 0
    best_validation_loss = float("inf")
    metrics: list[dict[str, float | int | str]] = []

    if checkpoint:
        if checkpoint["vocabulary_size"] != len(vocab.id_to_token):
            raise RuntimeError("Checkpoint vocabulary size does not match vocab.json.")
        model.load_state_dict(checkpoint["model_state"])
        if not args.reset_optimizer and "optimizer_state" in checkpoint:
            optimizer.load_state_dict(checkpoint["optimizer_state"])
        start_epoch = int(checkpoint.get("epoch", 0))
        best_validation_loss = float(checkpoint.get("best_validation_loss", float("inf")))
        metrics = load_existing_metrics(Path(args.output_dir))
        print(f"resumed={args.resume} start_epoch={start_epoch} best_val_loss={best_validation_loss:.4f}")

    output_token_ids = torch.tensor(vocab.output_token_ids, dtype=torch.long, device=device)
    output_index_map = build_output_index_map(len(vocab.id_to_token), vocab.output_token_ids, device)
    constraint_tables = build_constraint_tables(vocab, device)
    behavior_tables = build_behavior_tables(vocab, device)
    loss_fn = nn.CrossEntropyLoss(ignore_index=-1, label_smoothing=config.label_smoothing)

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    total_target_epoch = start_epoch + config.epochs

    for local_epoch in range(1, config.epochs + 1):
        epoch = start_epoch + local_epoch
        model.train()
        total_loss = 0.0
        total_items = 0
        total_batches = len(train_loader)

        for batch_index, (
            context_tokens,
            previous_tokens,
            targets,
            previous_midi,
            repeat_count,
            rest_streak,
            previous_duration,
            duration_streak,
        ) in enumerate(train_loader, start=1):
            context_tokens = context_tokens.to(device)
            previous_tokens = previous_tokens.to(device)
            targets = targets.to(device)
            previous_midi = previous_midi.to(device)
            repeat_count = repeat_count.to(device)
            rest_streak = rest_streak.to(device)
            previous_duration = previous_duration.to(device)
            duration_streak = duration_streak.to(device)
            optimizer.zero_grad(set_to_none=True)
            with torch.amp.autocast("cuda", enabled=use_amp):
                logits = model(context_tokens, previous_tokens).index_select(1, output_token_ids)
                target_classes = output_index_map[targets]
                loss = loss_fn(logits, target_classes)
                loss = loss + constraint_loss(logits, context_tokens, constraint_tables, config)
                loss = loss + behavior_loss(
                    logits,
                    previous_midi,
                    repeat_count,
                    rest_streak,
                    previous_duration,
                    duration_streak,
                    context_tokens,
                    behavior_tables,
                    config,
                )
            scaler.scale(loss).backward()
            scaler.step(optimizer)
            scaler.update()

            total_loss += float(loss.item()) * targets.numel()
            total_items += targets.numel()
            if args.progress_every > 0 and (batch_index % args.progress_every == 0 or batch_index == total_batches):
                print(
                    f"train_progress epoch={epoch:03d}/{total_target_epoch:03d} "
                    f"batch={batch_index}/{total_batches} "
                    f"percent={batch_index / max(total_batches, 1) * 100.0:.1f} "
                    f"train_loss={total_loss / max(total_items, 1):.4f}",
                    flush=True,
                )

        train_loss = total_loss / max(total_items, 1)
        validation_loss, accuracy, top3 = evaluate(
            model,
            validation_loader,
            loss_fn,
            device,
            output_token_ids,
            output_index_map,
            constraint_tables,
            behavior_tables,
            config,
        )
        improved = validation_loss < best_validation_loss
        if improved:
            best_validation_loss = validation_loss

        metrics.append(
            {
                "epoch": epoch,
                "train_loss": train_loss,
                "validation_loss": validation_loss,
                "accuracy": accuracy,
                "top3_accuracy": top3,
                "best": improved,
            }
        )
        suffix = " best" if improved else ""
        print(
            f"epoch={epoch:03d}/{total_target_epoch:03d} "
            f"train_loss={train_loss:.4f} val_loss={validation_loss:.4f} "
            f"acc={accuracy:.3f} top3={top3:.3f}{suffix}",
            flush=True,
        )
        save_checkpoint(output_dir / "MelodyPhraseTransformer.pt", model, optimizer, epoch, best_validation_loss, config, vocab)
        if improved:
            save_checkpoint(output_dir / "best_model.pt", model, optimizer, epoch, best_validation_loss, config, vocab)
        if args.save_every > 0 and epoch % args.save_every == 0:
            save_checkpoint(output_dir / f"checkpoint_epoch_{epoch:03d}.pt", model, optimizer, epoch, best_validation_loss, config, vocab)
        write_json(output_dir / "metrics.json", metrics)

    write_json(output_dir / "training_config.json", asdict(config))
    print(f"saved={output_dir / 'MelodyPhraseTransformer.pt'}")
    print(f"best={output_dir / 'best_model.pt'}")


@torch.no_grad()
def evaluate(
    model: MelodyPhraseTransformer,
    loader: DataLoader,
    loss_fn: nn.Module,
    device: torch.device,
    output_token_ids: torch.Tensor,
    output_index_map: torch.Tensor,
    constraint_tables: dict[str, torch.Tensor],
    behavior_tables: dict[str, torch.Tensor],
    config: TrainConfig,
) -> tuple[float, float, float]:
    model.eval()
    total_loss = 0.0
    total_items = 0
    correct = 0
    top3_correct = 0
    for context_tokens, previous_tokens, targets, previous_midi, repeat_count, rest_streak, previous_duration, duration_streak in loader:
        context_tokens = context_tokens.to(device)
        previous_tokens = previous_tokens.to(device)
        targets = targets.to(device)
        previous_midi = previous_midi.to(device)
        repeat_count = repeat_count.to(device)
        rest_streak = rest_streak.to(device)
        previous_duration = previous_duration.to(device)
        duration_streak = duration_streak.to(device)
        logits = model(context_tokens, previous_tokens).index_select(1, output_token_ids)
        target_classes = output_index_map[targets]
        loss = loss_fn(logits, target_classes)
        loss = loss + constraint_loss(logits, context_tokens, constraint_tables, config)
        loss = loss + behavior_loss(
            logits,
            previous_midi,
            repeat_count,
            rest_streak,
            previous_duration,
            duration_streak,
            context_tokens,
            behavior_tables,
            config,
        )
        total_loss += float(loss.item()) * targets.numel()
        total_items += targets.numel()
        predictions = logits.argmax(dim=1)
        correct += int((predictions == target_classes).sum().item())
        top3 = logits.topk(k=min(3, logits.size(1)), dim=1).indices
        top3_correct += int((top3 == target_classes.unsqueeze(1)).any(dim=1).sum().item())
    return total_loss / max(total_items, 1), correct / max(total_items, 1), top3_correct / max(total_items, 1)


def save_checkpoint(
    path: Path,
    model: MelodyPhraseTransformer,
    optimizer: torch.optim.Optimizer,
    epoch: int,
    best_validation_loss: float,
    config: TrainConfig,
    vocab: MelodyVocabulary,
) -> None:
    torch.save(
        {
            "model_state": model.state_dict(),
            "optimizer_state": optimizer.state_dict(),
            "epoch": epoch,
            "best_validation_loss": best_validation_loss,
            "config": asdict(config),
            "vocabulary_size": len(vocab.id_to_token),
        },
        path,
    )


def load_checkpoint(path: Path) -> dict:
    return torch.load(path, map_location="cpu", weights_only=False)


def load_existing_metrics(output_dir: Path) -> list[dict[str, float | int | str]]:
    path = output_dir / "metrics.json"
    if not path.exists():
        return []
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", default="synthetic_melody_dataset_v3.jsonl")
    parser.add_argument("--vocab", default="vocab_v3.json")
    parser.add_argument("--output-dir", default="runs/melody_phrase_transformer")
    parser.add_argument("--resume")
    parser.add_argument("--reset-optimizer", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    parser.add_argument("--epochs", type=int, default=30)
    parser.add_argument("--batch-size", type=int, default=64)
    parser.add_argument("--learning-rate", type=float, default=0.0003)
    parser.add_argument("--label-smoothing", type=float, default=0.02)
    parser.add_argument("--mode-penalty", type=float, default=0.0)
    parser.add_argument("--mood-penalty", type=float, default=0.0)
    parser.add_argument("--style-penalty", type=float, default=0.0)
    parser.add_argument("--entropy-penalty", type=float, default=0.0)
    parser.add_argument("--interval-penalty", type=float, default=0.0)
    parser.add_argument("--octave-penalty", type=float, default=0.0)
    parser.add_argument("--repeat-penalty", type=float, default=0.0)
    parser.add_argument("--rest-penalty", type=float, default=0.0)
    parser.add_argument("--duration-penalty", type=float, default=0.0)
    parser.add_argument("--embedding-size", type=int, default=128)
    parser.add_argument("--heads", type=int, default=4)
    parser.add_argument("--layers", type=int, default=2)
    parser.add_argument("--feedforward-size", type=int, default=384)
    parser.add_argument("--dropout", type=float, default=0.1)
    parser.add_argument("--validation-ratio", type=float, default=0.15)
    parser.add_argument("--seed", type=int, default=1984)
    parser.add_argument("--save-every", type=int, default=0)
    parser.add_argument("--progress-every", type=int, default=50)
    parser.add_argument("--num-workers", type=int, default=0)
    parser.add_argument("--amp", action="store_true")
    return parser.parse_args()


if __name__ == "__main__":
    train(parse_args())
