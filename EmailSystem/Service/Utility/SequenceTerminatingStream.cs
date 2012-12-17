using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    /// <summary>
    /// Read-Only stream that will stop reading at the first occurrence of a specified byte sequence
    /// </summary>
    public class SequenceTerminatingStream : Stream
    {
        private Stream _SourceStream;
        private Encoding _Encoding;
        private byte[] _Sequence;
        private int _LastSequenceMatchIndex = -1;
        private bool _IsTerminated = false;

        public SequenceTerminatingStream(Stream stream, byte[] sequence)
        {
            _SourceStream = stream;
            _Sequence = sequence;
        }
 
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        { 
        } 

        public override int Read(byte[] buffer, int offset, int count)
        {
            //check that offset + requested byte count fits buffer
            if (buffer.Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }

            int read = 0;
            int byteRead = 0;

            //only continue if read bytes is less than requested
            //the stream terminator has not been read
            //and the last byte read is not -1
            while (read < count && !_IsTerminated && byteRead > -1)
            {
                //read a byte
                byteRead = _SourceStream.ReadByte(); 

                //do nothing if the byte is -1, this is used to mark the end of the stream
                if(byteRead > -1)
                {
                    //this is a valid byte, set the buffer value
                    read++;
                    buffer[offset + read - 1] = (byte) byteRead;
 
                    //if the current match index is the same, continue forward with match
                    if (_Sequence[_LastSequenceMatchIndex + 1] == (byte) byteRead)
                    {
                        _LastSequenceMatchIndex++;
                    }
                    else if(_Sequence[0] == (byte)byteRead)
                    {
                        //does not match current sequence, but does match start of sequence
                        _LastSequenceMatchIndex = 0;
                    }                    
                    else                    
                    { 
                        //reset, no match
                        _LastSequenceMatchIndex = -1;
                    }                    
                    if (_LastSequenceMatchIndex + 1 == _Sequence.Length)
                    {
                        //bingo, we have a match
                        _IsTerminated = true;
                    }
                }
            }
            return read;
        }

        #region Not Supported

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
