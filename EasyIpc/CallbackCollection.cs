using MessagePack;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface ICallbackCollection<TMessageType>
        where TMessageType : Enum
    {
        void Add(Type contentType, Action<object> callback);
        void Add(Func<object, object> handler, Type contentType, Type returnType);
        void Add(Func<object> handler, Type returnType);
        Task InvokeActions(MessageWrapper<TMessageType> wrapper);
        Task InvokeFuncs(MessageWrapper<TMessageType> wrapper, Func<MessageWrapper<TMessageType>, Task> responseFunc);
    }

    public class CallbackCollection<TMessageType> : ICallbackCollection<TMessageType>
        where TMessageType : Enum
    {
        private readonly List<IpcAction> _actions = new();
        private readonly SemaphoreSlim _actionsLock = new(1, 1);
        private readonly List<IpcFunc> _funcs = new();
        private readonly SemaphoreSlim _funcsLock = new(1, 1);

        public void Add(Type contentType, Action<object> callback)
        {
            try
            {
                _actionsLock.Wait();
                _actions.Add(new IpcAction(contentType, callback));
            }
            finally
            {
                _actionsLock.Release();
            }
        }

        public void Add(Func<object, object> handler, Type contentType, Type returnType)
        {
            try
            {
                _funcsLock.Wait();
                _funcs.Add(new IpcFunc(handler, contentType, returnType));
            }
            finally
            {
                _funcsLock.Release();
            }
        }

        public void Add(Func<object> handler, Type returnType)
        {
            try
            {
                _funcsLock.Wait();
                _funcs.Add(new IpcFunc(handler, returnType));
            }
            finally
            {
                _funcsLock.Release();
            }
        }

        public async Task InvokeActions(MessageWrapper<TMessageType> wrapper)
        {
            if (wrapper is null)
            {
                return;
            }

            try
            {
                await _actionsLock.WaitAsync();

                foreach (var callback in _actions)
                {
                    if (callback.ContentType == wrapper.ContentType)
                    {
                        var content = MessagePackSerializer.Deserialize(wrapper.ContentType, wrapper.Content);
                        callback.Action.Invoke(content);
                    }
                }
            }
            finally
            {
                _actionsLock.Release();
            }
        }

        public async Task InvokeFuncs(
            MessageWrapper<TMessageType> wrapper,
            Func<MessageWrapper<TMessageType>, Task> responseFunc)
        {
            if (wrapper is null)
            {
                return;
            }

            try
            {
                await _funcsLock.WaitAsync();

                foreach (var func in _funcs)
                {
                    object result = default;
                    Type returnType = func.ReturnType;

                    if (func.ContentType is null)
                    {
                        result = func.Handler.Invoke();
                    }
                    else if (func.ContentType == wrapper.ContentType)
                    {
                        var content = MessagePackSerializer.Deserialize(wrapper.ContentType, wrapper.Content);
                        result = func.Handler2.Invoke(content);
                    }

                    if (result is Task resultTask)
                    {
                        result = ((dynamic)resultTask).GetAwaiter().GetResult();
                        returnType = result.GetType();
                    }

                    var responseWrapper = new MessageWrapper<TMessageType>(
                        wrapper.MessageType,
                        result,
                        returnType,
                        wrapper.Id);

                    await responseFunc.Invoke(responseWrapper);
                }
            }
            finally
            {
                _funcsLock.Release();
            }
        }

        private class IpcAction
        {
            public IpcAction(Type contentType, Action<object> action)
            {
                ContentType = contentType;
                Action = action;
            }

            public Type ContentType { get; }
            public Action<object> Action { get; }
        }

        private class IpcFunc
        {
            public IpcFunc(Func<object, object> handler, Type contentType, Type returnType)
            {
                ContentType = contentType;
                Handler2 = handler;
                ReturnType = returnType;
            }

            public IpcFunc(Func<object> handler, Type returnType)
            {
                Handler = handler;
                ReturnType = returnType;
            }

            public Type ContentType { get; }
            public Type ReturnType { get; }
            public Func<object> Handler { get; }
            public Func<object, object> Handler2 { get; }
        }
    }
}
