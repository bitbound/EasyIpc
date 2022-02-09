using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IServer<TMessageType> : IConnectionBase<TMessageType>
        where TMessageType : Enum
    {
        Task Initialize(string pipeName);

        Task<bool> WaitForConnection(CancellationToken cancellationToken);
    }


    public class Server<TMessageType> : ConnectionBase<TMessageType>, IServer<TMessageType>
        where TMessageType : Enum
    {
        public Server(ICallbackCollectionFactory callbackFactory, ILogger<Server<TMessageType>> logger)
            : base(callbackFactory, logger)
        { }


        public Task Initialize(string pipeName)
        {
            return InitializeInternal(pipeName);
        }


        public async Task<bool> WaitForConnection(CancellationToken cancellationToken)
        {
            try
            {
                await _initLock.WaitAsync();

                if (_pipeStream is null)
                {
                    throw new InvalidOperationException($"You must initialize the connection before calling this method.");
                }

                if (_pipeStream is NamedPipeServerStream serverStream)
                {
                    await serverStream.WaitForConnectionAsync(cancellationToken);
                    _logger.LogDebug("Connection established for server pipe {id}.", PipeName);
                }
                else
                {
                    throw new InvalidOperationException($"{nameof(_pipeStream)} is not of type NamedPipeServerStream.");
                }

                if (!_pipeStream.IsConnected)
                {
                    return false;
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task InitializeInternal(string pipeName)
        {
            try
            {
                await _initLock.WaitAsync();

                if (_pipeStream != null)
                {
                    throw new InvalidOperationException("This IPC server has already been initialized.");
                }

                _logger.LogDebug("IPC server connection initializing.  Pipe name: {pipeName}.", pipeName);


                PipeName = pipeName;

                _pipeStream = new NamedPipeServerStream(pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
            }
            finally
            {
                _initLock.Release();
            }
        }
    }

}
