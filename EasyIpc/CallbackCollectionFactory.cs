using System;

namespace EasyIpc
{
    public interface ICallbackCollectionFactory
    {
        ICallbackCollection<TMessageType> Create<TMessageType>()
            where TMessageType : Enum;
    }

    public class CallbackCollectionFactory : ICallbackCollectionFactory
    {
        public ICallbackCollection<TMessageType> Create<TMessageType>()
            where TMessageType : Enum
        {
            return new CallbackCollection<TMessageType>();
        }
    }
}
