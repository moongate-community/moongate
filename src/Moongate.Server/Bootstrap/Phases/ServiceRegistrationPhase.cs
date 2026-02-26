using System.Security.Cryptography;
using DryIoc;
using Moongate.Abstractions.Extensions;
using Moongate.Abstractions.Types;
using Moongate.Core.Extensions.Logger;
using Moongate.Core.Types;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Generated;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Data.Version;
using Moongate.Server.Http;
using Moongate.Server.Http.Interfaces;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Bootstrap.Phases;

/// <summary>
/// Bootstrap phase 2: registers HTTP server, scripting modules, and all DI services.
/// </summary>
internal sealed class ServiceRegistrationPhase : IBootstrapPhase
{
    public int Order => 2;

    public string Name => "ServiceRegistration";

    public void Configure(BootstrapContext context)
    {
        RegisterHttpServer(context);
        RegisterScriptUserData(context);
        RegisterScriptModules(context);
        RegisterServices(context);
    }

    private static MoongateHttpServiceOptions CreateHttpServiceOptions(BootstrapContext context)
    {
        var jwtSigningKey = ResolveHttpJwtSigningKey(context);

        return new()
        {
            DirectoriesConfig = context.DirectoriesConfig,
            IsOpenApiEnabled = context.Config.Http.IsOpenApiEnabled,
            Port = context.Config.Http.Port,
            MinimumLogLevel = context.Config.LogLevel.ToSerilogLogLevel(),
            Jwt = new()
            {
                IsEnabled = context.Config.Http.Jwt.IsEnabled,
                SigningKey = jwtSigningKey,
                Issuer = context.Config.Http.Jwt.Issuer,
                Audience = context.Config.Http.Jwt.Audience,
                ExpirationMinutes = context.Config.Http.Jwt.ExpirationMinutes
            }
        };
    }

    private static void RegisterHttpServer(BootstrapContext context)
    {
        if (!context.Config.Http.IsEnabled)
        {
            context.Logger.Information("HTTP Server disabled.");

            return;
        }

        context.Container.RegisterMoongateService<IMoongateHttpService, MoongateHttpService>(ServicePriority.HttpServer);
        context.Logger.Information("HTTP Server enabled.");
        context.Container.RegisterInstance(CreateHttpServiceOptions(context));
    }

    private static void RegisterScriptModules(BootstrapContext context)
    {
        context.Container.RegisterInstance(
            new LuaEngineConfig(
                Path.Combine(context.DirectoriesConfig.Root, ".luarc"),
                context.DirectoriesConfig[DirectoryType.Scripts],
                VersionUtils.Version
            )
        );
        ScriptModuleRegistry.Register(context.Container);
        Generated.ScriptModuleRegistry.Register(context.Container);

        if (!context.Container.IsRegistered<List<ScriptModuleData>>())
        {
            context.Container.RegisterInstance(new List<ScriptModuleData>());
        }
    }

    private static void RegisterScriptUserData(BootstrapContext context)
    {
        context.Container.RegisterLuaUserData<PlayerConnectedEvent>();
        context.Container.RegisterLuaUserData<PlayerDisconnectedEvent>();
        context.Container.RegisterLuaUserData<ClientVersion>();
    }

    private static void RegisterServices(BootstrapContext context)
    {
        BootstrapServiceRegistration.Register(
            context.Container,
            context.Config,
            context.DirectoriesConfig,
            context.ConsoleUiService
        );
    }

    private static string ResolveHttpJwtSigningKey(BootstrapContext context)
    {
        var configuredKey = context.Config.Http.Jwt.SigningKey;

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            return configuredKey;
        }

        var envKey = Environment.GetEnvironmentVariable("MOONGATE_HTTP_JWT_SIGNING_KEY");

        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        if (!context.Config.Http.Jwt.IsEnabled)
        {
            return string.Empty;
        }

        Span<byte> buffer = stackalloc byte[64];
        RandomNumberGenerator.Fill(buffer);
        var generated = Convert.ToHexString(buffer);

        context.Logger.Warning(
            "HTTP JWT is enabled but no signing key was configured. Generated ephemeral key for this process. " +
            "Set MOONGATE_HTTP_JWT_SIGNING_KEY to keep tokens valid across restarts."
        );

        return generated;
    }
}
