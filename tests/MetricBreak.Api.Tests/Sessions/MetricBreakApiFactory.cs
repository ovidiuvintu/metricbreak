
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class MetricBreakApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _workspaceStatePath;

    public MetricBreakApiFactory(
        string? workspaceStatePath = null)
    {
        _workspaceStatePath = Path.GetFullPath(
            workspaceStatePath ?? Path.Combine(
                Path.GetTempPath(),
                "metricbreak-api-tests",
                Guid.NewGuid().ToString("N"),
                "workspace-state.json"));
    }

    public ReplaySessionManager SessionManager =>
        Services.GetRequiredService<ReplaySessionManager>();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace the production workspace store
            // with a test-specific instance.
            services.RemoveAll<ProjectWorkspaceStore>();

            services.AddSingleton(
                new ProjectWorkspaceStore(
                    _workspaceStatePath));
        });
    }
}
