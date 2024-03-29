﻿using Microsoft.Extensions.DependencyInjection;
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
            services.AddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
            services.AddSingleton<ICallbackStoreFactory, CallbackStoreFactory>();
            services.AddSingleton<IIpcRouter, IpcRouter>();
            services.AddTransient<ICallbackStore, CallbackStore>();
            return services;
        }
    }
}
