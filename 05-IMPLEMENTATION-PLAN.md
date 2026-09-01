# Implementation Plan

## Objetivo

Implementar em etapas pequenas. Cada fase deve resultar em uma aplicação executável.

## Phase 1 — Foundation

Criar solução .NET 10:

```text
Spectra.Cli
Spectra.Core
Spectra.Audio
Spectra.Analysis
Spectra.Reporting
Spectra.Tests
```

Configurar:
- nullable;
- implicit usings;
- analyzers;
- warnings;
- logging;
- DI quando fizer sentido.

Primeiro comando:
`Spectra file.mp3`

## Phase 2 — MP3 Metadata

Implementar:
- MPEG version;
- bitrate;
- CBR/VBR;
- sample rate;
- channels;
- duration;
- encoder;
- frame information.

Criar testes unitários.

Não considerar metadata como prova de qualidade.

## Phase 3 — PCM Decoder

Integrar decoder confiável.

Criar `IAudioDecoder` e `FfmpegAudioDecoder`.

Testar:
- mono;
- stereo;
- 44.1 kHz;
- 48 kHz;
- CBR;
- VBR.

## Phase 4 — Basic Signal Analysis

Implementar:
- waveform;
- RMS;
- peak;
- sample statistics;
- per-channel analysis.

Ainda não criar score.

## Phase 5 — FFT / Spectral Analysis

Implementar:
- STFT;
- FFT;
- spectrum;
- spectral centroid;
- bandwidth;
- rolloff;
- flatness;
- energy bands.

Gerar dados para HTML.

## Phase 6 — Spectral Visualization

Implementar:
- spectrum;
- spectrogram;
- effective bandwidth;
- cutoff detection.

Validar visualmente os resultados.

## Phase 7 — Loudness / Dynamics

Implementar:
- LUFS;
- true peak;
- RMS;
- crest factor;
- dynamic analysis;
- clipping detection.

## Phase 8 — Stereo

Implementar:
- L/R correlation;
- channel balance;
- phase correlation;
- mono compatibility;
- Mid/Side.

## Phase 9 — Transcoding Detection

Somente agora implementar possível transcodificação.

Usar múltiplas evidências.

Criar `TranscodingAnalyzer` com:
- Probability;
- Confidence;
- Evidence;
- Metrics.

Não usar uma regra única baseada em frequência.

## Phase 10 — Quality Scoring

Criar:
- EncodingQualityScore;
- SpectralQualityScore;
- TechnicalQualityScore;
- MasteringQualityScore;
- OverallQualityScore.

Explicar todos os scores.

## Phase 11 — HTML Reporter

Criar relatório completo com gráficos.

## Phase 12 — Excel Reporter

Criar workbook com:
- Summary;
- File Info;
- Encoding;
- Spectral;
- Loudness;
- Dynamics;
- Clipping;
- Stereo;
- Transcoding;
- Findings;
- Raw Metrics.

## Phase 13 — Validation Dataset

Fase obrigatória.

Criar coleção de referência:

```text
reference/
├── original/
├── mp3-320/
├── mp3-256/
├── mp3-192/
├── mp3-128/
├── transcoded-128-to-320/
├── transcoded-192-to-320/
└── problematic-mastering/
```

Idealmente usar a mesma música em diferentes versões:

```text
track-original.wav
track-320.mp3
track-256.mp3
track-192.mp3
track-128.mp3
track-128-transcoded-320.mp3
```

Testar:
- original → 320;
- original → 256;
- original → 192;
- original → 128;
- 128 → 320;
- 192 → 320.

## Phase 14 — Calibration

Registrar:
- falsos positivos;
- falsos negativos;
- thresholds;
- justificativa de cada threshold.

Nunca calibrar com apenas uma música.

## Phase 15 — Regression Tests

Cada problema encontrado deve virar teste.

Exemplo:
```text
Given a known 128→320 transcode
When analyzed
Then transcoding probability should be high
```

Evitar expectativas excessivamente rígidas; validar faixas razoáveis.

## CLI

Suportar:

```bash
Spectra "song.mp3"
Spectra "song.mp3" --html
Spectra "song.mp3" --sheet
Spectra "song.mp3" --html --sheet
Spectra "song.mp3" --verbose
Spectra "song.mp3" --json
```

## Critical Development Rule

Prioridade:

```text
Correctness
↓
Validation
↓
Accuracy
↓
Performance
```

Não otimizar prematuramente.

## Critical Rule About Conclusions

Evitar afirmações absolutas como:
`This is definitely a fake 320 kbps.`

Preferir:
- `Strong evidence of previous lossy compression.`
- `Possible previous lossy compression.`
- `Insufficient evidence to determine source quality.`

## Final Goal

A primeira versão deve ser:
- tecnicamente correta;
- mensurável;
- explicável;
- reproduzível;
- testável;
- fácil de calibrar.

Somente depois da análise individual estar confiável considerar:
- batch;
- biblioteca musical;
- banco de dados;
- GUI;
- MAUI;
- classificação avançada;
- machine learning.

Prioridade absoluta:

> fazer o algoritmo acertar antes de fazer a aplicação ficar bonita.
