from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import torch
from torch import nn

from context_v3 import OPTIONAL_CONTEXT_KEYS


@dataclass(frozen=True)
class MelodyVocabulary:
    token_to_id: dict[str, int]
    id_to_token: list[str]
    phrase_tokens: list[str]
    progression_tokens: list[str]
    extra_context_tokens: list[str]

    @property
    def pad_id(self) -> int:
        return self.token_to_id["<PAD>"]

    @property
    def unk_id(self) -> int:
        return self.token_to_id["<UNK>"]

    @property
    def output_token_ids(self) -> list[int]:
        return [self.token_to_id[token] for token in [*self.phrase_tokens, "<EOS>"]]

    @classmethod
    def load(cls, path: str | Path) -> "MelodyVocabulary":
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        extra_context_tokens = [
            token
            for key in OPTIONAL_CONTEXT_KEYS
            for token in data.get(key, [])
        ]
        tokens = (
            data["special_tokens"]
            + data["style_tokens"]
            + data["mode_tokens"]
            + data["mood_tokens"]
            + data["meter_tokens"]
            + data["bar_tokens"]
            + extra_context_tokens
            + data["progression_tokens"]
            + data["phrase_tokens"]
        )
        return cls(
            token_to_id={token: index for index, token in enumerate(tokens)},
            id_to_token=tokens,
            phrase_tokens=data["phrase_tokens"],
            progression_tokens=data["progression_tokens"],
            extra_context_tokens=extra_context_tokens,
        )

    def encode(self, token: str) -> int:
        return self.token_to_id.get(token, self.unk_id)


class MelodyPhraseTransformer(nn.Module):
    def __init__(
        self,
        vocabulary_size: int,
        embedding_size: int = 128,
        heads: int = 4,
        layers: int = 2,
        feedforward_size: int = 384,
        dropout: float = 0.1,
        max_sequence_length: int = 96,
    ) -> None:
        super().__init__()
        self.embedding = nn.Embedding(vocabulary_size, embedding_size)
        self.position_embedding = nn.Embedding(max_sequence_length, embedding_size)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=embedding_size,
            nhead=heads,
            dim_feedforward=feedforward_size,
            dropout=dropout,
            batch_first=True,
            activation="gelu",
        )
        self.encoder = nn.TransformerEncoder(encoder_layer, num_layers=layers)
        self.output = nn.Linear(embedding_size, vocabulary_size)

    def forward(self, context_tokens: torch.Tensor, previous_tokens: torch.Tensor) -> torch.Tensor:
        sequence = torch.cat([context_tokens, previous_tokens], dim=1)
        length = sequence.size(1)
        positions = torch.arange(length, device=sequence.device).unsqueeze(0).expand_as(sequence)
        embedded = self.embedding(sequence) + self.position_embedding(positions)
        mask = torch.triu(torch.ones(length, length, device=sequence.device), diagonal=1).bool()
        encoded = self.encoder(embedded, mask=mask)
        return self.output(encoded[:, -1, :])
