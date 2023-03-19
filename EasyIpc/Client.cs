using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IClient : IConnectionBase
    {
        Task Initialize(string serverName, string pipeName);
        Task<bool> Connect(int timeout);
    }

    internal class Client : ConnectionBase, IClient
    {
        public Client(ICallbackStoreFactory callbackFactory, ILogger<Client> logger)
            : base(callbackFactory, logger)
        { }

        public async Task<bool> Connect(int timeout)
        {
            try
            {
                await _initLock.WaitAsync();

                if (_pipeStream is null)
                {
                    throw new InvalidOperationException("IPC client must be initialized before calling this method.");
                }

                if (_pipeStream is NamedPipeClientStream clientPipe)
                {
                    await clientPipe.ConnectAsync(timeout);
                    _logger.LogDebug("Connection established for client pipe {id}.", PipeName);
                }
                else
                {
                    throw new InvalidOperationException("PipeStream is not of type NamedPipeClientStream.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to IPC server.");
            }
            finally
            {
                _initLock.Release();
            }

            return _pipeStream?.IsConnected == true;
        }

        public Task Initialize(string serverName, string pipeName)
        {
            return InitializeInternal(serverName, pipeName);
        }

        private async Task InitializeInternal(string serverName, string pipeName)
        {
            try
            {
                await _initLock.WaitAsync();

                if (_pipeStream != null)
                {
                    throw new InvalidOperationException("IPC client has already been initialized.");
                }

                _logger.LogDebug("IPC client connection initializing.  Pipe name: {pipeName}.", pipeName);


                PipeName = pipeName;

                _pipeStream = new NamedPipeClientStream(serverName,
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}
