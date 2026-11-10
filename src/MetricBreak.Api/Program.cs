
using MetricBreak.Api.Sessions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReplaySessionManager>();
builder.Services.AddScoped<CsvSessionImportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost(
    "/api/sessions",
    async (
        IFormFile file,
        CsvSessionImportService importService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var response = await importService.ImportAsync(
                file,
                cancellationToken);

            return Results.Created(
                $"/api/sessions/{response.SessionId}",
                response);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                Error = exception.Message
            });
        }
        catch (FormatException exception)
        {
            return Results.BadRequest(new
            {
                Error = exception.Message
            });
        }
        catch (InvalidDataException exception)
        {
            return Results.BadRequest(new
            {
                Error = exception.Message
            });
        }
    })
    .DisableAntiforgery()
    .WithName("CreateReplaySession")
    .WithTags("Sessions")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<CreateSessionResponse>(
        StatusCodes.Status201Created)
    .Produces(
        StatusCodes.Status400BadRequest);

app.Run();

