// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Benchmarks.Configuration;
using Benchmarks.Data;
using Benchmarks.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Benchmarks
{
    public class Startup
    {
        public Startup(IHostingEnvironment hostingEnv, Scenarios scenarios)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(hostingEnv.ContentRootPath)
                .AddCommandLine(Program.Args)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{hostingEnv.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            Scenarios = scenarios;
        }

        public IConfigurationRoot Configuration { get; set; }

        public Scenarios Scenarios { get; }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);

            // We re-register the Scenarios as an instance singleton here to avoid it being created again due to the
            // registration done in Program.Main
            services.AddSingleton(Scenarios);

            // Common DB services
            services.AddSingleton<IRandom, DefaultRandom>();
            services.AddSingleton<ApplicationDbSeeder>();

            services
                .AddEntityFrameworkSqlServer()
                .AddDbContextPool<ApplicationDbContext>((sp, ob) =>
                {
                    var appSettings = sp.GetRequiredService<IOptions<AppSettings>>().Value;

                    if (appSettings.Database == DatabaseServer.PostgreSql)
                    {
                        ob.UseNpgsql(appSettings.ConnectionString);
                    }
                    else
                    {
                        ob.UseSqlServer(appSettings.ConnectionString);
                    }
                });

            if (Scenarios.Any("Raw") || Scenarios.Any("Dapper"))
            {
                services.AddSingleton<DbProviderFactory>((provider) => {
                    var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;

                    if (settings.Database == DatabaseServer.PostgreSql)
                    {
                        return NpgsqlFactory.Instance;
                    }
                    else
                    {
                        return SqlClientFactory.Instance;
                    }
                });
            }

            if (Scenarios.Any("Ef"))
            {
                services.AddScoped<EfDb>();
            }

            if (Scenarios.Any("Raw"))
            {
                services.AddScoped<RawDb>();
            }

            if (Scenarios.Any("Dapper"))
            {
                services.AddScoped<DapperDb>();
            }

            if (Scenarios.Any("Fortunes"))
            {
                var settings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Katakana, UnicodeRanges.Hiragana);
                settings.AllowCharacter('\u2014');  // allow EM DASH through
                services.AddWebEncoders((options) =>
                {
                    options.TextEncoderSettings = settings;
                });
            }

            if (Scenarios.Any("Mvc"))
            {
                var mvcBuilder = services
                    .AddMvcCore()
                    //.AddApplicationPart(typeof(Startup).GetTypeInfo().Assembly)
                    .AddControllersAsServices();

                if (Scenarios.MvcJson || Scenarios.Any("MvcDbSingle") || Scenarios.Any("MvcDbMulti"))
                {
                    mvcBuilder.AddJsonFormatters();
                }

                if (Scenarios.MvcViews || Scenarios.Any("MvcDbFortunes"))
                {
                    mvcBuilder
                        .AddViews()
                        .AddRazorViewEngine();
                }
            }

            if (Scenarios.Any("MemoryCache"))
            {
                services.AddMemoryCache();
            }

            if (Scenarios.Any("ResponseCaching"))
            {
                services.AddResponseCaching();
            }

            return services.BuildServiceProvider(validateScopes: true);
        }

        public void Configure(IApplicationBuilder app, ApplicationDbSeeder dbSeeder, IOptions<AppSettings> appSettings,
            ILoggerFactory loggerFactory)
        {
            if (Scenarios.StaticFiles)
            {
                app.UseStaticFiles();
            }

            if (Scenarios.StaticFiles || Scenarios.Plaintext)
            {
                app.UsePlainText();
            }

            if (Scenarios.Json)
            {
                app.UseJson();
            }

            if (Scenarios.CopyToAsync)
            {
                app.UseCopyToAsync();
            }

            // Single query endpoints
            if (Scenarios.DbSingleQueryRaw)
            {
                app.UseSingleQueryRaw();
            }

            if (Scenarios.DbSingleQueryDapper)
            {
                app.UseSingleQueryDapper();
            }

            if (Scenarios.DbSingleQueryEf)
            {
                app.UseSingleQueryEf();
            }

            // Multiple query endpoints
            if (Scenarios.DbMultiQueryRaw)
            {
                app.UseMultipleQueriesRaw();
            }

            if (Scenarios.DbMultiQueryDapper)
            {
                app.UseMultipleQueriesDapper();
            }

            if (Scenarios.DbMultiQueryEf)
            {
                app.UseMultipleQueriesEf();
            }

            // Multiple update endpoints
            if (Scenarios.DbMultiUpdateRaw)
            {
                app.UseMultipleUpdatesRaw();
            }

            if (Scenarios.DbMultiUpdateDapper)
            {
                app.UseMultipleUpdatesDapper();
            }

            if (Scenarios.DbMultiUpdateEf)
            {
                app.UseMultipleUpdatesEf();
            }

            // Fortunes endpoints
            if (Scenarios.DbFortunesRaw)
            {
                app.UseFortunesRaw();
            }

            if (Scenarios.DbFortunesDapper)
            {
                app.UseFortunesDapper();
            }

            if (Scenarios.DbFortunesEf)
            {
                app.UseFortunesEf();
            }

            if (Scenarios.Any("Db"))
            {
                if (!dbSeeder.Seed())
                {
                    Environment.Exit(1);
                }
            }

            if (Scenarios.Any("Mvc"))
            {
                app.UseMvc();
            }

            if (Scenarios.MemoryCachePlaintext)
            {
                app.UseMemoryCachePlaintext();
            }

            if (Scenarios.MemoryCachePlaintextSetRemove)
            {
                app.UseMemoryCachePlaintextSetRemove();
            }

            if (Scenarios.ResponseCachingPlaintextCached)
            {
                app.UseResponseCachingPlaintextCached();
            }

            if (Scenarios.ResponseCachingPlaintextResponseNoCache)
            {
                app.UseResponseCachingPlaintextResponseNoCache();
            }

            if (Scenarios.ResponseCachingPlaintextRequestNoCache)
            {
                app.UseResponseCachingPlaintextRequestNoCache();
            }

            if (Scenarios.ResponseCachingPlaintextVaryByCached)
            {
                app.UseResponseCachingPlaintextVaryByCached();
            }

            app.RunDebugInfoPage();
        }
    }
}
