using MessagePack;
using System;
using System.Runtime.Serialization;

namespace EasyIpc
{
    [DataContract]
    public class MessageWrapper<TMessageType>
        where TMessageType : Enum
    {
        public MessageWrapper()
        {
            Id = Guid.NewGuid();
        }

        public MessageWrapper(TMessageType messageType)
           : this()
        {
            MessageType = messageType;
        }

        public MessageWrapper(TMessageType messageType, object content, Type contentType)
            : this(messageType)
        {
            Content = MessagePackSerializer.Serialize(content);
            ContentType = contentType;
        }

        public MessageWrapper(TMessageType messageType, object content, Type contentType, Guid responseTo)
            : this(messageType, content, contentType)
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
        public TMessageType MessageType { get; set; }

        [DataMember]
        public Guid ResponseTo { get; set; }
    }
}
