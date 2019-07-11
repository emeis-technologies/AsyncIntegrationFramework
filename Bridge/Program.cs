using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using Common;
using System.ServiceModel.Channels;
using System.Transactions;
using System.Configuration;
using System.Messaging;

namespace Bridge
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceHost registerClientServiceHost = null;
            string registerClientMSMQName = ConfigurationManager.AppSettings["MediatorServiceMSMQName"];

            if (!MessageQueue.Exists(registerClientMSMQName))
            {
                MessageQueue versionManagerQueue = MessageQueue.Create(registerClientMSMQName, true);
                versionManagerQueue.SetPermissions(@"Everyone", MessageQueueAccessRights.FullControl);

                versionManagerQueue.SetPermissions(@"ANONYMOUS LOGON",
                    MessageQueueAccessRights.ReceiveMessage |
                    MessageQueueAccessRights.PeekMessage |
                    MessageQueueAccessRights.WriteMessage);
            }

            registerClientServiceHost = new ServiceHost(typeof(MediatorService));
            registerClientServiceHost.Open();

            Console.WriteLine("Bridge Started");            

            Console.WriteLine("Hit Enter to close server");
            Console.ReadKey();

            registerClientServiceHost.Close();
        }
    }

    public class QueueItem
    {
        public enum State
        {
            Pending,
            Sending,
            Idle
        }

        public QueueItem(string groupName
            , string clientName
            , string pushMessageBoxAddress)
        {
            Init(groupName
                , clientName
                , pushMessageBoxAddress
                , State.Idle);
        }

        public QueueItem(QueueItem QueueItem)
        {
            Init(QueueItem.GroupName
                , QueueItem.ClientName
                , QueueItem.PushMessageBoxAddress
                , QueueItem.QueueState);
        }

        protected void Init(string groupName
            , string clientName
            , string pushMessageBoxAddress
            , State queueState)
        {
            this.GroupName = groupName;
            this.ClientName = clientName;
            this.PushMessageBoxAddress = pushMessageBoxAddress;
            this.QueueState = queueState;
        }

        public string GroupName { get; set; }
        public string ClientName { get; set; }
        public string PushMessageBoxAddress { get; set; }
        public State QueueState { get; set; }
    }

    public class ClientQueue
    {
        static protected readonly object lockObject = new object();

        List<QueueItem> queue = new List<QueueItem>();

        public bool IsEmpty()
        {
            return Queue.Count == 0;
        }

        public void Clear()
        {
            lock (lockObject)
            {
                Queue.Clear();
            }
        }

        public void Remove(QueueItem queueItem)
        {
            lock (lockObject)
            {
                QueueItem queueItemObj
                    = GetItem(queueItem.GroupName
                    , queueItem.ClientName);

                if (queueItemObj != null)
                {
                    Queue.Remove(queueItemObj);
                }
                else
                {
                    return;
                }
            }
        }

        public void Update(string groupName, string clientName, string pushMessageBoxAddress)
        {
            lock (lockObject)
            {
                QueueItem queueItemObj
                    = GetItem(groupName, clientName);

                if (queueItemObj != null)
                {
                    queueItemObj.PushMessageBoxAddress
                        = pushMessageBoxAddress;
                }
                else
                {
                    throw new System.Exception("Client item does not exists");
                }
            }
        }

        public void Add(QueueItem queueItem)
        {
            lock (lockObject)
            {
                QueueItem queueItemObj
                    = GetItem(queueItem.GroupName
                    , queueItem.ClientName);

                if (queueItemObj != null)
                {
                    if (queueItemObj.PushMessageBoxAddress
                        == queueItem.PushMessageBoxAddress)
                        return;
                    else
                        throw new System.Exception(
                            "Client item already exist in queue with different address");
                }
                else
                {
                    Queue.Add(queueItem);
                }
            }
        }


        public QueueItem GetItem(string groupName
            , string clientName)
        {
            foreach (QueueItem queueItemObj in Queue)
            {
                if ((queueItemObj.GroupName == groupName) &&
                    (queueItemObj.ClientName == clientName))
                {
                    return queueItemObj;
                }
            }

            return null;
        }

        public bool IsInQueue(string groupName
            , string clientName)
        {
            return (GetItem(groupName
                , clientName) != null);
        }

        public IEnumerator<QueueItem> GetEnumerator()
        {
            return Queue.GetEnumerator();
        }

        protected List<QueueItem> Queue
        {
            get { return queue; }
        }
    }

    public class ClientQueueManagerBO
    {
        static ClientQueue clientQueue = null;

        static readonly object lockObject = new object();

        public ClientQueueManagerBO()
        {
            if (clientQueue == null)
            {
                clientQueue = new ClientQueue();
            }
        }

        public void AddToQueue(string groupName
            , string clientName
            , string pushMessageBoxAddress)
        {
            lock (lockObject)
            {
                if (clientQueue.IsInQueue(groupName, clientName))
                {
                    clientQueue.Update(groupName
                        , clientName, pushMessageBoxAddress);
                }
                else
                {
                    clientQueue.Add(new QueueItem(groupName
                        , clientName, pushMessageBoxAddress));
                }
            }
        }

        public IEnumerator<QueueItem> GetEnumerator()
        {
            return clientQueue.GetEnumerator();
        }
    }

    public class ListenerServiceClient
        : ClientBase<IListenerService>, IListenerService
    {
        public ListenerServiceClient(Binding binding
            , EndpointAddress remoteAddress) :
            base(binding, remoteAddress)
        {
        }

        public void AddNode(Element element)
        {
            base.Channel.AddNode(element);
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class MediatorService : IMediatorService
    {
        [OperationBehavior(TransactionScopeRequired = true, TransactionAutoComplete = true)]
        public void AddNode(Element element)
        {
            WcfServerPushServerBO.Instance.AddNode(element);

            Console.WriteLine("Recieved Order: {0}", element);
        }
    }

    public class ServerPushMessageBO
    {
        public void SendPushMessage(Element element, string pushMessageBoxAddress)
        {
            NetMsmqBinding msmqCallbackBinding = new NetMsmqBinding();
            msmqCallbackBinding.Security.Mode = NetMsmqSecurityMode.None;

            ListenerServiceClient client
                = new ListenerServiceClient(msmqCallbackBinding
                    , new EndpointAddress(pushMessageBoxAddress));

            using (TransactionScope scope
                = new TransactionScope(TransactionScopeOption.Required))
            {
                client.AddNode(element);

                scope.Complete();
            }

            client.Close();
        }
    }

    public class WcfServerPushServerBO
    {
        private static WcfServerPushServerBO instance = null;
        static ClientQueueManagerBO clientQueueManagerBO = null;
        static ServerPushMessageBO serverPushMessageBO = null;

        private WcfServerPushServerBO()
        {
            if (clientQueueManagerBO == null)
                clientQueueManagerBO = new ClientQueueManagerBO();

            // TODO: Read from config.
            RegisterClient(new ClientInfo
            {
                GroupName = ConfigurationManager.AppSettings["GroupName"],
                ClientName = "localhost",
                PushMessageBoxAddress = "net.msmq://localhost/private/WcfServerPush/ListenerService"
            });

            if (serverPushMessageBO == null)
                serverPushMessageBO = new ServerPushMessageBO();
        }

        public static WcfServerPushServerBO Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WcfServerPushServerBO();
                }
                return instance;
            }
        }

        private void RegisterClient(ClientInfo clientInfo)
        {
            clientQueueManagerBO.AddToQueue(clientInfo.GroupName
                , clientInfo.ClientName, clientInfo.PushMessageBoxAddress);
        }

        public void AddNode(Element element)
        {
            IEnumerator<QueueItem> clientEnumerator = clientQueueManagerBO.GetEnumerator();

            clientEnumerator.Reset();

            while (clientEnumerator.MoveNext())
            {
                serverPushMessageBO.SendPushMessage(element
                    , clientEnumerator.Current.PushMessageBoxAddress);
            }
        }
    }

    public class ClientInfo
    {
        public string GroupName { get; set; }
        public string ClientName { get; set; }
        public string PushMessageBoxAddress { get; set; }
    }
}
