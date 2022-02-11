using MessagePack;
using System;
using System.Runtime.Serialization;

namespace EasyIpc
{
    [DataContract]
    public class MessageWrapper
    {
        [SerializationConstructor]
        public MessageWrapper()
        {
            Id = Guid.NewGuid();
        }

        public MessageWrapper(Type contentType, object content, MessageType messageType)
            : this()
        {
            Content = MessagePackSerializer.Serialize(contentType, content);
            ContentType = contentType;
            MessageType = messageType;
        }

        public MessageWrapper(Type contentType, object content, Guid responseTo)
            : this(contentType, content, MessageType.Response)
        {
            ResponseTo = responseTo;
        }

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public byte[] Content { get; set; }

        [DataMember]
        public Type ContentType { get; set; }

        [DataMember]
        public MessageType MessageType { get; set;}

        [DataMember]
        public Guid ResponseTo { get; set; }
    }
}
