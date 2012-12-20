using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Tests
{
    /// <summary>
    /// Used for unit testing duplex streams
    /// </summary>
    public class DuplexCommunicationPipe : IDisposable
    {
        private DuplexHalf _StreamOne;
        private DuplexHalf _StreamTwo;

        public DuplexCommunicationPipe()
        {
            _StreamOne = new DuplexHalf("One");
            _StreamTwo = new DuplexHalf("Two");
            _StreamOne.ReversePath = _StreamTwo;
            _StreamTwo.ReversePath = _StreamOne;
        }

        public Stream Stream1 { get { return _StreamOne; } }
        public Stream Stream2 { get { return _StreamTwo; } }

        private class DuplexHalf : MemoryStream
        {
            private object _LockToken = new object();
            private AutoResetEvent _ARE = new AutoResetEvent(false);
            private WaitHandle[] _WaitHandles;
            private string _Name;

            public DuplexHalf(string name)
            {
                _Name = name;
                _WaitHandles = new WaitHandle[] { _ARE };
            }

            public DuplexHalf ReversePath { get; set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                ReversePath.BaseWrite(buffer, offset, count);
            }
 
            public override void WriteByte(byte value)
            {
                ReversePath.BaseWriteByte(value);
            }

            private int _LastRead = 0;
            private void BaseWrite(byte[] buffer, int offset, int count)
            {
                Trace.WriteLine(string.Format("{0} BaseWrite() Acquiring Lock: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                lock (_LockToken)
                {
                    Position = Length;
                    base.Write(buffer, offset, count);
                    Position = _LastRead;
                }

                Trace.WriteLine(string.Format("{0} BaseWrite() Setting ARE: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                _ARE.Set();
            } 

            private void WaitForReadIfNeeded()
            { 
                if (Position == Length)
                {
                    Trace.WriteLine(string.Format("{0} WaitForReadIfNeeded() Waiting To Read: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                    WaitHandle.WaitAll(_WaitHandles);
                    Trace.WriteLine(string.Format("{0} WaitForReadIfNeeded() Waiting Finished: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                } 
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                WaitForReadIfNeeded();
                Trace.WriteLine(string.Format("{0} Read Acquiring Lock: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                lock (_LockToken)
                {
                    int read = base.Read(buffer, offset, count);
                    _LastRead += read;

                    Trace.WriteLine(string.Format("{0} Read Finished: Position: {1}, Last Read: {2} Length: {3}", _Name, Position, _LastRead, Length));
                    return read;
                }
            }


            private void BaseWriteByte(byte value)
            {
                lock (_LockToken)
                {
                    Position = Length;
                    base.WriteByte(value);
                    Position = _LastRead;
                }
                _ARE.Set();
            }

            public override int ReadByte()
            {                
                WaitForReadIfNeeded();
                lock (_LockToken)
                { 
                    int result = base.ReadByte();
                    _LastRead += 1;
                    return result;
                }
            }

            protected override void Dispose(bool disposing)
            {
                _ARE.Set();            
            }

        }

        public void Dispose()
        {
            Stream1.Dispose();
            Stream2.Dispose();
        }
    }
}
