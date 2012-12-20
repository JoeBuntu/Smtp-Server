using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Service;

namespace Tests
{
    [TestClass]
    public class SmtpClientStreamTest
    { 
        private StreamReader _Reader;
        private StreamWriter _Writer;
        private DuplexCommunicationPipe _twoWayPipe;
        private SmtpStreamProcessor _StreamProcessor;

        [TestInitialize]
        public void Init()
        {
            _twoWayPipe = new DuplexCommunicationPipe();
            _Reader = new StreamReader(_twoWayPipe.Stream1);
            _Writer = new StreamWriter(_twoWayPipe.Stream1);
            _Writer.NewLine = "\r\n";
            _Writer.AutoFlush = true;

            _StreamProcessor = new SmtpStreamProcessor(_twoWayPipe.Stream2);
            new Action(_StreamProcessor.Process).BeginInvoke(null, null);
        }

        [TestCleanup]
        public void CleanUp()
        {
            _twoWayPipe.Dispose();
        }

        [TestMethod]
        public void FirstReplyIs220Greeting()
        { 
            string reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            Assert.IsTrue(reply.StartsWith("220"));
        }

        [TestMethod]
        public void AcceptsEHLO()
        {
            string reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "220");
     
            _Writer.WriteLine("EHLO localhost");

            reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "250"); 
        }

        [TestMethod]
        public void AcceptsHELO()
        {
            string reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "220");

            _Writer.WriteLine("HELO localhost");

            reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "250"); 
        }

        [TestMethod]
        public void AcceptsEHLOAndReturnsMultiLine250()
        {
            string reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "220");

            _Writer.WriteLine("EHLO localhost");

            reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "250-SIZE 10000000");

            reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "250-STARTTLS");

            reply = _Reader.ReadLine();
            Assert.IsNotNull(reply);
            StringAssert.StartsWith(reply, "250 HELP");
        }

       

    }
}
