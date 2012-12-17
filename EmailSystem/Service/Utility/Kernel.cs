using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Service
{
    public class Kernel
    {
        public static T Get<T>() where T : class
        {
            T retVal = null;
            System.Type typeOfT = typeof(T);
            if (typeof(IMailPackageQueue).IsAssignableFrom(typeOfT))
            {
                string path = @".\Private$\EmailService";
                System.Messaging.IMessageFormatter formatter = Get<System.Messaging.IMessageFormatter>();
                IDataStreamRepository streamRepository = Get<IDataStreamRepository>();
                retVal = new MsmqMailPackageQueue(path, formatter, streamRepository) as T;
            }
            if (typeof(System.Messaging.IMessageFormatter).IsAssignableFrom(typeOfT))
            {
                retVal = new System.Messaging.BinaryMessageFormatter() as T;
            }
            if (typeof(IDataStreamRepository).IsAssignableFrom(typeOfT))
            {
                retVal = new FileSystemMailPackageQueue() as T;
            }
             return retVal;
        }

    }
}
