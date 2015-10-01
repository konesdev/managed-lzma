﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// These classes contain the public metadata model. Metadata describes how to decode the streams
// contained in an archive, but does not hold any information about the files they represent.
//
// The structure of the metadata is different from the binary file format, mostly to reduce its
// complexity and to make it easier to understand. As a side effect it also allocates less arrays.
//

namespace ManagedLzma.SevenZip
{
    /// <summary>Contains the metadata required to read content from an archive.</summary>
    public sealed class ArchiveMetadata
    {
        /// <summary>The raw streams stored in the archive.</summary>
        public ImmutableArray<ArchiveFileSection> FileSections { get; }

        /// <summary>Instructions on how to decode the archive.</summary>
        public ImmutableArray<ArchiveDecoderSection> DecoderSections { get; }

        /// <summary>Constructs empty metadata.</summary>
        public ArchiveMetadata()
        {
            this.FileSections = ImmutableArray<ArchiveFileSection>.Empty;
            this.DecoderSections = ImmutableArray<ArchiveDecoderSection>.Empty;
        }

        public ArchiveMetadata(ImmutableArray<ArchiveFileSection> streams, ImmutableArray<ArchiveDecoderSection> sections)
        {
            if (streams.IsDefault)
                throw new ArgumentNullException(nameof(streams));

            if (sections.IsDefault)
                throw new ArgumentNullException(nameof(sections));

            this.FileSections = streams;
            this.DecoderSections = sections;
        }
    }

    /// <summary>Describes where a raw stream is stored in an archive.</summary>
    public sealed class ArchiveFileSection
    {
        /// <summary>The offset of the stream in the archive.</summary>
        public long Offset { get; }

        /// <summary>The length of the stream.</summary>
        public long Length { get; }

        /// <summary>The checksum of the stream, if available.</summary>
        public Checksum? Checksum { get; }

        public ArchiveFileSection(long offset, long length, Checksum? checksum)
        {
            this.Offset = offset;
            this.Length = length;
            this.Checksum = checksum;
        }
    }

    /// <summary>Describes how to decode a section of an archive.</summary>
    public sealed class ArchiveDecoderSection
    {
        /// <summary>The decoders required to decode the section.</summary>
        public ImmutableArray<DecoderMetadata> Decoders { get; }

        /// <summary>Describes where to obtain the decoded streams.</summary>
        public DecoderInputMetadata DecodedStream { get; }

        /// <summary>The total length of the decoded streams.</summary>
        public long Length { get; }

        /// <summary>The checksum over all decoded streams, if available.</summary>
        public Checksum? Checksum { get; }

        /// <summary>Information about the streams contained in this section.</summary>
        public ImmutableArray<DecodedStreamMetadata> Streams { get; }

        public ArchiveDecoderSection(ImmutableArray<DecoderMetadata> decoders, DecoderInputMetadata decodedStream, long length, Checksum? checksum, ImmutableArray<DecodedStreamMetadata> sections)
        {
            if (decoders.IsDefault)
                throw new ArgumentNullException(nameof(decoders));

            if (sections.IsDefault)
                throw new ArgumentNullException(nameof(sections));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            this.Decoders = decoders;
            this.DecodedStream = decodedStream;
            this.Length = length;
            this.Checksum = checksum;
            this.Streams = sections;
        }
    }

    /// <summary>Information about a stream which can be decoded from an archive.</summary>
    public struct DecodedStreamMetadata
    {
        /// <summary>The length of the decoded stream.</summary>
        public long Length { get; internal set; }

        /// <summary>The checksum of the decoded stream, if available.</summary>
        public Checksum? Checksum { get; internal set; }

        public DecodedStreamMetadata(long length, Checksum? checksum)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            this.Length = length;
            this.Checksum = checksum;
        }
    }

    /// <summary>Describes how to configure a decoder and how to connect it to other decoders.</summary>
    public sealed class DecoderMetadata
    {
        /// <summary>The type of the decoder.</summary>
        public CompressionMethod DecoderType { get; }

        /// <summary>Additional settings for the decoder. What exactly is stored here depends on the decoder type.</summary>
        public ImmutableArray<byte> Settings { get; }

        /// <summary>Describes where the decoder should obtain its inputs. The number of inputs depends on the decoder type.</summary>
        public ImmutableArray<DecoderInputMetadata> InputStreams { get; }

        /// <summary>Describes the output of the decoder. The number of outputs depends on the decoder type.</summary>
        public ImmutableArray<DecoderOutputMetadata> OutputStreams { get; }

        public DecoderMetadata(CompressionMethod type, ImmutableArray<byte> settings, ImmutableArray<DecoderInputMetadata> inputs, ImmutableArray<DecoderOutputMetadata> outputs)
        {
            if (type.IsUndefined)
                throw new ArgumentOutOfRangeException(nameof(type));

            if (settings.IsDefault)
                throw new ArgumentNullException(nameof(settings));

            if (inputs.IsDefault)
                throw new ArgumentNullException(nameof(inputs));

            if (outputs.IsDefault)
                throw new ArgumentNullException(nameof(outputs));

            this.DecoderType = type;
            this.Settings = settings;
            this.InputStreams = inputs;
            this.OutputStreams = outputs;
        }
    }

    /// <summary>Describes where a decoder obtains its input.</summary>
    public struct DecoderInputMetadata
    {
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private readonly int mDecoderIndex;

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private readonly int mStreamIndex;

        public DecoderInputMetadata(int? decoderIndex, int streamIndex)
        {
            if (decoderIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(decoderIndex));

            if (streamIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(streamIndex));

            mDecoderIndex = decoderIndex ?? -1;
            mStreamIndex = streamIndex;
        }

        /// <summary>
        /// The index of the decoder which provides the stream,
        /// or null if the stream is stored directly in the archive.
        /// </summary>
        public int? DecoderIndex => mDecoderIndex >= 0 ? mDecoderIndex : default(int?);

        /// <summary>
        /// The index of the stream. If the stream is provided by a decoder then this is an
        /// index into its output streams. If the stream is stored in the archive then this
        /// is an index into the file sections from the archive metadata.
        /// </summary>
        public int StreamIndex => mStreamIndex;
    }

    /// <summary>Describes the output of a decoder.</summary>
    public struct DecoderOutputMetadata
    {
        /// <summary>The length of the output stream provided by the decoder.</summary>
        public long Length { get; }

        public DecoderOutputMetadata(long length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            this.Length = length;
        }
    }
}
