using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using System.ServiceModel;
using System.Configuration;
using System.Messaging;
using System.Transactions;

namespace Listener
{
    class Program
    {
        static void Main(string[] args)
        {
            ListenerService listenerService = null;
            ServiceHost pushMessageServiceHost = null;

            string messageBoxMSMQName = ConfigurationManager.AppSettings["ListenerMSMQName"];

            if (!MessageQueue.Exists(messageBoxMSMQName))
            {
                MessageQueue versionManagerQueue = MessageQueue.Create(messageBoxMSMQName, true);
                versionManagerQueue.SetPermissions(@"Everyone", MessageQueueAccessRights.FullControl);

                versionManagerQueue.SetPermissions(@"ANONYMOUS LOGON",
                    MessageQueueAccessRights.ReceiveMessage |
                    MessageQueueAccessRights.PeekMessage |
                    MessageQueueAccessRights.WriteMessage);
            }

            listenerService = new ListenerService();
            pushMessageServiceHost = new ServiceHost(listenerService);

            pushMessageServiceHost.Open();

            Console.WriteLine("Listener Started");
            Console.ReadKey();
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ListenerService : IListenerService
    {
        public void AddNode(Element element)
        {
            Console.WriteLine(element);
        }
    }
}
