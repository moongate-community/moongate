using System.Globalization;
using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Server.Admin.Authentication;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Admin.Services.Grpc;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Admin.Internal;

internal static class AdminGrpcApplication
{
    private static readonly TimeSpan MaximumCallDuration = TimeSpan.FromSeconds(15);

    public static void AddServices(
        IServiceCollection services,
        AdminApiConfig config,
        IAdminSessionStore sessions,
        IAdminLoginThrottle throttle,
        IAdminServerInfoProvider info,
        IAccountService? accounts,
        IAccountAdminAccessService? authority
    )
    {
        // External instance registration keeps ownership with Moongate's root container.
        services.AddSingleton(sessions);
        services.AddSingleton(throttle);
        services.AddSingleton(info);

        if (accounts is not null) { services.AddSingleton(accounts); }

        if (authority is not null) { services.AddSingleton(authority); }
        services.AddGrpc(
            options =>
            {
                options.MaxReceiveMessageSize = config.MaxReceiveMessageBytes;
                options.EnableDetailedErrors = false;
                options.Interceptors.Add<AdminExceptionInterceptor>();
            }
        );
        services.AddAuthentication(AdminAuthorizationPolicies.Scheme)
                .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
                    AdminAuthorizationPolicies.Scheme,
                    _ => { }
                );
        services.AddAuthorization(
            options => options.AddPolicy(
                AdminAuthorizationPolicies.AccountAdministration,
                policy => policy.RequireAuthenticatedUser().RequireRole(AdminAuthorizationPolicies.AdministratorRole)
            )
        );
    }

    public static void Configure(WebApplication app, ServerMode mode, AdminRequestGate gate)
    {
        app.UseRouting();

        // Admission precedes authentication, bounding Redis/password work as well as RPC bodies.
        app.Use(
            async (context, next) =>
            {
                var status = gate.TryEnter();

                try
                {
                    if (status != StatusCode.OK)
                    {
                        context.Items["AdminStatus"] = status.ToString();
                        context.Response.ContentType = "application/grpc";
                        context.Response.Headers["grpc-status"] = ((int)status).ToString(CultureInfo.InvariantCulture);
                        context.Response.Headers["grpc-message"] = "Administration endpoint unavailable or busy.";

                        return;
                    }
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                    deadline.CancelAfter(MaximumCallDuration);
                    context.RequestAborted = deadline.Token;
                    await next(context);
                }
                finally
                {
                    var operation = context.GetEndpoint()?.DisplayName ?? "unmapped";
                    Log.ForContext<AdminRequestGate>()
                       .Information(
                           "Admin operation {Operation} actor {ActorId} target {TargetId} status {Status} correlation {CorrelationId}",
                           operation,
                           context.Items["AdminActorId"] ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                           context.Items["AdminTargetId"],
                           context.Items["AdminStatus"] ??
                           context.Response.StatusCode.ToString(CultureInfo.InvariantCulture),
                           context.TraceIdentifier
                       );

                    if (status == StatusCode.OK) { gate.Exit(); }
                }
            }
        );
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGrpcService<AdminSessionGrpcService>().AllowAnonymous();
        app.MapGrpcService<AdminServerGrpcService>().RequireAuthorization();

        if ((mode & ServerMode.Login) != 0)
        {
            app.MapGrpcService<AdminLoginGrpcService>().AllowAnonymous();
            app.MapGrpcService<AdminAccountsGrpcService>()
               .RequireAuthorization(AdminAuthorizationPolicies.AccountAdministration);
            app.MapGrpcService<AdminAccountSessionsGrpcService>()
               .RequireAuthorization(AdminAuthorizationPolicies.AccountAdministration);
        }
    }
}
