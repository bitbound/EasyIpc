using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EasyIpc
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddEasyIpc(this IServiceCollection services)
        {
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<ICallbackStoreFactory, CallbackStoreFactory>();
            services.AddSingleton<IRouter, Router>();
            services.AddScoped<IClient, Client>();
            services.AddScoped<IServer, Server>();
            services.AddScoped<ICallbackStore, CallbackStore>();
            return services;
        }
    }
}
