﻿// Copyright (c) .NET Foundation and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Buffers;

namespace Nerdbank.GitVersioning.ManagedGit;

internal class GitPackMemoryCacheStream : Stream
{
    private readonly Stream stream;
    private readonly MemoryStream cacheStream = new MemoryStream();
    private readonly long length;

    public GitPackMemoryCacheStream(Stream stream)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.length = this.stream.Length;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => this.length;

    /// <inheritdoc/>
    public override long Position
    {
        get => this.cacheStream.Position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        throw new NotSupportedException();
    }

#if NETSTANDARD2_0
    public int Read(Span<byte> buffer)
#else
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
#endif
    {
        if (this.cacheStream.Length < this.length
            && this.cacheStream.Position + buffer.Length > this.cacheStream.Length)
        {
            long currentPosition = this.cacheStream.Position;
            int toRead = (int)(buffer.Length - this.cacheStream.Length + this.cacheStream.Position);
            int actualRead = this.stream.Read(buffer.Slice(0, toRead));
            this.cacheStream.Seek(0, SeekOrigin.End);
            this.cacheStream.Write(buffer.Slice(0, actualRead));
            this.cacheStream.Seek(currentPosition, SeekOrigin.Begin);
            this.DisposeStreamIfRead();
        }

        return this.cacheStream.Read(buffer);
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return this.Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin != SeekOrigin.Begin)
        {
            throw new NotSupportedException();
        }

        if (offset > this.cacheStream.Length)
        {
            int toRead = (int)(offset - this.cacheStream.Length);
            var totalRead = 0;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(toRead);
            while (true)
            {
                int read = this.stream.Read(buffer, totalRead, toRead);
                totalRead += read;
                if (read == 0 || toRead == read)
                {
                    break;
                }

                toRead = toRead - read;
            }

            this.cacheStream.Seek(0, SeekOrigin.End);
            this.cacheStream.Write(buffer, 0, totalRead);
            ArrayPool<byte>.Shared.Return(buffer);

            this.DisposeStreamIfRead();
            return this.cacheStream.Position;
        }
        else
        {
            return this.cacheStream.Seek(offset, origin);
        }
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.stream.Dispose();
            this.cacheStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DisposeStreamIfRead()
    {
        if (this.cacheStream.Length == this.stream.Length)
        {
            this.stream.Dispose();
        }
    }
}
