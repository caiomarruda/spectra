# Audio Analysis Specification

## 1. File Metadata

Extrair:
- filename;
- extensão;
- tamanho;
- duração;
- formato;
- MPEG version;
- layer;
- bitrate nominal;
- bitrate médio;
- bitrate mínimo;
- bitrate máximo;
- CBR/VBR;
- sample rate;
- channels;
- channel mode;
- encoder;
- encoder delay, quando disponível;
- padding, quando disponível.

### Bitrate

Não confiar apenas no metadata.

Para MP3, calcular também:

`total_audio_bits / audio_duration`

Comparar:
- Declared Bitrate
- Average Bitrate
- Bitrate Mode
- Bitrate Consistency

## 2. Audio Decoding

Decodificar para PCM antes das análises de sinal.

Preferir:
- 44.1/48 kHz;
- 32-bit float;
- stereo quando aplicável.

Usar uma biblioteca madura; não implementar decoder MP3 manualmente. FFmpeg pode ser usado como backend.

Registrar:
- decoder;
- decoder version;
- input sample rate;
- decoded sample rate.

Criar abstração `IAudioDecoder`.

## 3. Waveform

Calcular:
- peak;
- RMS;
- RMS por janela;
- peak por canal;
- RMS por canal;
- silêncio inicial/final;
- amplitude mínima/máxima.

## 4. FFT / STFT

Configuração inicial:
- Window: Hann;
- FFT Size: 4096 ou 8192;
- Overlap: 50% ou superior.

Centralizar a configuração e evitar números mágicos.

## 5. Spectral Analysis

Calcular:
- spectral centroid;
- spectral bandwidth;
- spectral rolloff;
- spectral flatness;
- spectral flux;
- spectral contrast;
- frequência máxima efetivamente presente;
- energia por bandas.

Bandas mínimas:
- 0–20 Hz
- 20–60 Hz
- 60–120 Hz
- 120–250 Hz
- 250–500 Hz
- 500 Hz–1 kHz
- 1–2 kHz
- 2–4 kHz
- 4–8 kHz
- 8–12 kHz
- 12–16 kHz
- 16–18 kHz
- 18–20 kHz
- 20–22.05 kHz

## 6. Effective Bandwidth

Não definir bandwidth simplesmente pelo último bin não-zero.

Usar energia relativa e persistência ao longo do tempo.

Procurar:
- queda consistente de energia;
- cutoff abrupto;
- frequência onde energia passa abaixo do threshold;
- comportamento do cutoff ao longo do tempo.

Registrar:
- Effective Bandwidth;
- Bandwidth Confidence;
- Cutoff Frequency;
- Cutoff Sharpness;
- Cutoff Consistency.

## 7. Spectrogram

Gerar dados para HTML, permitindo observar:
- frequência;
- tempo;
- energia;
- cortes;
- comportamento dos agudos.

Não usar spectrograma isoladamente para determinar a origem.

## 8. Loudness

Calcular:
- Integrated LUFS;
- Short-Term LUFS;
- Momentary LUFS;
- True Peak;
- Sample Peak;
- Loudness Range, quando aplicável.

Distinguir volume baixo de qualidade ruim. Volume baixo não deve automaticamente penalizar qualidade.

## 9. Dynamic Range

Calcular:
- crest factor;
- peak-to-RMS;
- RMS distribution;
- loudness variation;
- clipping;
- percentual de samples próximos de 0 dBFS.

Não usar uma métrica genérica chamada `DR` sem explicar sua metodologia.

## 10. Clipping

Detectar:
- sample clipping;
- consecutive clipped samples;
- clipping por canal;
- clipping duration;
- clipping percentage.

Distinguir clipping isolado de clipping sustentado e severo.

## 11. Stereo Analysis

Calcular:
- correlação L/R;
- diferença RMS entre canais;
- balance;
- mono compatibility;
- phase correlation;
- side/mid energy.

Detectar:
- canal praticamente ausente;
- desequilíbrio severo;
- mono disfarçado de stereo;
- problemas de fase;
- inversão de polaridade;
- excesso de conteúdo lateral.

## 12. Noise Analysis

Estimar:
- noise floor;
- silêncio;
- ruído de fundo;
- hiss;
- hum;
- low-frequency rumble.

Não penalizar automaticamente ruído: uma gravação analógica legítima pode ter noise floor elevado.

## 13. Transcoding Analysis

Combinar múltiplas evidências:
- spectral cutoff;
- cutoff sharpness;
- frequência de cutoff;
- distribuição de energia em alta frequência;
- inconsistência espectral;
- comportamento de transientes;
- características de compressão;
- encoder information;
- bitrate declarado.

Retornar:
- Transcoding Probability: 0–100;
- Confidence: LOW/MEDIUM/HIGH;
- Evidence.

Nunca afirmar a origem histórica com certeza apenas pelo áudio.

## 14. Source Quality

Separar `Source Quality` de `Encoding Quality`.

Exemplo:
```text
Encoding: Excellent
Source: Suspicious
Reason: Strong evidence of previous lossy compression.
```

## 15. Mastering Quality

Avaliar:
- clipping;
- loudness excessivo;
- crest factor;
- dinâmica;
- true peak;
- distorção aparente;
- balance espectral.

Não confundir mastering ruim com MP3 ruim.

## 16. Result Object

Criar `AudioAnalysisResult` contendo:
- FileInfo;
- FormatInfo;
- EncodingAnalysis;
- SpectralAnalysis;
- LoudnessAnalysis;
- DynamicAnalysis;
- ClippingAnalysis;
- StereoAnalysis;
- NoiseAnalysis;
- TranscodingAnalysis;
- SourceQualityAnalysis;
- MasteringAnalysis;
- OverallAssessment.

O objeto deve ser serializável para JSON.
