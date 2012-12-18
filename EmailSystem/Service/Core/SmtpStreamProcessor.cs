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
    public class SmtpStreamProcessor : IDisposable
    {  
        private SmtpSecurity _SecurityMode;
        private X509Certificate _Certificate;
        private Stream _SourceStream;
        private Stream _IntermediateStream;
        private LoggingStreamReader _Reader;
        private LoggingStreamWriter _Writer;
        private ScopedActivity _Activity;
        private IMailPackageQueue _MailPackageQueue;
        private const string SERVER_LABEL = "S";
        private const string CLIENT_LABEL = "C";
        private MailPackage _CurrentMailPackage;

        #region Initialization

        public SmtpStreamProcessor(Stream stream)
            : this(stream, SmtpSecurity.None, null)
        { 
        }

        public SmtpStreamProcessor(Stream stream, SmtpSecurity securityMode, X509Certificate certificate)
        {           
            _SourceStream = stream;
            _SecurityMode = securityMode;
            _Certificate = certificate;
            _Activity = new ScopedActivity("SmtpClientProcessor");
            _MailPackageQueue = Kernel.Get<IMailPackageQueue>();
        }

        #endregion

        public void Process()
        {
            //establish data streams
            PrepareIntermediateDataStream();
            PrepareReaderStream();
            PrepareWriterStream();

            ProcessCore();
        }

        private void ProcessCore()
        {
            MailPackage mailPackage = new MailPackage();

            //first and foremost issue standard greeting
            _Writer.WriteLineWithLogging(ServerCommands.GREETING_220, SERVER_LABEL);
 
            //begin command exchange... 
            for (string line = _Reader.ReadLineWithLogging(CLIENT_LABEL); line != null; line = _Reader.ReadLineWithLogging(CLIENT_LABEL))
            {
                //HELO = Basic hello
                if (line.StartsWith(ClientCommands.HELO))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.SIZE_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.STARTTLS_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.HELP_250, SERVER_LABEL);
                    mailPackage.Host = line.Replace(ClientCommands.HELO, string.Empty).Trim();
                }
                //EHLO = Extended Helo, Extended SMTP commands 
                else if (line.StartsWith(ClientCommands.EHLO))  
                {
                    _Writer.WriteLineWithLogging(ServerCommands.SIZE_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.STARTTLS_250, SERVER_LABEL);
                    _Writer.WriteLineWithLogging(ServerCommands.HELP_250, SERVER_LABEL);
                    mailPackage.Host = line.Replace(ClientCommands.EHLO, string.Empty).Trim();
                }

                //NOOP = No Operation, this equivalent to a ping
                else if (line.StartsWith(ClientCommands.NOOP))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                }

                //STARTTLS - Start Transport Layer Security
                else if (line.StartsWith(ClientCommands.STARTTLS))
                {
                    //make sure security mode is set to STARTTLS
                    if (_SecurityMode == SmtpSecurity.STARTTLS)
                    {
                        _Writer.WriteLineWithLogging(ServerCommands.READY_FOR_STARTTLS, SERVER_LABEL);

                        //switch to over to ssl
                        _IntermediateStream = EstablishSsl();
                        PrepareReaderStream();
                        PrepareWriterStream();
                    }
                }
                //VRFY - used to confirm users, not implementing at this time
                else if (line.StartsWith(ClientCommands.VRFY))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.ERROR_COMMAND_NOT_IMPLEMENTED, SERVER_LABEL);
                }
                //EXPN - expand? not implementing at this time
                else if(line.StartsWith(ClientCommands.EXPN))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.ERROR_COMMAND_NOT_IMPLEMENTED, SERVER_LABEL);
                }
                //MAIL FROM = sender address. Indicates start of email transaction
                else if (line.StartsWith(ClientCommands.MAIL_FROM))
                {
                    //clear out forward-path, reverse-path and data 'buffers'
                    _CurrentMailPackage.From = null;
                    _CurrentMailPackage.Tos.Clear();
                    _CurrentMailPackage.Received = DateTime.Now;

                    //respond
                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                    mailPackage.From = line.Replace(ClientCommands.MAIL_FROM, string.Empty).Trim();
                }
                //
                else if (line.StartsWith(ClientCommands.RCPT_TO))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                    mailPackage.Tos.Add(line.Replace(ClientCommands.RCPT_TO, string.Empty).Trim());
                }
                else if (line.StartsWith(ClientCommands.DATA))
                {
                    //only accept data if other parts of mail transaction have been fullfilled
                    if (OkToRecieveData)
                    {
                        _Writer.WriteLineWithLogging(ServerCommands.START_DATA_354, SERVER_LABEL);
                        _Activity.Log("Reading data...");

                        mailPackage.ReferenceId = _Activity.ActivityId;
                        byte[] dataStreamTerminator = _Reader.CurrentEncoding.GetBytes("/r/n./r/n");
                        Stream data = new SequenceTerminatingStream(_IntermediateStream, dataStreamTerminator);
                        _MailPackageQueue.Add(mailPackage, data);

                        _Writer.WriteLineWithLogging(ServerCommands.OK_250, SERVER_LABEL);
                    }
                    else
                    {
                        if (_CurrentMailPackage.Tos.Any())
                        {
                            _Writer.WriteLineWithLogging(ServerCommands.ERROR_COMMAND_OUT_OF_SEQUENCE, SERVER_LABEL);
                        }
                        else
                        {
                            _Writer.WriteLineWithLogging(ServerCommands.ERROR_NO_VALID_RECIPIENTS, SERVER_LABEL);
                        }
                    }
                }
                else if (line.StartsWith(ClientCommands.QUIT))
                {
                    _Writer.WriteLineWithLogging(ServerCommands.BYE_221, SERVER_LABEL);
                }
                //unknown command
                else
                {
                    _Writer.WriteLineWithLogging(ServerCommands.ERROR_COMMAND_UNRECOGNIZED, SERVER_LABEL);
                }
            } 
        }

        private bool OkToRecieveData
        {
            get
            {
                return _CurrentMailPackage != null
                    && _CurrentMailPackage.From != null
                    && _CurrentMailPackage.Tos.Any();
            }
        }
 
        private void PrepareIntermediateDataStream()
        {
            //if ssl security, perform server authentication
            if (_SecurityMode == SmtpSecurity.SSL)
            {
                _IntermediateStream = EstablishSsl();
            }
            else
            {
                _IntermediateStream = _SourceStream;
            }
        }

        private void PrepareReaderStream()
        {
            _Reader = new LoggingStreamReader(_IntermediateStream, _Activity);
        }

        private void PrepareWriterStream()
        {
            _Writer = new LoggingStreamWriter(_IntermediateStream, _Activity);
            _Writer.AutoFlush = true;
            _Writer.NewLine = "\r\n";
        }

        private SslStream EstablishSsl()
        {
            //perform server authentication, do not authenticate client
            SslStream ssl = new SslStream(_SourceStream, false);
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
                if (_IntermediateStream != null)
                {
                    _IntermediateStream.Dispose();
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
            public const string NOOP = "NOOP";
            public const string RCPT_TO = "RCPT TO";
            public const string MAIL_FROM = "MAIL FROM";
            public const string QUIT = "QUIT";
            public const string DATA = "DATA";
            public const string END_DATA = ".";
            public const string VRFY = "VRFY";
            public const string EXPN = "EXPN";

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

            public const string ERROR_COMMAND_OUT_OF_SEQUENCE = "503";
            public const string ERROR_NO_VALID_RECIPIENTS = "554";
            public const string ERROR_COMMAND_NOT_IMPLEMENTED = "502";
            public const string ERROR_COMMAND_UNRECOGNIZED = "500";
            public const string ERROR_TRANSACTION_FAILED = "554";       

            public const string START_DATA_354 = "354 Start mail input; end with";
        }
    }
}
