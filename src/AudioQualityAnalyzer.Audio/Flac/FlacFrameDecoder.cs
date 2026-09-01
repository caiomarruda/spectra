namespace AudioQualityAnalyzer.Audio.Flac;

internal readonly record struct FlacFrameResult
{
    public required int[][] Channels { get; init; }
    public required int BlockSize { get; init; }
    public required int FrameByteLength { get; init; }
}

/// <summary>
/// Decodes one FLAC frame (RFC 9639 §9) at a given byte offset: header, one subframe per channel
/// (fixed or LPC prediction over partitioned-Rice-coded residuals, or constant/verbatim), stereo
/// decorrelation, and both header (CRC-8) and whole-frame (CRC-16) integrity checks.
/// </summary>
internal static class FlacFrameDecoder
{
    public static FlacFrameResult DecodeFrame(byte[] data, int frameStart, int dataEnd, FlacStreamInfo streamInfo)
    {
        var reader = new FlacBitReader(data, frameStart, dataEnd);

        var syncAndFlags = reader.ReadBits(16);
        if ((syncAndFlags >> 2) != 0b11111111111110)
        {
            throw new InvalidDataException($"Bad frame sync at byte offset {frameStart}.");
        }
        if (((syncAndFlags >> 1) & 1) != 0)
        {
            throw new InvalidDataException($"Frame reserved bit set at byte offset {frameStart}.");
        }

        var blockSizeCode = (int)reader.ReadBits(4);
        var sampleRateCode = (int)reader.ReadBits(4);
        var channelAssignmentCode = (int)reader.ReadBits(4);
        var sampleSizeCode = (int)reader.ReadBits(3);
        if (reader.ReadBits(1) != 0)
        {
            throw new InvalidDataException($"Frame reserved bit set at byte offset {frameStart}.");
        }

        // Coded frame/sample number: a UTF-8-like variable-length field. The header is byte-aligned
        // at this point (16 + 4+4 + 4+3+1 = 32 bits), so it can be read as whole bytes.
        SkipCodedNumber(reader);

        var blockSize = blockSizeCode switch
        {
            0b0001 => 192,
            >= 0b0010 and <= 0b0101 => 576 << (blockSizeCode - 0b0010),
            0b0110 => (int)reader.ReadBits(8) + 1,
            0b0111 => (int)reader.ReadBits(16) + 1,
            >= 0b1000 => 256 << (blockSizeCode - 0b1000),
            _ => throw new InvalidDataException($"Reserved block-size code at byte offset {frameStart}."),
        };

        if (sampleRateCode == 0b1100)
        {
            reader.ReadBits(8); // sample rate in kHz — STREAMINFO's rate is used instead, this is only consumed to stay aligned
        }
        else if (sampleRateCode is 0b1101 or 0b1110)
        {
            reader.ReadBits(16); // sample rate in Hz or tens-of-Hz — likewise unused
        }
        else if (sampleRateCode == 0b1111)
        {
            throw new InvalidDataException($"Invalid sample-rate code at byte offset {frameStart}.");
        }

        if (channelAssignmentCode > 10)
        {
            throw new InvalidDataException($"Reserved channel assignment at byte offset {frameStart}.");
        }
        var isStereoDecorrelated = channelAssignmentCode is >= 8 and <= 10;
        var frameChannelCount = isStereoDecorrelated ? 2 : channelAssignmentCode + 1;
        if (frameChannelCount != streamInfo.ChannelCount)
        {
            throw new InvalidDataException($"Frame channel count ({frameChannelCount}) doesn't match STREAMINFO ({streamInfo.ChannelCount}) at byte offset {frameStart}.");
        }

        var frameBitsPerSample = sampleSizeCode switch
        {
            0b000 => streamInfo.BitsPerSample,
            0b001 => 8,
            0b010 => 12,
            0b100 => 16,
            0b101 => 20,
            0b110 => 24,
            0b111 => 32,
            _ => throw new InvalidDataException($"Reserved sample-size code at byte offset {frameStart}."),
        };

        if (!reader.IsByteAligned)
        {
            throw new InvalidDataException($"Frame header did not end byte-aligned at byte offset {frameStart}.");
        }

        var headerLength = reader.BytePosition - frameStart;
        var expectedCrc8 = FlacCrc.ComputeCrc8(data.AsSpan(frameStart, headerLength));
        var actualCrc8 = reader.ReadAlignedByte();
        if (actualCrc8 != expectedCrc8)
        {
            throw new InvalidDataException($"Frame header CRC-8 mismatch at byte offset {frameStart}.");
        }

        var subframes = new int[streamInfo.ChannelCount][];
        for (var channel = 0; channel < streamInfo.ChannelCount; channel++)
        {
            var subframeBitsPerSample = frameBitsPerSample;
            if (isStereoDecorrelated)
            {
                // The "side" (difference) channel needs one extra bit of precision.
                var channelIsSide = channelAssignmentCode switch
                {
                    8 => channel == 1,  // left/side: channel 0 = left, channel 1 = side
                    9 => channel == 0,  // right/side: channel 0 = side, channel 1 = right
                    10 => channel == 1, // mid/side: channel 0 = mid, channel 1 = side
                    _ => false,
                };
                if (channelIsSide)
                {
                    subframeBitsPerSample++;
                }
            }
            subframes[channel] = DecodeSubframe(reader, blockSize, subframeBitsPerSample, frameStart);
        }

        reader.AlignToByte();

        var frameLengthBeforeCrc16 = reader.BytePosition - frameStart;
        var expectedCrc16 = FlacCrc.ComputeCrc16(data.AsSpan(frameStart, frameLengthBeforeCrc16));
        var crc16Hi = reader.ReadAlignedByte();
        var crc16Lo = reader.ReadAlignedByte();
        var actualCrc16 = (ushort)(((uint)crc16Hi << 8) | crc16Lo);
        if (actualCrc16 != expectedCrc16)
        {
            throw new InvalidDataException($"Frame CRC-16 mismatch at byte offset {frameStart}.");
        }

        var channels = ReconstructStereo(channelAssignmentCode, subframes, blockSize);

        return new FlacFrameResult
        {
            Channels = channels,
            BlockSize = blockSize,
            FrameByteLength = reader.BytePosition - frameStart,
        };
    }

