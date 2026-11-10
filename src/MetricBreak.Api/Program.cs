using MetricBreak.Api;
using MetricBreak.Api.Sessions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReplaySessionManager>();
builder.Services.AddSingleton<SessionInvestigationStore>();
builder.Services.AddSingleton<ProjectWorkspaceStore>();
builder.Services.AddScoped<CsvSessionImportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapProjectEndpoints();
app.MapSessionEndpoints();
app.MapReplayEndpoints();
app.MapInvestigationEndpoints();
app.MapBreakpointEndpoints();

app.Run();

public partial class Program { }

