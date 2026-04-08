using AspireApp.ServiceDefaults;
using ContactExtractor.Api.Infrastructure;
using ContactExtractor.MigrationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("contactextractor"))
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();