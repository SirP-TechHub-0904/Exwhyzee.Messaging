using System;
using System.IO;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

// Multi-path .env file discovery
var possibleEnvPaths = new[]
{
    Path.Combine(AppContext.BaseDirectory, ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Exwhyzee.Messaging.Web", ".env")
};

foreach (var envPath in possibleEnvPaths)
{
    if (File.Exists(envPath))
    {
        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#") && line.Contains('='))
            {
                var parts = line.Split(new[] { '=' }, 2);
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Register Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register Swagger Documentation with ApiKey Header Security
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XYZSMS Developer API",
        Version = "v1",
        Description = "RESTful HTTP API for XYZSMS messaging, account balance, delivery logs, and sender ID inspection."
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key authentication. Enter your API Key in the 'X-Api-Key' header or use 'Authorization: Bearer <ApiKey>'.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Database Connection from .env / Configuration
var connectionString = builder.Configuration.GetConnectionString("ZyxsmsDbConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__ZyxsmsDbConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Core Messaging & Infrastructure Services
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<IZeptoMailService, ZeptoMailService>();
builder.Services.AddScoped<ZeptoMailService>();
builder.Services.AddScoped<ISendEmail, SendEmail>();
builder.Services.AddScoped<SendEmail>();

var app = builder.Build();

// Enable Swagger UI in both Development and Production for developer portal usage
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "XYZSMS API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
