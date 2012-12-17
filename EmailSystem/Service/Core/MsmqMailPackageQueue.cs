using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Messaging;
using System.IO;

namespace Service 
{
    public class MsmqMailPackageQueue : IMailPackageQueue
    {
        private MessageQueue _MQ;
        private IMessageFormatter _Formatter;
        private IDataStreamRepository _StreamRepository;

        public MsmqMailPackageQueue(string path, IMessageFormatter formatter, IDataStreamRepository streamRepository)
        {
            if (MessageQueue.Exists(path))
            {
                _MQ = new MessageQueue(path);
            }
            else
            {
                _MQ = MessageQueue.Create(path);
            }
            _Formatter = formatter;
        }

        public void Add(MailPackage package, Stream data)
        { 
            //first add the stream to the repository
            string uniqueId = string.Format("{0}.{1:MMddyyyyHHmmssfffff}", package.ReferenceId, package.Received);
            _StreamRepository.Add(data, uniqueId); 
 
            //send the rest of the message details to msmq
            Message msg = new Message(); 
            msg.Formatter = _Formatter;
            msg.Body = package;
            _MQ.Send(msg);
        }
    }
}
