using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{

    /// <summary>
    /// Reads a stream until the smtp data terminator is read (\r\n\.\r\n)
    /// </summary>
    public class SmtpDataStream : Stream
    {
        private Stream _SourceStream;
        private Encoding _Encoding;
        private byte[] _Match;
        private int _LastMatchIndex = -1;
        private bool _IsTerminated = false;

        public SmtpDataStream(Stream stream, Encoding encoding)
        {
            _SourceStream = stream; 
            _Encoding = encoding;
            _Match = encoding.GetBytes("\r\n.\r\n");
        }

        #region Properties

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
            //do nothing
        }

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

        #endregion
        
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
                    if (_Match[_LastMatchIndex + 1] == (byte) byteRead)
                    {
                        _LastMatchIndex++;
                        if (_LastMatchIndex + 1 == _Match.Length)
                        {
                            //bingo, we have a match
                            _IsTerminated = true;
                        }
                    }
                    else if(_Match[0] == (byte)byteRead)
                    {
                        //does not match current sequence, but does match start of sequence
                        _LastMatchIndex = 0;
                    }
                    else
                    {
                        //reset, no match
                        _LastMatchIndex = -1;
                    }
                }
            }
            return read;
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
    }
}
