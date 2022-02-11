using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EasyIpc
{
    public static class Bootstrapper
    {
        private readonly static ILoggerFactory _loggerFactory = new LoggerFactory();

        private static ConnectionFactory _defaultFactory;
        private static Router _defaultRouter;

        public static void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IConnectionFactory, ConnectionFactory>();
            serviceCollection.AddSingleton<ICallbackStoreFactory, CallbackStoreFactory>();
            serviceCollection.AddSingleton<IRouter, Router>();
            serviceCollection.AddScoped<IClient, Client>();
            serviceCollection.AddScoped<IServer, Server>();
            serviceCollection.AddScoped<ICallbackStore, CallbackStore>();
        }

        public static IRouter DefaultRouter =>
            _defaultRouter ??= new Router(DefaultFactory, _loggerFactory.CreateLogger<Router>());

        public static IConnectionFactory DefaultFactory =>
            _defaultFactory ??= new ConnectionFactory(new CallbackStoreFactory(), _loggerFactory);
    }
}
