using Microsoft.AspNetCore.Builder;
using Play.Common.Configurations;
using Play.Identity.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAzureKeyVault();
builder.Services.AddServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.ConfigurePipeline(app.Configuration, app.Environment);

app.Run();
