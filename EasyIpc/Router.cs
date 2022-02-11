using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IRouter
    {

        /// <summary>
        /// Creates a message-based IIpcServer that handle messages via registered callbacks.
        /// Message callbacks can be registered using IIpcServer.On method.  The IpcServer
        /// will be assigned a randomly-generated ID for the pipe name.
        /// </summary>
        /// <returns>The newly-created IIpcServer.</returns>
        Task<IServer> CreateServer();

        /// <summary>
        /// Creates a message-based IIpcServer that handle messages via registered callbacks.
        /// Message callbacks can be registered using IIpcServer.On method.
        /// </summary>
        /// <param name="pipeName">The pipe name to use for the IpcServer.</param>
        /// <returns></returns>
        Task<IServer> CreateServer(string pipeName);

        bool TryGetServer(string pipeName, out IServer server);

        bool TryRemoveServer(string pipeName, out IServer server);

    }

    public class Router : IRouter
    {
        private static readonly ConcurrentDictionary<string, IServer> _pipeStreams = new();

        private readonly IConnectionFactory _serverFactory;
        private readonly ILogger<Router> _logger;

        public Router(IConnectionFactory serverFactory, ILogger<Router> logger)
        {
            _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        public async Task<IServer> CreateServer()
        {
            var pipeName = Guid.NewGuid().ToString();
            return await CreateServerInternal(pipeName);
        }

        public async Task<IServer> CreateServer(string pipeName)
        {
            return await CreateServerInternal(pipeName);
        }

        public bool TryGetServer(string pipeName, out IServer server)
        {
            return _pipeStreams.TryGetValue(pipeName, out server);
        }

        public bool TryRemoveServer(string pipeName, out IServer server)
        {
            return _pipeStreams.TryRemove(pipeName, out server);
        }

        private async Task<IServer> CreateServerInternal(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
            {
                throw new ArgumentNullException(nameof(pipeName));
            }

            _logger.LogDebug("Creating pipe message server {name}.", pipeName);

            var serverConnection = await _serverFactory.CreateServer(pipeName);

            serverConnection.ReadingEnded += ServerConnection_ReadingEnded;

            if (!_pipeStreams.TryAdd(pipeName, serverConnection))
            {
                throw new ArgumentException("The pipe name is already in use.");
            }

            return serverConnection;
        }

        private void ServerConnection_ReadingEnded(object sender, IConnectionBase args)
        {
            if (_pipeStreams.TryRemove(args.PipeName, out var server))
            {
                server.ReadingEnded -= ServerConnection_ReadingEnded;
            }
            else
            {
                _logger.LogWarning("Pipe name {pipeName} not found.", args.PipeName);
            }
        }
    }
}