    private static int[][] ReconstructStereo(int channelAssignmentCode, int[][] subframes, int blockSize)
    {
        switch (channelAssignmentCode)
        {
            case 8: // left/side
            {
                var left = subframes[0];
                var side = subframes[1];
                var right = new int[blockSize];
                for (var i = 0; i < blockSize; i++)
                {
                    right[i] = left[i] - side[i];
                }
                return [left, right];
            }
            case 9: // right/side
            {
                var side = subframes[0];
                var right = subframes[1];
                var left = new int[blockSize];
                for (var i = 0; i < blockSize; i++)
                {
                    left[i] = right[i] + side[i];
                }
                return [left, right];
            }
            case 10: // mid/side
            {
                var mid = subframes[0];
                var side = subframes[1];
                var left = new int[blockSize];
                var right = new int[blockSize];
                for (var i = 0; i < blockSize; i++)
                {
                    // The encoder discarded mid's LSB (mid = (l+r)>>1); recover it from side's
                    // parity, since l+r and l-r always share the same parity.
                    var mid64 = ((long)mid[i] << 1) + (side[i] & 1); // a left shift always leaves the LSB 0, so adding it in is equivalent to OR-ing it in
                    left[i] = (int)((mid64 + side[i]) >> 1);
                    right[i] = (int)((mid64 - side[i]) >> 1);
                }
                return [left, right];
            }
            default:
                return subframes;
        }
    }

    private static void SkipCodedNumber(FlacBitReader reader)
    {
        var first = reader.ReadAlignedByte();
        int continuationBytes;
        if ((first & 0x80) == 0)
        {
            continuationBytes = 0;
        }
        else if ((first & 0xE0) == 0xC0)
        {
            continuationBytes = 1;
        }
        else if ((first & 0xF0) == 0xE0)
        {
            continuationBytes = 2;
        }
        else if ((first & 0xF8) == 0xF0)
        {
            continuationBytes = 3;
        }
        else if ((first & 0xFC) == 0xF8)
        {
            continuationBytes = 4;
        }
        else if ((first & 0xFE) == 0xFC)
        {
            continuationBytes = 5;
        }
        else if (first == 0xFE)
        {
            continuationBytes = 6;
        }
        else
        {
            throw new InvalidDataException("Invalid coded frame/sample number prefix.");
        }

        for (var i = 0; i < continuationBytes; i++)
        {
            var b = reader.ReadAlignedByte();
            if ((b & 0xC0) != 0x80)
            {
                throw new InvalidDataException("Invalid coded frame/sample number continuation byte.");
            }
        }
    }

