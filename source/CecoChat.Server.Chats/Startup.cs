using System.Reflection;
using Autofac;
using Calzolari.Grpc.AspNetCore.Validation;
using CecoChat.AspNet.Health;
using CecoChat.AspNet.Prometheus;
using CecoChat.Autofac;
using CecoChat.Cassandra;
using CecoChat.Cassandra.Health;
using CecoChat.Contracts.Backplane;
using CecoChat.Data.Chats;
using CecoChat.Data.Chats.Telemetry;
using CecoChat.Data.Config;
using CecoChat.Kafka;
using CecoChat.Kafka.Health;
using CecoChat.Kafka.Telemetry;
using CecoChat.Otel;
using CecoChat.Redis.Health;
using CecoChat.Server.Backplane;
using CecoChat.Server.Chats.Backplane;
using CecoChat.Server.Chats.Endpoints;
using CecoChat.Server.Chats.HostedServices;
using CecoChat.Server.Identity;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CecoChat.Server.Chats;

public class Startup : StartupBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly BackplaneOptions _backplaneOptions;
    private readonly CassandraOptions _chatsDbOptions;

    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        : base(configuration)
    {
        _environment = environment;

        _backplaneOptions = new();
        Configuration.GetSection("Backplane").Bind(_backplaneOptions);

        _chatsDbOptions = new();
        Configuration.GetSection("ChatsDb").Bind(_chatsDbOptions);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        AddTelemetryServices(services);
        AddHealthServices(services);

        // security
        services.AddJwtAuthentication(JwtOptions);
        services.AddUserPolicyAuthorization();

        // clients
        services.AddGrpc(grpc =>
        {
            grpc.EnableDetailedErrors = _environment.IsDevelopment();
            grpc.EnableMessageValidation();
        });
        services.AddGrpcValidation();

        // common
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddOptions();
    }

    private void AddTelemetryServices(IServiceCollection services)
    {
        ResourceBuilder serviceResourceBuilder = ResourceBuilder
            .CreateEmpty()
            .AddService(serviceName: "Chats", serviceNamespace: "CecoChat", serviceVersion: "0.1")
            .AddEnvironmentVariableDetector();

        services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(serviceResourceBuilder);
                tracing.AddAspNetCoreInstrumentation(aspnet =>
                {
                    aspnet.EnableGrpcAspNetCoreSupport = true;
                    HashSet<string> excludedPaths = new()
                    {
                        PrometheusOptions.ScrapeEndpointPath, HealthPaths.Health, HealthPaths.Startup, HealthPaths.Live, HealthPaths.Ready
                    };
                    aspnet.Filter = httpContext => !excludedPaths.Contains(httpContext.Request.Path);
                });
                tracing.AddKafkaInstrumentation();
                tracing.AddChatsInstrumentation();
                tracing.ConfigureSampling(TracingSamplingOptions);
                tracing.ConfigureOtlpExporter(TracingExportOptions);
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(serviceResourceBuilder);
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddChatsInstrumentation();
                metrics.ConfigurePrometheusAspNetExporter(PrometheusOptions);
            });
    }

    private void AddHealthServices(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<ConfigDbInitHealthCheck>(
                "config-db-init",
                tags: new[] { HealthTags.Health, HealthTags.Startup })
            .AddCheck<ChatsDbInitHealthCheck>(
                "chats-db-init",
                tags: new[] { HealthTags.Health, HealthTags.Startup })
            .AddCheck<HistoryConsumerHealthCheck>(
                "history-consumer",
                tags: new[] { HealthTags.Health, HealthTags.Startup, HealthTags.Live })
            .AddCheck<ReceiversConsumerHealthCheck>(
                "receivers-consumer",
                tags: new[] { HealthTags.Health, HealthTags.Startup, HealthTags.Live })
            .AddCheck<SendersConsumerHealthCheck>(
                "senders-consumer",
                tags: new[] { HealthTags.Health, HealthTags.Startup, HealthTags.Live })
            .AddRedis(
                "config-db",
                ConfigDbOptions,
                tags: new[] { HealthTags.Health, HealthTags.Ready })
            .AddCassandra(
                name: "chats-db",
                timeout: _chatsDbOptions.HealthTimeout,
                tags: new[] { HealthTags.Health, HealthTags.Ready })
            .AddKafka(
                "backplane",
                _backplaneOptions.Kafka,
                _backplaneOptions.Health,
                tags: new[] { HealthTags.Health });

        services.AddSingleton<ConfigDbInitHealthCheck>();
        services.AddSingleton<ChatsDbInitHealthCheck>();
        services.AddSingleton<HistoryConsumerHealthCheck>();
        services.AddSingleton<ReceiversConsumerHealthCheck>();
        services.AddSingleton<SendersConsumerHealthCheck>();
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        // ordered hosted services
        builder.RegisterHostedService<InitDynamicConfig>();
        builder.RegisterHostedService<InitChatsDb>();
        builder.RegisterHostedService<InitBackplane>();
        builder.RegisterHostedService<InitBackplaneComponents>();

        // configuration
        IConfiguration configDbConfig = Configuration.GetSection("ConfigDb");
        builder.RegisterModule(new ConfigDbAutofacModule(configDbConfig, registerHistory: true));

        // chats db
        IConfiguration chatsDbConfig = Configuration.GetSection("ChatsDb");
        builder.RegisterModule(new ChatsDbAutofacModule(chatsDbConfig));
        builder.RegisterModule(new CassandraHealthAutofacModule(chatsDbConfig));

        // backplane
        builder.RegisterType<KafkaAdmin>().As<IKafkaAdmin>().SingleInstance();
        builder.RegisterOptions<KafkaOptions>(Configuration.GetSection("Backplane:Kafka"));
        builder.RegisterType<HistoryConsumer>().As<IHistoryConsumer>().SingleInstance();
        builder.RegisterType<StateConsumer>().As<IStateConsumer>().SingleInstance();
        builder.RegisterFactory<KafkaConsumer<Null, BackplaneMessage>, IKafkaConsumer<Null, BackplaneMessage>>();
        builder.RegisterModule(new KafkaAutofacModule());
        builder.RegisterOptions<BackplaneOptions>(Configuration.GetSection("Backplane"));

        // shared
        builder.RegisterType<ContractMapper>().As<IContractMapper>().SingleInstance();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<ChatsService>();
            endpoints.MapHttpHealthEndpoints(setup =>
            {
                Func<HttpContext, HealthReport, Task> responseWriter = (context, report) => CustomHealth.Writer(serviceName: "chats", context, report);
                setup.Health.ResponseWriter = responseWriter;

                if (env.IsDevelopment())
                {
                    setup.Startup.ResponseWriter = responseWriter;
                    setup.Live.ResponseWriter = responseWriter;
                    setup.Ready.ResponseWriter = responseWriter;
                }
            });
        });

        app.UseOpenTelemetryPrometheusScrapingEndpoint(context => context.Request.Path == PrometheusOptions.ScrapeEndpointPath);
    }
}
