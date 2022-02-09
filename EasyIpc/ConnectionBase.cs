using MessagePack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IConnectionBase<TMessageType> : IDisposable
    {
        event EventHandler<IConnectionBase<TMessageType>> ReadingEnded;

        bool IsConnected { get; }
        string PipeName { get; }


        void BeginRead(CancellationToken cancellationToken);
        Stream GetStream();
        Task<Result<TReturnType>> Invoke<TReturnType, TContentType>(TMessageType messageType, TContentType content, int timeoutMs = 5000);

        Task<Result<TReturnType>> Invoke<TReturnType>(TMessageType messageType, int timeoutMs = 5000);

        IConnectionBase<TMessageType> Off<TContentType>(TMessageType messageType);
        IConnectionBase<TMessageType> On<TContentType>(TMessageType messageType, Action<TContentType> callback);

        IConnectionBase<TMessageType> On<TContentType, ReturnType>(TMessageType messageType, Func<TContentType, ReturnType> handler);
        IConnectionBase<TMessageType> On<ReturnType>(TMessageType messageType, Func<ReturnType> handler);
        Task Send<TContentType>(TMessageType messageType, TContentType content, int timeoutMs = 5000);
    }


    public abstract class ConnectionBase<TMessageType> : IConnectionBase<TMessageType>
        where TMessageType : Enum
    {
        protected readonly SemaphoreSlim _initLock = new(1, 1);
        protected readonly ILogger _logger;
        protected PipeStream _pipeStream;

        private readonly ConcurrentDictionary<TMessageType, ICallbackCollection<TMessageType>> _callbacks = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<MessageWrapper<TMessageType>>> _invokesPendingCompletion = new();
        private readonly ICallbackCollectionFactory _ipcCallbackFactory;
        private CancellationToken _readStreamCancelToken;
        private Task _readTask;


        public ConnectionBase(ICallbackCollectionFactory ipcCallbackFactory, ILogger logger)
        {
            _ipcCallbackFactory = ipcCallbackFactory ?? throw new ArgumentNullException(nameof(ipcCallbackFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<IConnectionBase<TMessageType>> ReadingEnded;

        public string PipeName { get; protected set; }

        public bool IsConnected => _pipeStream?.IsConnected ?? false;

        public void BeginRead(CancellationToken cancellationToken)
        {
            if (_readTask?.IsCompleted == false)
            {
                throw new InvalidOperationException("Stream is already being read.");
            }

            _readStreamCancelToken = cancellationToken;
            _readTask = Task.Run(ReadFromStream, cancellationToken);
        }

        public void Dispose()
        {
            _pipeStream?.Dispose();
        }

        public Stream GetStream()
        {
            return _pipeStream;
        }

        public async Task<Result<TReturnType>> Invoke<TReturnType>(MessageWrapper<TMessageType> wrapper, int timeoutMs = 5000)
        {
            try
            {
                var tcs = new TaskCompletionSource<MessageWrapper<TMessageType>>();
                if (!_invokesPendingCompletion.TryAdd(wrapper.Id, tcs))
                {
                    _logger.LogWarning("Already waiting for invoke completion of message ID {id}.", wrapper.Id);
                    return Result.Fail<TReturnType>($"Already waiting for invoke completion of message ID {wrapper.Id}.");
                }

                await SendInternal(wrapper, timeoutMs);

                if (!await Task.Run(() => tcs.Task.Wait(timeoutMs)))
                {
                    _logger.LogWarning("Timed out while invoking message type {messageType}.", wrapper.MessageType);

                    return Result.Fail<TReturnType>("Timed out while invoking message.");
                }

                var result = tcs.Task.Result;

                return Result.Ok((TReturnType)MessagePackSerializer.Deserialize(result.ContentType, result.Content));
            }
            finally
            {
                _invokesPendingCompletion.TryRemove(wrapper.Id, out _);
            }
        }

        public Task<Result<TReturnType>> Invoke<TReturnType, TContentType>(
            TMessageType messageType,
            TContentType content,
            int timeoutMs = 5000)
        {
            var wrapper = new MessageWrapper<TMessageType>(messageType, content, typeof(TContentType));

            return Invoke<TReturnType>(wrapper, timeoutMs);
        }

        public Task<Result<TReturnType>> Invoke<TReturnType>(TMessageType messageType, int timeoutMs = 5000)
        {
            var wrapper = new MessageWrapper<TMessageType>(messageType);
            return Invoke<TReturnType>(wrapper, timeoutMs);
        }

        public IConnectionBase<TMessageType> Off<TContentType>(TMessageType messageType)
        {
            if (!_callbacks.TryRemove(messageType, out _))
            {
                _logger.LogWarning("The message type {messageType} wasn't found in the callback colection.", messageType);
            }

            return this;
        }

        public IConnectionBase<TMessageType> On<TContentType>(TMessageType messageType, Action<TContentType> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var objectCallback = new Action<object>(x => callback((TContentType)x));

            _callbacks.AddOrUpdate(messageType,
                _ =>
                {
                    var newCollection = _ipcCallbackFactory.Create<TMessageType>();
                    newCollection.Add(typeof(TContentType), objectCallback);
                    return newCollection;
                },
                (k, v) =>
                {
                    v.Add(typeof(TContentType), objectCallback);
                    return v;
                });

            return this;
        }


        public IConnectionBase<TMessageType> On<TContentType, ReturnType>(TMessageType messageType, Func<TContentType, ReturnType> handler)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var objectHandler = new Func<object, object>(x => handler((TContentType)x));

            _callbacks.AddOrUpdate(messageType,
                _ =>
                {
                    var newCollection = _ipcCallbackFactory.Create<TMessageType>();
                    newCollection.Add(objectHandler, typeof(TContentType), typeof(ReturnType));
                    return newCollection;
                },
                (k, v) =>
                {
                    v.Add(objectHandler, typeof(TContentType), typeof(ReturnType));
                    return v;
                });

            return this;
        }

        public IConnectionBase<TMessageType> On<ReturnType>(TMessageType messageType, Func<ReturnType> handler)
        {

            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var objectHandler = new Func<object>(() => handler());

            _callbacks.AddOrUpdate(messageType,
                _ =>
                {
                    var newCollection = _ipcCallbackFactory.Create<TMessageType>();
                    newCollection.Add(objectHandler, typeof(ReturnType));
                    return newCollection;
                },
                (k, v) =>
                {
                    v.Add(objectHandler, typeof(ReturnType));
                    return v;
                });

            return this;
        }

        public Task Send<TContentType>(TMessageType messageType, TContentType content, int timeoutMs = 5000)
        {
            return SendInternal(messageType, content, typeof(TContentType), timeoutMs);
        }

        private void OnReadingEnded()
        {
            ReadingEnded?.Invoke(this, this);
        }

        private async Task ReadFromStream()
        {
            while (_pipeStream.IsConnected)
            {
                try
                {
                    if (_readStreamCancelToken.IsCancellationRequested)
                    {
                        _logger.LogDebug("IPC connection read cancellation requested.  Pipe Name: {pipeName}", PipeName);
                        break;
                    }

                    var messageSizeBuffer = new byte[4];
                    await _pipeStream.ReadAsync(messageSizeBuffer, 0, 4, _readStreamCancelToken);
                    var messageSize = BitConverter.ToInt32(messageSizeBuffer, 0);

                    var buffer = new byte[messageSize];

                    var bytesRead = 0;

                    while (bytesRead < messageSize)
                    {
                        bytesRead += await _pipeStream.ReadAsync(buffer, 0, messageSize, _readStreamCancelToken);
                    }

                    var wrapper = MessagePackSerializer.Deserialize<MessageWrapper<TMessageType>>(buffer);

                    if (wrapper.ResponseTo != Guid.Empty &&
                        _invokesPendingCompletion.TryGetValue(wrapper.ResponseTo, out var tcs))
                    {
                        tcs.SetResult(wrapper);
                        continue;
                    }

                    if (_callbacks.TryGetValue(wrapper.MessageType, out var callbackCollection))
                    {
                        await callbackCollection.InvokeActions(wrapper);

                        await callbackCollection.InvokeFuncs(wrapper, async result =>
                        {
                            await SendInternal(result);
                        });
                    }
                }
                catch (ThreadAbortException ex)
                {
                    _logger.LogInformation(ex, "IPC connection aborted.  Pipe Name: {pipeName}", PipeName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to process pipe message.", ex);
                }
            }

            _logger.LogDebug("IPC stream reading ended. Pipe Name: {pipeName}", PipeName);
            OnReadingEnded();
        }

        private Task SendInternal(TMessageType messageType, object content, Type contentType, int timeoutMs = 5000)
        {
            var wrapper = new MessageWrapper<TMessageType>(messageType, content, contentType);
            return SendInternal(wrapper, timeoutMs);
        }
        private async Task SendInternal(MessageWrapper<TMessageType> wrapper, int timeoutMs = 5000)
        {
            try
            {
                if (timeoutMs < 1)
                {
                    throw new ArgumentException("Timeout must be greater than 0.");
                }

                using var cts = new CancellationTokenSource(timeoutMs);
                var wrapperBytes = MessagePackSerializer.Serialize(wrapper);

                var messageSizeBuffer = BitConverter.GetBytes(wrapperBytes.Length);
                await _pipeStream.WriteAsync(messageSizeBuffer, 0, messageSizeBuffer.Length, cts.Token);

                await _pipeStream.WriteAsync(wrapperBytes, 0, wrapperBytes.Length, cts.Token);
                await _pipeStream.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending message.  Message Type: {messageType}.  Content Type: {contentType}",
                    typeof(TMessageType),
                    wrapper.ContentType);
            }
        }
    }
}
