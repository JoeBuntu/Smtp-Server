using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO;

namespace Service
{
    public class SmtpClientProcessor : IDisposable
    {
        private TcpClient _Client;
        private SmtpSecurity _SecurityMode;
        private X509Certificate _Certificate;
        private Stream _UnderlyingStream;
        private LoggingStreamReader _Reader;
        private LoggingStreamWriter _Writer;
        private ScopedActivity _Activity;
        private IMailPackageQueue _MailPackageQueue;
        private const string SERVER_LABEL = "S";
        private const string CLIENT_LABEL = "C";

        #region Initialization

        public SmtpClientProcessor(TcpClient client )
            : this(client, SmtpSecurity.None, null)
        { 
        }

        public SmtpClientProcessor(TcpClient client, SmtpSecurity securityMode, X509Certificate certificate)
        {           
            _Client = client;
            _SecurityMode = securityMode;
            _Certificate = certificate;
            _Activity = new ScopedActivity("SmtpClientProcessor");
            _MailPackageQueue = Kernel.Get<IMailPackageQueue>();
        }

        #endregion

        public void Process()
        {
            //establish data streams
            PrepareUnderlyingDataStream();
            PrepareReaderStream();
            PrepareWriterStream();

            ProcessCore();
        }

        private void ProcessCore()
        {
            MailPackage mailPackage = new MailPackage();
            mailPackage.Received = DateTime.Now;

            //first and foremost issue standard greeting
            _Writer.WriteLineWithLogging(ServerCommands.GREETING_220, SERVER_LABEL);
            

            //begin command exchange...
            for (string line = _Reader.ReadLineWithLogging(CLIENT_LABEL); line != null; line = _Reader.ReadLineWithLogging(CLIENT_LABEL))
            {
                if (line.StartsWith(ClientCommands.HELO))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.SIZE_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.STARTTLS_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.HELP_250, SERVER_LABEL);
                    mailPackage.Host = line.Replace(ClientCommands.HELO, string.Empty).Trim();
                }
                if (line.StartsWith(ClientCommands.EHLO)) //Extended HELO
                {
                    _Writer.WriteLineWithLogging(ServerCommands.SIZE_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.STARTTLS_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.HELP_250, SERVER_LABEL);
                    mailPackage.Host = line.Replace(ClientCommands.EHLO, string.Empty).Trim();
                }
                else if (line.StartsWith(ClientCommands.STARTTLS))
                {
                    //make sure security mode is set to STARTTLS
                    if(_SecurityMode == SmtpSecurity.STARTTLS)
                    {
                        _Writer.WriteLineWithLogging(ServerCommands.READY_FOR_STARTTLS, SERVER_LABEL);

                        //switch to over to ssl
                        _UnderlyingStream = EstablishSsl();
                        PrepareReaderStream();
                        PrepareWriterStream();
                    }
                }
                else if (line.StartsWith(ClientCommands.MAIL_FROM))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                    mailPackage.From = line.Replace(ClientCommands.MAIL_FROM, string.Empty).Trim();
                }
                else if (line.StartsWith(ClientCommands.RCPT_TO))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                    mailPackage.Tos.Add(line.Replace(ClientCommands.RCPT_TO, string.Empty).Trim());
                }
                else if (line.StartsWith(ClientCommands.DATA))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.START_DATA_354, SERVER_LABEL);
                    _Activity.Log("Reading data...");

                    mailPackage.ReferenceId = _Activity.ActivityId;
                    Stream data = new SmtpDataStream(_UnderlyingStream, _Reader.CurrentEncoding);
                    _MailPackageQueue.Add(mailPackage, data);

                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);

                }
                else if(line.StartsWith(ClientCommands.QUIT))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.BYE_221, SERVER_LABEL);
                }
            } 
        }
 
        private void PrepareUnderlyingDataStream()
        {
            //if ssl security, perform server authentication
            if (_SecurityMode == SmtpSecurity.SSL)
            {
                _UnderlyingStream = EstablishSsl();
            }
            else
            {
                _UnderlyingStream = _Client.GetStream();
            }
        }

        private void PrepareReaderStream()
        {
            _Reader = new LoggingStreamReader(_UnderlyingStream, _Activity);
        }

        private void PrepareWriterStream()
        {
            _Writer = new LoggingStreamWriter(_UnderlyingStream, _Activity);
            _Writer.AutoFlush = true;
            _Writer.NewLine = "\r\n";
        }

        private SslStream EstablishSsl()
        {
            //perform server authentication, do not authenticate client
            SslStream ssl = new SslStream(_Client.GetStream(), false);
            ssl.AuthenticateAsServer(_Certificate, false, System.Security.Authentication.SslProtocols.Default, false);            

            //throw exception if authentication failed
            if (!ssl.IsAuthenticated)
            {
                throw new ApplicationException("Unable to Authenticate As Server");
            }

            return ssl;
        }
 
        public void Dispose()
        {
            try
            {
                if (_Writer != null)
                {
                    _Writer.Dispose();
                }
                if (_Reader != null)
                {
                    _Reader.Dispose();
                }
                if (_UnderlyingStream != null)
                {
                    _UnderlyingStream.Dispose();
                }
            }
            finally
            {
                if(_Activity != null)
                {
                    _Activity.Dispose();
                }
            }
        }

        private static class ClientCommands
        {
            public const string STARTTLS = "STARTTLS";
            public const string EHLO = "EHLO";
            public const string HELO = "HELO";
            public const string RCPT_TO = "RCPT TO";
            public const string MAIL_FROM = "MAIL FROM";
            public const string QUIT = "QUIT";
            public const string DATA = "DATA";
            public const string END_DATA = ".";
        }

        private static class ServerCommands
        { 
            public const string GREETING_220 = "220 localhost PensionPro Software Smtp";
            public const string READY_FOR_STARTTLS = "220 Ready to start TLS";

            public const string OK_250 = "250 OK";
            public const string BYE_221 = "221 Bye";

            public const string SIZE_250 = "250-SIZE 10000000";
            public const string STARTTLS_250 = "250-STARTTLS";
            public const string HELP_250 = "250 HELP";

            public const string START_DATA_354 = "354 Start mail input; end with";
        }
    }
}
