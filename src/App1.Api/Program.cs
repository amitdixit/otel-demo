using App.Api.Endpoints;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

builder.Services.AddHttpClient("app2", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["App2Endpoint"]!);
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add(
        "accept", "application/json");
});

builder.Services.AddOpenTelemetry().WithTracing(builder =>
{
    builder.AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("PublishRequest")
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("App1"))
        .AddOtlpExporter(opts =>
        {
            opts.Endpoint =
                new Uri(
                    $"{configuration["Jaeger:Protocol"]}://{configuration["Jaeger:Host"]}:{configuration["Jaeger:Port"]}");
        });
});




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

PublishEndpoints.MapPublishEndpoints(app);

app.Run();

