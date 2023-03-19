using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace EasyIpc
{
    public interface IConnectionFactory
    {
        Task<IIpcClient> CreateClient(string serverName, string pipeName);
        Task<IIpcServer> CreateServer(string pipeName);
    }

    public class ConnectionFactory : IConnectionFactory
    {
        private static IConnectionFactory? _default;
        private readonly ICallbackStoreFactory _callbackFactory;
        private readonly ILoggerFactory _loggerFactory;

        public ConnectionFactory(ICallbackStoreFactory callbackFactory, ILoggerFactory loggerFactory)
        {
            _callbackFactory = callbackFactory;
            _loggerFactory = loggerFactory;
        }

        public static IConnectionFactory Default =>
            _default ??= 
            new ConnectionFactory(new CallbackStoreFactory(new LoggerFactory()), new LoggerFactory());

        public Task<IIpcClient> CreateClient(string serverName, string pipeName)
        {
            var client = new IpcClient(
                serverName, 
                pipeName, 
                _callbackFactory, 
                _loggerFactory.CreateLogger<IpcClient>());
            return Task.FromResult((IIpcClient)client);
        }

        public Task<IIpcServer> CreateServer(string pipeName)
        {
            var server = new IpcServer(
                pipeName, 
                _callbackFactory, 
                _loggerFactory.CreateLogger<IpcServer>());
            return Task.FromResult((IIpcServer)server);
        }
    }
}
