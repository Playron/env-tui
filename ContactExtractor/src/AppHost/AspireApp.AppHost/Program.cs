var builder = DistributedApplication.CreateBuilder(args);

// ── 1. Database (SQL Server) ──────────────────────────────────────
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var contactDb = sql.AddDatabase("contactextractor");

// ── 2. RabbitMQ ───────────────────────────────────────────────────
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// ── 3. Keycloak ───────────────────────────────────────────────────
var keycloakAdmin = builder.AddParameter("keycloak-admin", secret: true);

var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdmin)
    .WithDataVolume()
    .WithRealmImport("./realms")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("KC_HEALTH_ENABLED", "true");

// ── 4. Ollama + NuExtract ─────────────────────────────────────────
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithOpenWebUI();

var nuextract = ollama.AddModel("nuextract");

// ── 5. Database migrations ───────────────────────────────────────
var migrations = builder.AddProject<Projects.ContactExtractor_MigrationService>("migrations")
    .WithReference(contactDb)
    .WaitFor(contactDb);

// ── 6. API-prosjekt ───────────────────────────────────────────────
var api = builder.AddProject<Projects.ContactExtractor_Api>("contactextractor-api")
    .WithReference(contactDb)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithReference(nuextract)
    .WaitFor(contactDb)
    .WaitFor(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(nuextract)
    .WaitForCompletion(migrations)
    .WithEnvironment("Llm__Mode", "single")
    .WithEnvironment("Llm__Provider", "ollama");

// ── 7. React Frontend ─────────────────────────────────────────────
builder.AddNpmApp("frontend", "../../../../contact-extractor-ui", "dev")
    .WithReference(api)
    .WithReference(keycloak)
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
