using System;
using System.IO;

namespace PlanetaryProcessor
{
    /// <summary>
    /// A file stream that deletes the file after disposal
    /// </summary>
    public class DestructableFileStream : Stream
    {
        /// <summary>
        /// The stream that is wrapped
        /// </summary>
        private readonly FileStream _streamImplementation;

        /// <summary>
        /// The path where the file is located
        /// </summary>
        private String _path;

        /// <summary>
        /// Create a new DestructableFileStream
        /// </summary>
        internal DestructableFileStream(FileStream baseStream, String filePath)
        {
            _path = filePath;
            _streamImplementation = baseStream;
        }

        public override void Flush()
        {
            _streamImplementation.Flush();
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            return _streamImplementation.Read(buffer, offset, count);
        }

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
        {
            return _streamImplementation.Seek(offset, origin);
        }

        public override void SetLength(Int64 value)
        {
            _streamImplementation.SetLength(value);
        }

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            _streamImplementation.Write(buffer, offset, count);
        }

        public override Boolean CanRead
        {
            get { return _streamImplementation.CanRead; }
        }

        public override Boolean CanSeek
        {
            get { return _streamImplementation.CanSeek; }
        }

        public override Boolean CanWrite
        {
            get { return _streamImplementation.CanWrite; }
        }

        public override Int64 Length
        {
            get { return _streamImplementation.Length; }
        }

        public override Int64 Position
        {
            get { return _streamImplementation.Position; }
            set { _streamImplementation.Position = value; }
        }

        protected override void Dispose(Boolean disposing)
        {
            _streamImplementation.Dispose();
            File.Delete(_path);
        }
    }
}