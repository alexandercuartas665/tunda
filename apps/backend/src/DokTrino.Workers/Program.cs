using DokTrino.Application;
using DokTrino.Application.Common;
using DokTrino.Infrastructure;
using DokTrino.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddScoped<ITenantContext, SystemTenantContext>();

builder.Services.AddHostedService<RecurringBillingWorker>();

var host = builder.Build();
host.Run();
