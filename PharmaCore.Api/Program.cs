using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PharmaCore.Api.Auth;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Products;
using PharmaCore.Infrastructure.Security;
using PharmaCore.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

var connectionString = builder.Configuration.GetConnectionString("PharmaCore")
    ?? throw new InvalidOperationException("ConnectionStrings:PharmaCore is not configured (use user-secrets or ConnectionStrings__PharmaCore).");
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be set to at least 32 characters (user-secrets or Jwt__SigningKey).");

builder.Services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ProductService>();
DateOnlyTypeHandler.Register();
builder.Services.AddScoped<PharmaCore.Application.Inventory.IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<PharmaCore.Application.Inventory.InventoryService>();
builder.Services.AddScoped<PharmaCore.Application.Drugs.IDrugReferenceRepository, DrugReferenceRepository>();
builder.Services.AddScoped<PharmaCore.Application.Drugs.DrugReferenceService>();
builder.Services.AddSingleton<DbMigrator>();
builder.Services.AddValidatorsFromAssemblyContaining<SaveProductRequestValidator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(o =>
{
    foreach (var p in Permissions.All)
        o.AddPolicy(p, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", p));
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("login", w => { w.PermitLimit = 10; w.Window = TimeSpan.FromMinutes(1); });
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // TODO: add the Bearer security definition once the Swashbuckle/OpenApi package versions are pinned and built.
builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "sqlserver");

var app = builder.Build();

app.UseExceptionHandler(a => a.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        DuplicateBarcodeException e => (409, e.Message),
        PharmaCore.Application.Drugs.InvalidDrugFileException e => (400, e.Message),
        PharmaCore.Application.Inventory.NegativeStockException e => (409, e.Message),
        PharmaCore.Application.Inventory.WarehouseNotFoundException e => (404, e.Message),
        PharmaCore.Application.Inventory.ProductNotFoundException e => (404, e.Message),
        PharmaCore.Application.Inventory.BatchNotFoundException e => (404, e.Message),
        PharmaCore.Domain.Inventory.InsufficientStockException e => (409, e.Message),
        _ => (500, "An unexpected error occurred.")
    };
    if (status == 500) Log.Error(ex, "Unhandled exception");
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title });
}));

app.UseSerilogRequestLogging();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Migrations run on startup when enabled (default on in Development).
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
{
    var dir = app.Configuration["Database:MigrationsPath"] ?? Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../../database/migrations"));
    var applied = await app.Services.GetRequiredService<DbMigrator>().ApplyAsync(dir);
    Log.Information("Applied migrations: {Migrations}", applied.Count == 0 ? "none (up to date)" : string.Join(", ", applied));
}

// Development-only bootstrap admin. Password comes from configuration, never from source.
if (app.Environment.IsDevelopment())
{
    var seedPassword = app.Configuration["DevSeed:AdminPassword"];
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    if (!string.IsNullOrEmpty(seedPassword) && !await users.AnyUserAsync(default))
    {
        await users.CreateAdminAsync("admin", scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(seedPassword), default);
        Log.Warning("DEV SEED: created user 'admin' with the password from DevSeed:AdminPassword.");
    }
}

app.Run();

public partial class Program;
