using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace Service
{
    public static class Tests
    {
        public static void Run()
        {
            SmtpClient client = new SmtpClient(IPAddress.Loopback.ToString(), 465);
            client.EnableSsl = true;

            MailMessage message = new MailMessage();
            message.From = new MailAddress("jcooper@email.com", "Dr Cooper");
            message.To.Add(new MailAddress("1@email.com", "Number 1"));
            message.To.Add(new MailAddress("2@email.com", "Number 2"));
            message.To.Add(new MailAddress("3@email.com", "Number 3"));
            message.To.Add(new MailAddress("4@email.com", "Number 4"));
            message.CC.Add(new MailAddress("5@email.com", "Number 5"));
            message.CC.Add(new MailAddress("6@email.com", "Number 6"));
            message.CC.Add(new MailAddress("7@email.com", "Number 7"));
            message.CC.Add(new MailAddress("7@email.com", "Number 8"));
            message.Subject = "This is my subject";
            message.Body = ".";


            Attachment attachment = new Attachment(File.Open(@"C:\Users\Joe\Documents\WebCam Media\Capture\ArcSoft_Video14.wmv", FileMode.Open), "john", "video/x-ms-wmv");
            message.Attachments.Add(attachment);

            System.Net.ServicePointManager.ServerCertificateValidationCallback = Callback;
            client.Send(message);
            attachment.Dispose();
        }

        private static bool Callback(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }
    }

}
