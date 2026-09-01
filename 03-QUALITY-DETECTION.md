# Quality Detection and Scoring

## Objetivo

Criar uma camada de interpretação dos dados técnicos.

O scoring não deve esconder as métricas originais. Primeiro apresentar fatos; depois interpretação.

## Encoding Quality

Avaliar:
- bitrate;
- bitrate consistency;
- CBR/VBR;
- sample rate;
- codec;
- encoder;
- inconsistências.

Exemplo:
```text
Encoding Quality: 92/100
320 kbps CBR
44.1 kHz
No bitrate anomalies
```

## Spectral Quality

Avaliar:
- effective bandwidth;
- spectral distribution;
- high-frequency energy;
- cutoff behavior;
- spectral anomalies.

Não usar regra simples como `bandwidth < 18 kHz = bad`.

## Transcoding Probability

Criar score de 0–100:
- 0–20: Very unlikely
- 21–40: Unlikely
- 41–60: Uncertain
- 61–80: Likely
- 81–100: Highly likely

Retornar também Confidence e Evidence[].

## Technical Quality

Avaliar:
- clipping;
- channel imbalance;
- phase;
- noise;
- DC offset;
- excessive silence;
- corrupted frames.

## Mastering Quality

Avaliar:
- loudness excessivo;
- limiting;
- clipping;
- baixa dinâmica;
- true peak;
- spectral imbalance.

Loudness baixo não é defeito por si só.

## Overall Score

Criar:
- EncodingQualityScore;
- SpectralQualityScore;
- TechnicalQualityScore;
- MasteringQualityScore;
- OverallQualityScore.

Mostrar a composição do score.

## Confidence

Diagnósticos importantes devem possuir:
- LOW;
- MEDIUM;
- HIGH.

Exemplo:
```text
Possible Transcoding
Probability: 76%
Confidence: MEDIUM
```

## Evidence System

Criar `AnalysisFinding` com:
- Code;
- Title;
- Severity;
- Confidence;
- Description;
- Evidence;
- Metrics.

Exemplo:
```text
Code: TRANSCODING_SPECTRAL_CUTOFF
Title: Possible previous lossy encoding
Severity: WARNING
Confidence: HIGH

Evidence:
- Effective bandwidth: 15.8 kHz
- Abrupt cutoff: detected
- High-frequency energy: unusually low
- Declared bitrate: 320 kbps
```

## Casos obrigatórios

### Caso A — bom 320
Good spectrum, dynamics, no clipping:
`GOOD 320 KBPS`

### Caso B — possível transcode
Strong previous-lossy indicators:
`320 KBPS / POSSIBLE TRANSCODE`

### Caso C — 320 legítimo, mastering ruim
Good encoding + severe clipping/low dynamics:
`VALID 320 KBPS / POOR MASTERING`

### Caso D — volume baixo
Good spectrum and dynamics + low loudness:
`VALID 320 KBPS / LOW LOUDNESS`

Volume baixo não deve virar baixa qualidade automaticamente.

## No False Certainty

Se os dados forem insuficientes:
`Unable to determine source quality with confidence.`

Isso é uma resposta válida.

## Validation Mode

Implementar:
```bash
Spectra file.mp3 --verbose
Spectra file.mp3 --json
```

`--verbose` mostra todas as métricas usadas.

`--json` exporta os dados brutos.

## Futuro Machine Learning

Não implementar ML agora.

Manter abstração que permita futuramente trocar:
`RuleBasedQualityAnalyzer`
por:
`MachineLearningQualityAnalyzer`.
