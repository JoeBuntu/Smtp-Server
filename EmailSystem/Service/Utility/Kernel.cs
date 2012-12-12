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
            if(typeof(ILogger).IsAssignableFrom(typeOfT))
            {
                retVal = new DefaultLogger() as T;
            }
            else if (typeof(IMailPackageQueue).IsAssignableFrom(typeOfT))
            {
                retVal = new FileSystemMailPackageQueue() as T;
            }
             return retVal;
        }

    }
}
