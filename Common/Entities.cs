using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace Common
{
    [ServiceContract]
    public interface IListenerService
    {
        [OperationContract(IsOneWay = true)]
        void AddNode(Element element);
    }

    [ServiceContract]
    public interface IMediatorService
    {
        [OperationContract(IsOneWay = true)]
        void AddNode(Element element);
    }

    [DataContract]
    public class Element
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Abstract { get; set; }
    }
}
