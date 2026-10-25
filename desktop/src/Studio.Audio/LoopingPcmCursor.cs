namespace Studio.Audio;
// Platform-independent cursor; speed is changed in the MIDI schedule, never here.
public sealed class LoopingPcmCursor(float[] pcm, bool loop)
{
    private int position;
    public int Read(float[] buffer, int offset, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > Buffer.ByteLength(buffer)/sizeof(float) || count > Buffer.ByteLength(buffer)/sizeof(float)-offset) throw new ArgumentException("Buffer too small");
        if (pcm.Length == 0) return 0;
        var written = 0;
        while (written < count)
        {
            var available = Math.Min(count - written, pcm.Length - position);
            // NAudio SampleToWaveProvider supplies a WaveBuffer float view whose runtime
            // array is byte[]. Array.Copy rejects the primitive type mismatch.
            // BlockCopy deliberately copies PCM bytes without changing any sample bits.
            Buffer.BlockCopy(pcm, position*sizeof(float), buffer, (offset+written)*sizeof(float), available*sizeof(float));
            written += available; position += available;
            if (position != pcm.Length) continue;
            if (!loop) break;
            position = 0;
        }
        return written;
    }
}
