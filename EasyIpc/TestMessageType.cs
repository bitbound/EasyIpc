using System.Runtime.Serialization;

namespace EasyIpc
{
    [DataContract]
    public enum TestMessageType
    {
        [EnumMember(Value = "Ping")]
        Ping,
        [EnumMember(Value = "Pong")]
        Pong
    }
}
