# Audio Quality Analyzer

## Objetivo

Criar uma aplicação Console em .NET 10 para análise técnica de arquivos de áudio, inicialmente com foco em MP3.

A primeira versão deve analisar uma única música por execução e produzir dados técnicos detalhados para validar a qualidade dos algoritmos.

A aplicação deverá identificar:
- bitrate declarado e real;
- CBR/VBR;
- sample rate;
- canais;
- duração;
- características espectrais;
- bandwidth efetivo;
- possíveis cortes espectrais;
- clipping;
- loudness;
- dinâmica;
- problemas estéreo;
- possíveis sinais de transcodificação;
- possíveis fontes de baixa qualidade;
- qualidade técnica;
- qualidade do encoding;
- qualidade do mastering;
- score geral;
- confiança das conclusões.

## Princípio fundamental

Não considerar `320 kbps = boa qualidade`.

Um MP3 pode declarar 320 kbps e ter sido criado a partir de uma fonte de 128 kbps. Um MP3 legítimo de 320 kbps também pode ter uma gravação ou masterização ruim.

Separar:
- File / Encoding Quality
- Source Quality
- Signal Quality
- Mastering Quality
- Overall Quality

## Filosofia

Priorizar:
1. dados mensuráveis;
2. transparência;
3. explicabilidade;
4. reprodução dos resultados;
5. baixo número de falsos positivos;
6. capacidade de validar cada conclusão.

Quando não houver evidência suficiente, usar conclusões como `Possible transcoding` com nível de confiança, e não afirmações absolutas.

## Primeira versão

Entrada:

```bash
AudioQualityAnalyzer "/path/to/music.mp3"
AudioQualityAnalyzer --input "/path/to/music.mp3"
```

Exportação:

```bash
AudioQualityAnalyzer "/path/to/music.mp3" --html
AudioQualityAnalyzer "/path/to/music.mp3" --excel
AudioQualityAnalyzer "/path/to/music.mp3" --html --excel
AudioQualityAnalyzer "/path/to/music.mp3" --json
AudioQualityAnalyzer "/path/to/music.mp3" --verbose
```

## Não implementar inicialmente

- MAUI;
- GUI;
- processamento em lote;
- banco de dados;
- cloud;
- machine learning;
- IA externa;
- edição de áudio.

## Arquitetura

```text
AudioQualityAnalyzer
├── CLI
├── Application
├── Audio
│   ├── Decoder
│   ├── Metadata
│   └── Waveform
├── Analysis
│   ├── Spectral
│   ├── Loudness
│   ├── DynamicRange
│   ├── Clipping
│   ├── Stereo
│   └── Transcoding
├── Scoring
├── Reporting
│   ├── Console
│   ├── HTML
│   └── Excel
└── Tests
```

## Requisito importante

Cada análise deve produzir os dados usados para chegar ao resultado.

Exemplo:

```text
Transcoding Probability: 78%
Confidence: HIGH

Evidence:
- Effective bandwidth: 15.7 kHz
- Abrupt spectral cutoff: detected
- High-frequency energy: unusually low
- Declared bitrate: 320 kbps
```

Nenhuma métrica isolada deve determinar a qualidade.
