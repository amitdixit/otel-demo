using App2.ApiDb.Endpoints;
using App2.ApiDb.Repository;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<ISqlRepository, SqlRepository>();
builder.Services.AddTransient<IRabbitRepository, RabbitRepository>();
builder.Services.AddOpenTelemetry().WithTracing(builder =>
{
    builder.AddAspNetCoreInstrumentation()
        .AddSource("RabbitRepository")
        .AddSqlClientInstrumentation()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("App2"))
        .AddOtlpExporter(opts =>
        {
            opts.Endpoint =
                new Uri($"{configuration["Jaeger:Protocol"]}://{configuration["Jaeger:Host"]}:{configuration["Jaeger:Port"]}");
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

AppEndpoints.MapPublishEndpoints(app);

app.Run();

