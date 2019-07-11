using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using System.ServiceModel;
using System.Configuration;
using System.Messaging;
using System.Transactions;

namespace Source
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ISWA Started. Press enter to send a message...");
            Console.ReadKey();

            MediatorServiceClient mediatorServiceClient
                = new MediatorServiceClient("MediatorServiceEndPoint");

            using (TransactionScope scope
                = new TransactionScope(TransactionScopeOption.Required))
            {
                mediatorServiceClient.AddNode(new Element
                {
                    Id = 1,
                    Title = "Test",
                    Abstract = "Desc"
                });

                scope.Complete();
            }

            mediatorServiceClient.Close();

            Console.WriteLine("Message sent. Press enter to close the program...");
            Console.ReadKey();
        }
    }

    public class MediatorServiceClient
        : ClientBase<IMediatorService>, IMediatorService
    {
        public MediatorServiceClient(string endpointConfigurationName)
            : base(endpointConfigurationName)
        {
        }

        public void AddNode(Element element)
        {
            base.Channel.AddNode(element);
        }
    }
}
