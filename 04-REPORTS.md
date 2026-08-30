# Reporting Specification

## Objetivo

Manter a exportação de relatório HTML e Excel.

Os relatórios devem ser baseados exclusivamente em `AudioAnalysisResult`.

Arquitetura:

```text
AudioAnalysisResult
├── Console Reporter
├── HTML Reporter
└── Excel Reporter
```

Não duplicar lógica de análise nos exporters.

## Console

Mostrar:
- filename;
- duração;
- formato;
- bitrate;
- sample rate;
- canais;
- análise espectral;
- loudness;
- dinâmica;
- clipping;
- stereo;
- transcoding;
- scores;
- verdict.

## HTML

Criar relatório moderno e legível.

Seções:
1. Header
2. Executive Summary
3. File Information
4. Spectral Analysis
5. Loudness
6. Dynamic Range
7. Stereo
8. Findings
9. Technical Details

Executive Summary deve mostrar:
- Overall Score;
- Encoding Quality;
- Spectral Quality;
- Technical Quality;
- Mastering Quality;
- Transcoding Probability;
- Confidence.

### Gráficos

Incluir dados reais para:
- waveform;
- spectrum;
- spectrogram;
- loudness over time;
- RMS over time;
- peak over time;
- stereo correlation over time.

O relatório deve permitir entender visualmente o motivo das conclusões.

## Excel

Gerar `.xlsx` com worksheets:
- Summary
- File Info
- Encoding
- Spectral
- Loudness
- Dynamics
- Clipping
- Stereo
- Transcoding
- Findings
- Raw Metrics

### Summary

Mostrar:
- Track;
- Format;
- Bitrate;
- Sample Rate;
- Duration;
- Overall Score;
- Encoding Score;
- Spectral Score;
- Technical Score;
- Mastering Score;
- Transcoding Probability;
- Confidence;
- Verdict.

### Raw Metrics

Incluir todas as métricas calculadas.

Isso é obrigatório para validar e comparar o algoritmo.

## Naming

Usar:
`OriginalName.analysis.html`
`OriginalName.analysis.xlsx`

Exemplo:
`Daft Punk - One More Time.analysis.html`

## Export Errors

Falha na exportação não pode interromper a análise.

Exemplo:
```text
Analysis completed successfully.
HTML export: SUCCESS
Excel export: FAILED
Reason: File is currently open by another process.
```
