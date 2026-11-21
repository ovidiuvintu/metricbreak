
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetricBreak.Api.Investigations;
using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class MetricBreakApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _workspaceStatePath;
    private readonly string _catalogPath;
    private readonly string _archiveDirectory;
    private readonly string _investigationsDirectory;

    public MetricBreakApiFactory(
        string? workspaceStatePath = null)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-api-tests",
            Guid.NewGuid().ToString("N"));

        _workspaceStatePath = Path.GetFullPath(
            workspaceStatePath ?? Path.Combine(root, "workspace-state.json"));
        _catalogPath = Path.Combine(root, "recordings-catalog.json");
        _archiveDirectory = Path.Combine(root, "recordings");
        _investigationsDirectory = Path.Combine(root, "investigations");
    }

    public ReplaySessionManager SessionManager =>
        Services.GetRequiredService<ReplaySessionManager>();

    public void ClearRecordingCatalog()
    {
        if (File.Exists(_catalogPath))
        {
            File.Delete(_catalogPath);
        }
    }

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

            services.RemoveAll<RecordingCatalogStore>();
            services.AddSingleton(
                new RecordingCatalogStore(
                    _catalogPath,
                    _archiveDirectory));

            services.RemoveAll<SessionInvestigationStore>();
            services.AddSingleton(
                new SessionInvestigationStore(_investigationsDirectory));
        });
    }
}
