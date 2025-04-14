using System.IO;
using System.Threading;

namespace SharpSid;

public class CircularBufferStream : Stream
{
    private readonly byte[] _buffer;
    public int ReadPosition { get; private set; }
    public int WritePosition { get; private set; }
    private readonly int _size;
    private readonly Lock _lock = new();

    public CircularBufferStream(int size)
    {
        _buffer = new byte[size];
        _size = size;
        
        ReadPosition = 0;
        WritePosition = 0;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => _size;
    public override long Position { get => ReadPosition; set => ReadPosition = (int)value; }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            var bytesRead = 0;
            while (ReadPosition != WritePosition && bytesRead < count)
            {
                buffer[offset + bytesRead] = _buffer[ReadPosition];
                ReadPosition = (ReadPosition + 1) % _size;
                bytesRead++;
            }

            return bytesRead;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return 0;
    }

    public override void SetLength(long value)
    {
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            for (var i = 0; i < count; i++)
            {
                _buffer[WritePosition] = buffer[offset + i];
                WritePosition = (WritePosition + 1) % _size;

                if (WritePosition == ReadPosition)
                {
                    ReadPosition = (ReadPosition + 1) % _size;
                }
            }
        }
    }
    
    public int GetBufferedByteCount()
    {
        lock (_lock)
        {
            if (WritePosition >= ReadPosition)
            {
                return WritePosition - ReadPosition;
            }
            else
            {
                return _size - (ReadPosition - WritePosition);
            }
        }
    }
}
