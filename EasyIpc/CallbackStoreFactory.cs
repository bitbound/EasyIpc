using System;
using System.Collections.Generic;
using System.Text;

namespace EasyIpc
{
    public interface ICallbackStoreFactory
    {
        ICallbackStore Create();
    }

    public class CallbackStoreFactory : ICallbackStoreFactory
    {
        public ICallbackStore Create() => new CallbackStore();
    }
}