    private static int[] DecodeSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int frameStartForErrors)
    {
        if (reader.ReadBits(1) != 0)
        {
            throw new InvalidDataException($"Subframe padding bit set at frame {frameStartForErrors}.");
        }
        var subframeTypeCode = (int)reader.ReadBits(6);

        var wastedBits = 0;
        if (reader.ReadBits(1) != 0)
        {
            wastedBits = reader.ReadUnary() + 1;
        }
        var effectiveBits = bitsPerSample - wastedBits;
        if (effectiveBits <= 0)
        {
            throw new InvalidDataException($"Invalid wasted-bits count at frame {frameStartForErrors}.");
        }

        int[] samples;
        if (subframeTypeCode == 0b000000)
        {
            var value = (int)reader.ReadSignedBits(effectiveBits);
            samples = new int[blockSize];
            Array.Fill(samples, value);
        }
        else if (subframeTypeCode == 0b000001)
        {
            samples = new int[blockSize];
            for (var i = 0; i < blockSize; i++)
            {
                samples[i] = (int)reader.ReadSignedBits(effectiveBits);
            }
        }
        else if (subframeTypeCode is >= 0b001000 and <= 0b001100)
        {
            samples = DecodeFixedSubframe(reader, blockSize, effectiveBits, subframeTypeCode - 0b001000);
        }
        else if (subframeTypeCode >= 0b100000)
        {
            samples = DecodeLpcSubframe(reader, blockSize, effectiveBits, (subframeTypeCode & 0b011111) + 1);
        }
        else
        {
            throw new InvalidDataException($"Reserved subframe type at frame {frameStartForErrors}.");
        }

        if (wastedBits > 0)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] <<= wastedBits;
            }
        }

        return samples;
    }

    private static int[] DecodeFixedSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int order)
    {
        var samples = new int[blockSize];
        for (var i = 0; i < order; i++)
        {
            samples[i] = (int)reader.ReadSignedBits(bitsPerSample);
        }

        var residuals = DecodeResiduals(reader, blockSize, order);

        for (var i = order; i < blockSize; i++)
        {
            long predicted = order switch
            {
                0 => 0,
                1 => samples[i - 1],
                2 => (2L * samples[i - 1]) - samples[i - 2],
                3 => (3L * samples[i - 1]) - (3L * samples[i - 2]) + samples[i - 3],
                4 => (4L * samples[i - 1]) - (6L * samples[i - 2]) + (4L * samples[i - 3]) - samples[i - 4],
                _ => throw new InvalidDataException("Invalid fixed predictor order."),
            };
            samples[i] = (int)(residuals[i - order] + predicted);
        }

        return samples;
    }

    private static int[] DecodeLpcSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int order)
    {
        var samples = new int[blockSize];
        for (var i = 0; i < order; i++)
        {
            samples[i] = (int)reader.ReadSignedBits(bitsPerSample);
        }

        var precision = (int)reader.ReadBits(4) + 1;
        var shift = (int)reader.ReadSignedBits(5);
        var coefficients = new int[order];
        for (var i = 0; i < order; i++)
        {
            coefficients[i] = (int)reader.ReadSignedBits(precision);
        }

        var residuals = DecodeResiduals(reader, blockSize, order);

        for (var i = order; i < blockSize; i++)
        {
            long sum = 0;
            for (var j = 0; j < order; j++)
            {
                sum += (long)coefficients[j] * samples[i - 1 - j];
            }
            var predicted = shift >= 0 ? sum >> shift : sum << -shift;
            samples[i] = (int)(residuals[i - order] + predicted);
        }

        return samples;
    }

    /// <summary>Partitioned Rice coding (RFC 9639 §9.2.6/9.2.7): decodes blockSize−predictorOrder residual values.</summary>
    private static long[] DecodeResiduals(FlacBitReader reader, int blockSize, int predictorOrder)
    {
        var codingMethod = (int)reader.ReadBits(2);
        if (codingMethod is not (0 or 1))
        {
            throw new InvalidDataException("Reserved residual coding method.");
        }
        var paramBits = codingMethod == 0 ? 4 : 5;
        var escapeValue = codingMethod == 0 ? 0b1111 : 0b11111;

        var partitionOrder = (int)reader.ReadBits(4);
        var partitionCount = 1 << partitionOrder;
        if (blockSize % partitionCount != 0)
        {
            throw new InvalidDataException("Block size is not evenly divisible by the residual partition count.");
        }

        var samplesPerPartition = blockSize / partitionCount;
        if (samplesPerPartition <= predictorOrder && partitionOrder > 0)
        {
            throw new InvalidDataException("Residual partition too small for predictor order.");
        }
        if (partitionOrder == 0 && samplesPerPartition < predictorOrder)
        {
            throw new InvalidDataException("Residual partition too small for predictor order.");
        }

        var residuals = new long[blockSize - predictorOrder];
        var residualIndex = 0;

        for (var partition = 0; partition < partitionCount; partition++)
        {
            var count = partition == 0 ? samplesPerPartition - predictorOrder : samplesPerPartition;
            var riceParam = (int)reader.ReadBits(paramBits);

            if (riceParam == escapeValue)
            {
                var rawBits = (int)reader.ReadBits(5);
                for (var i = 0; i < count; i++)
                {
                    residuals[residualIndex++] = rawBits == 0 ? 0 : reader.ReadSignedBits(rawBits);
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var quotient = reader.ReadUnary();
                    var remainder = riceParam > 0 ? reader.ReadBits(riceParam) : 0;
                    var combined = ((long)quotient << riceParam) | remainder;
                    residuals[residualIndex++] = (combined & 1) != 0 ? -((combined >> 1) + 1) : combined >> 1;
                }
            }
        }

        return residuals;
    }
}
