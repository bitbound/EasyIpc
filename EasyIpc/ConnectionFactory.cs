using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IConnectionFactory<TMessageType>
        where TMessageType : Enum
    {
        Task<IClient<TMessageType>> CreateClient(string serverName, string pipeName);
        Task<IServer<TMessageType>> CreateServer(string pipeName);
    }

    public class ConnectionFactory<TMessageType> : IConnectionFactory<TMessageType>
        where TMessageType : Enum
    {
        private readonly ICallbackCollectionFactory _callbackFactory;
        private readonly ILoggerFactory _loggerFactory;

        public ConnectionFactory(ICallbackCollectionFactory callbackFactory, ILoggerFactory loggerFactory)
        {
            _callbackFactory = callbackFactory;
            _loggerFactory = loggerFactory;
        }

        public async Task<IServer<TMessageType>> CreateServer(string pipeName)
        {
            var server = new Server<TMessageType>(_callbackFactory, _loggerFactory.CreateLogger<Server<TMessageType>>());
            await server.Initialize(pipeName);
            return server;
        }
        public async Task<IClient<TMessageType>> CreateClient(string serverName, string pipeName)
        {
            var client = new Client<TMessageType>(_callbackFactory, _loggerFactory.CreateLogger<Client<TMessageType>>());
            await client.Initialize(serverName, pipeName);
            return client;
        }
    }
}
