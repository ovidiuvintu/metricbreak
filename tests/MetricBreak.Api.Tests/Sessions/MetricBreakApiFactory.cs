using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MetricBreak.Api.Sessions;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class MetricBreakApiFactory
    : WebApplicationFactory<Program>
{
    public ReplaySessionManager SessionManager =>
        Services.GetRequiredService<ReplaySessionManager>();
}
