# Spectra

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A read-only command-line tool that inspects an audio file's *technical* quality — bitrate, spectral content, loudness, dynamics, clipping, stereo image, and evidence of a prior lossy encode — and explains exactly what it measured, not just a score.

```
$ Spectra track.mp3 --json

=== Spectra ===

File:       track.mp3
Duration:   00:03:47.32
Size:       8.67 MB

VERDICT: GOOD 320 KBPS

-- Scores --
Overall:    94/100
Encoding:   98/100
Spectral:   92/100
Technical:  100/100
Mastering:  88/100

-- Transcoding --
Probability:           12% (Very unlikely)
Confidence:            High
  [Info] No spectral evidence of prior lossy encoding
```

## Why this exists

**320 kbps does not mean good quality.** A file can declare 320 kbps and still have been transcoded from a 128 kbps source — the bitrate says nothing about what actually happened to the audio. Conversely, a genuine 320 kbps MP3 can still have a bad recording or a poorly mastered source.

This tool keeps those questions separate instead of collapsing them into one number:

- **File / Encoding Quality** — is the file itself well encoded (bitrate consistency, no anomalies)?
- **Source Quality** — is there spectral evidence this was transcoded from a lower-quality source?
- **Signal / Technical Quality** — clipping, noise, channel problems, corrupted frames?
- **Mastering Quality** — loudness, dynamics, true peak — independent of the codec?
- **Overall Quality** — a composed score, always shown alongside the metrics that produced it.

No single metric decides the verdict, and nothing is stated as certain. A finding like *"possible transcoding"* always comes with a probability, a confidence level (LOW/MEDIUM/HIGH), and the raw evidence behind it — bandwidth deficit, cutoff sharpness, bitrate mismatch — so every conclusion can be checked, not just trusted.

## Read-only, always

The analyzer only ever *reads* the input file. It never writes to it, renames it, touches its tags, or modifies it in any way — decoding happens entirely in memory from bytes read with read-only file access. The only files it ever creates are the report files you explicitly ask for (`--html`, `--excel`, `--json`), written next to the input, never over it.

## Supported formats

| Format | Extensions | Notes |
|---|---|---|
| MP3 | `.mp3` | MPEG-1/2/2.5 Layer III, CBR/VBR/ABR, Xing/LAME tag aware |
| WAV | `.wav` | PCM (8/16/24/32-bit) and IEEE float (32/64-bit) |
| FLAC | `.flac` | Free Lossless Audio Codec |
| AIFF | `.aiff`, `.aif` | PCM, big-endian |

Only mono and stereo files are supported. Compressed WAV variants (A-law, µ-law, ADPCM) and AIFC (compressed AIFF) are rejected with a clear error rather than silently misread.

## What it measures

- **Encoding**: declared vs. measured average bitrate, bitrate range, CBR/VBR/ABR, frame count, Xing/LAME header presence
- **Spectral**: effective bandwidth, spectral centroid/rolloff/flatness/flux/contrast, cutoff frequency, cutoff sharpness and consistency over time, per-band energy
- **Loudness**: integrated/momentary/short-term LUFS, loudness range, sample peak, true peak
- **Dynamic range**: crest factor, RMS distribution over time, percentage of samples near full scale
- **Clipping**: clipped sample count, clip events, longest sustained clip, severity
- **Stereo**: L/R correlation, channel balance, mono compatibility, mid/side energy, phase problems, polarity inversion, mono-disguised-as-stereo detection
- **Noise**: noise floor, DC offset, excessive internal silence
- **Transcoding probability**: combines spectral cutoff deficit, cutoff sharpness, and bitrate-mismatch signals into one probability + confidence + evidence list — never a single hard frequency rule

Every analysis is one call — there's no separate "quick scan" mode that skips metrics.

## Installation

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <this-repo-url>
cd spectra-v2
dotnet build Spectra.slnx
```

Or publish a self-contained binary:

```bash
dotnet publish src/Spectra.Cli -c Release -o publish
```

## Usage

At least one export format (`--html`, `--excel`, or `--json`) is required — without one, nothing from the analysis is kept anywhere once the console output scrolls away.

```bash
# Single file
Spectra path/to/track.mp3 --json
Spectra --input path/to/track.flac --html --excel

# Batch: recursively analyze every supported file under a folder
Spectra --folder path/to/album --json
Spectra --folder path/to/library --json --threads 4

# Show every measured metric, not just the summary
Spectra path/to/track.wav --json --verbose
```

Running from source without a published binary:

```bash
dotnet run --project src/Spectra.Cli -- path/to/track.mp3 --json
```

### Options

| Flag | Description |
|---|---|
| `--input <path>` | Single-file mode (same as passing the path positionally) |
| `--folder <path>` | Recursively analyze every supported file under this folder |
| `--threads <n>` | Parallel files to analyze at once in `--folder` mode (default: all CPU cores) |
| `--html` | Export an HTML report (`OriginalName.analysis.html`, or `<FolderName>.batch-analysis.html` in `--folder` mode) |
| `--excel` | Export an Excel report (`.analysis.xlsx` / `.batch-analysis.xlsx`) |
| `--json` | Export the raw analysis data (`.analysis.json` / `.batch-analysis.json`) |
| `--verbose` | Show every measured metric, not just the summary (single file: always; `--folder`: adds per-track detail too) |

A failed export is reported but never aborts the analysis or the other exports:

```
HTML export: SUCCESS
Excel export: FAILED
Reason: File is currently open by another process.
```

## Architecture

```
Spectra
├── Cli            — argument parsing, format dispatch, orchestration
├── Core            — shared models (AudioAnalysisResult and friends), IAudioDecoder abstraction
├── Audio
│   ├── Mp3         — frame parser, metadata reader, NLayer-backed decoder
│   ├── Wav         — RIFF/WAVE parser, metadata reader, decoder
│   ├── Aiff        — FORM/AIFF parser, metadata reader, decoder
│   └── Flac        — FLAC bitstream parser, metadata reader, decoder
├── Analysis
│   ├── Waveform, Spectral, Loudness, Dynamics
│   ├── Clipping, Stereo, Noise
│   ├── Transcoding — probability scoring from spectral + encoding evidence
│   └── Scoring     — combines every analyzer into the overall assessment
├── Reporting
│   ├── Console, Html, Excel
└── Tests
```

Every signal analyzer (spectral, loudness, dynamics, clipping, stereo, noise) operates on decoded PCM (`DecodedAudio`) and has no idea which container the audio came from — only the metadata readers and decoders are format-specific. Adding a new input format means adding a new `IAudioDecoder` + metadata reader; nothing downstream changes.

The MP3 decoder is [NLayer](https://github.com/naudio/NLayer) (MIT-licensed, pure managed, no native/FFmpeg dependency). WAV, AIFF, and FLAC are decoded by hand-written, dependency-free decoders in this repo — WAV/AIFF because uncompressed PCM containers are simple enough not to need one, and FLAC as a from-scratch implementation of the [RFC 9639](https://www.rfc-editor.org/rfc/rfc9639.html) bitstream, written specifically to avoid taking on a dependency with unclear licensing.

## Testing

```bash
dotnet test
```

The test suite includes a synthetic, license-clean reference dataset (`reference/`) generated by `scripts/generate-reference-dataset.sh` — sine tones plus pink noise, transcoded through a real bitrate ladder with ffmpeg, so regression tests don't depend on copyrighted music.

## Not implemented (by design, for now)

- GUI / MAUI app
- A database or persistent history across runs
- Cloud processing or external AI/ML scoring
- Audio editing or repair — this tool only ever reads and reports

## License

[MIT](LICENSE)
