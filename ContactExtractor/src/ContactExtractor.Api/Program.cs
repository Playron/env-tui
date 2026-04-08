using ContactExtractor.Api.AI;
using ContactExtractor.Api.Endpoints;
using ContactExtractor.Api.Infrastructure;
using ContactExtractor.Api.Messaging.Consumers;
using ContactExtractor.Api.Services;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// EF Core med SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=contactextractor.db"));

// AI-service – velger provider basert på config (claude / openai / ollama / none)
builder.Services.AddLlmService(builder.Configuration);

// Automatisk DI-registrering av alle IFileParser-implementasjoner via Scrutor
builder.Services.Scan(scan => scan
    .FromAssemblyOf<IFileParser>()
    .AddClasses(c => c.AssignableTo<IFileParser>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());

builder.Services.AddScoped<FileParserFactory>();
builder.Services.AddScoped<ContactExtractionService>();

// SSE-bro mellom consumer og klient (singleton – holder channels per sesjon)
builder.Services.AddSingleton<SseProgressService>();

// MassTransit – InMemory for utvikling, RabbitMQ for produksjon
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ExtractionConsumer>();

    if (builder.Configuration.GetValue<bool>("UseRabbitMq"))
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration.GetConnectionString("RabbitMq") ?? "localhost");
            cfg.ReceiveEndpoint("extraction-queue", e =>
            {
                e.PrefetchCount = 3;  // Maks 3 samtidige AI-ekstraksjoner
                e.ConfigureConsumer<ExtractionConsumer>(context);
            });
        });
    }
    else
    {
        // InMemory – ingen RabbitMQ nødvendig for utvikling
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConcurrentMessageLimit = 3;  // Tilsvarer PrefetchCount for InMemory
            cfg.ConfigureEndpoints(context);
        });
    }
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ContactExtractor API",
        Version = "v1",
        Description = "API for å ekstrahere kontaktinformasjon fra ulike filformater. " +
                      "Støtter AI-drevet parsing (Claude, OpenAI, Ollama) for ustrukturerte filer."
    });
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration.GetValue<string>("AllowedOrigins") ?? "http://localhost:5173"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()));

// Maks filstørrelse 10 MB
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ContactExtractor v1"));

    // Auto-migrering i utvikling
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Endpoint-mapping
app.MapUploadEndpoints();
app.MapContactEndpoints();
app.MapExportEndpoints();
app.MapSettingsEndpoints();

app.MapGet("/health", () => TypedResults.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
   .WithTags("Health");

app.Run();

// For integration testing
public partial class Program { }
