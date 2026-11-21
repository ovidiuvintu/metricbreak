using MetricBreak.Api.Endpoints;
using MetricBreak.Api.Investigations;
using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;
using MetricBreak.Ingestion.Csv;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReplaySessionManager>();
builder.Services.AddSingleton<RecordingCatalogStore>();
builder.Services.AddSingleton<CsvRecordingImporter>();
builder.Services.AddSingleton<SessionInvestigationStore>();
builder.Services.AddSingleton<ProjectWorkspaceStore>();
builder.Services.AddSingleton<InvestigationBookmarkService>();
builder.Services.AddScoped<CsvSessionImportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

_ = app.Services
    .GetRequiredService<ProjectWorkspaceStore>()
    .TryRestoreLastOpenedProject(out _);

app.MapProjectEndpoints();
app.MapRecordingEndpoints();
app.MapSessionEndpoints();
app.MapReplayEndpoints();
app.MapInvestigationEndpoints();
app.MapBreakpointEndpoints();
app.MapChannelEndpoints();

app.Run();

public partial class Program { }

