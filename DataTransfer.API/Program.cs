using DataTransfer.Application.DTOs;
using DataTransfer.Application.Services;
using DataTransfer.Application.Validators;
using DataTransfer.Core.Interfaces;
using DataTransfer.Infrastructure.Data;
using DataTransfer.Infrastructure.Repositories;
using DataTransfer.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/datatransfer.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SQL Data Transfer API",
        Version = "v1",
        Description = "API for transferring data between SQL Server databases"
    });
});

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.AllowAnyOrigin() // Allow any origin for testing
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            
            // Comment out the specific origin configuration for now
            // policy.WithOrigins("http://localhost:4200")
            //       .AllowAnyHeader()
            //       .AllowAnyMethod();
        });
});

// Add Entity Framework Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerConnection") ?? 
                        "Server=localhost;Database=ISQLHelper;Integrated Security=true;TrustServerCertificate=true;"));

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                builder.Configuration["Jwt:Key"] ?? "DefaultKeyForDevelopment1234567890ABCDEFGHIJKLMN")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// Add MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(DataTransfer.Application.Commands.ExecuteTransferCommand).Assembly));

// Add Validators
builder.Services.AddScoped<DatabaseConnectionDtoValidator>();
builder.Services.AddScoped<IValidator<DatabaseConnectionDto>, DatabaseConnectionDtoValidator>();
builder.Services.AddScoped<IValidator<TransferRequestDto>, TransferRequestDtoValidator>();
builder.Services.AddScoped<IValidator<LoginRequestDto>, LoginRequestDtoValidator>();
builder.Services.AddScoped<IValidator<RegisterUserDto>, RegisterUserDtoValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Add Application Services
builder.Services.AddScoped<IDatabaseService, SqlDatabaseService>();
builder.Services.AddScoped<IDataTransferService, DataTransferService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<AuthService>();

// Add DataSource Services
builder.Services.AddScoped<IDataSourceRepository, DataSourceRepository>();
builder.Services.AddScoped<IDataSourceService, DataSourceService>();

var app = builder.Build();

// Initialize Database
app.Services.InitializeDatabase();

// Configure the HTTP request pipeline.
// Enable Swagger in all environments
app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SQL Data Transfer API v1");
    c.RoutePrefix = "swagger";
});

// Only redirect HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Add CORS logging middleware for debugging
app.Use(async (context, next) =>
{
    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("CORSMiddleware");
    
    logger.LogInformation("Request Origin: {Origin}", context.Request.Headers.Origin);
    logger.LogInformation("Request Path: {Path}", context.Request.Path);
    logger.LogInformation("Request Method: {Method}", context.Request.Method);
    
    await next();
    
    logger.LogInformation("Response Status: {Status}", context.Response.StatusCode);
    if (context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
    {
        logger.LogInformation("CORS Allow Origin: {AllowOrigin}", 
            context.Response.Headers["Access-Control-Allow-Origin"]);
    }
});

// Use CORS middleware - This must be placed before UseAuthorization and MapControllers
app.UseCors("AllowAngularApp");

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add health check with more debugging info
app.MapGet("/health", () => 
{
    return Results.Ok(new 
    { 
        Status = "Healthy", 
        Timestamp = DateTime.UtcNow, 
        Environment = app.Environment.EnvironmentName,
        Message = "API is running correctly"
    });
});

app.Run();
