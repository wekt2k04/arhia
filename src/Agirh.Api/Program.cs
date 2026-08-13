using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.AI;
using Polly.Extensions.Http;
using Polly;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using Agirh.Infrastructure.MAF;
using Agirh.Domain.Interfaces;
using Agirh.Core.Interfaces;
using Agirh.Core.Models;
using Agirh.Core.Settings;
using Agirh.Api.Logging;

var builder = WebApplication.CreateBuilder(args);

// ── File logger : capture + conservation des logs de l'API.
// Le fichier est RÉINITIALISÉ (tronqué) à chaque démarrage → un run = un fichier frais.
var logFile = builder.Configuration["Logging:File:Path"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "logs", "agirh-api.log");
try
{
    builder.Logging.AddProvider(new FileLoggerProvider(logFile));
    Console.WriteLine($"[FileLogger] logs -> {logFile} (réinitialisé à ce démarrage)");
}
catch (Exception ex)
{
    Console.WriteLine($"[FileLogger] échec d'initialisation de {logFile}: {ex.Message} (logs console uniquement)");
}

// ── Audit log structuré (JSONL par requête de chat) : rôle, modèles, résultat.
// Fichier RÉINITIALISÉ à chaque démarrage, dans logs/agirh-audit.jsonl.
var auditFile = builder.Configuration["Logging:AuditFile:Path"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "logs", "agirh-audit.jsonl");
try
{
    builder.Services.AddSingleton<IChatAuditLogger>(new ChatAuditLogger(auditFile));
    Console.WriteLine($"[ChatAudit] audit -> {auditFile} (réinitialisé à ce démarrage)");
}
catch (Exception ex)
{
    Console.WriteLine($"[ChatAudit] échec d'initialisation de {auditFile}: {ex.Message} (audit désactivé)");
    builder.Services.AddSingleton<IChatAuditLogger>(new NoopChatAuditLogger());
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Database — SQL Server only (Docker container agirh-sql:1433)
var connString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));

// JWT — fail-fast au démarrage : une clé absente OU vide (ex. profil local sans
// section Jwt:) provoque une erreur claire immédiate, jamais un IDX10703 lazy
// sur la 1ère requête authentifiée.
var configuredJwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey))
    throw new InvalidOperationException("JWT Key not configured (Jwt:Key) — renseignez appsettings.local.json ou la variable d'environnement Jwt__Key.");
var jwtKey = configuredJwtKey;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Rate limiting — fixed-window per bucket+IP (login 5/min, chat 20/min, open 1000/min)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Message = "Trop de requêtes. Veuillez réessayer plus tard." }, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var bucket = context.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase) ? "login"
            : context.Request.Path.StartsWithSegments("/api/agent/chat", StringComparison.OrdinalIgnoreCase) ? "chat"
            : "open";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"{bucket}|{ip}";
        var permitLimit = bucket switch
        {
            "login" => 5,
            "chat" => 20,
            _ => 1000,
        };
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
        });
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddHealthChecks();

// Repositories
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
builder.Services.AddScoped<IChecklistRepository, ChecklistRepository>();
builder.Services.AddScoped<IPayrollProfileRepository, PayrollProfileRepository>();
builder.Services.AddScoped<ISalaryAdvanceRepository, SalaryAdvanceRepository>();
builder.Services.AddScoped<IAgentConversationRepository, AgentConversationRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Dynamic discovery: scan all non-abstract IMafTool implementations
var mafAssembly = typeof(Agirh.Infrastructure.MAF.ChecklistFunctions).Assembly;
foreach (var type in mafAssembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IMafTool).IsAssignableFrom(t)))
{
    builder.Services.AddScoped(typeof(IMafTool), type);
}

// ─── ORCHESTRATOR-WORKER PIPELINE WITH ACTOR-CRITIC REFLECTION ──────────

// Options typées du pipeline IA (section "AI:", plus "Embedding:ExpectedDimension").
// Les services injectent IOptions<AIOptions> au lieu de lire IConfiguration.
builder.Services.AddOptions<AIOptions>()
    .Bind(builder.Configuration.GetSection("AI"))
    .Bind(builder.Configuration.GetSection("Embedding"));

// Named HttpClient for AI calls (socket pooling, bounded timeout for health check)
builder.Services.AddHttpClient("OllamaClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AI:HealthCheckTimeoutSeconds", 15));
});

// HardState Extractor — extracts Zone Rouge from JWT
builder.Services.AddScoped<IHardStateExtractor, HardStateExtractor>();

// Agent 1: Cognitive Profiler — calls Ollama tiny model for JSON extraction
var configuredOllamaUri = builder.Configuration["AI:Endpoint"];
// Lot D — Production exige un endpoint non vide (fourni par l'environnement) :
// un "Endpoint": "" en prod échouerait silencieusement sur localhost.
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(configuredOllamaUri))
    throw new InvalidOperationException("AI:Endpoint is required in Production (set via environment).");
var ollamaUri = string.IsNullOrWhiteSpace(configuredOllamaUri) ? "http://localhost:11434" : configuredOllamaUri;

builder.Services.AddHttpClient<ICognitiveProfiler, ProfilerService>(
    client => { client.BaseAddress = new Uri(ollamaUri); client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AI:ProfilerTimeout", 300)); })
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt =>
            TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1))));

// Agent 2: Zero-Trust Dispatcher — pure C# RBAC routing (no LLM)
builder.Services.AddScoped<IZeroTrustDispatcher, ZeroTrustDispatcher>();

// Agent 3: Worker Executor — executes MAF function
builder.Services.AddScoped<IWorkerExecutor, WorkerExecutor>();

// Pre-Flight Validator — C# parameter validation before tool dispatch
builder.Services.AddScoped<IPreFlightValidator, PreFlightValidator>();

// Actor-Critic Reflection agents
builder.Services.AddHttpClient<ISynthesizerAgent, SynthesizerAgent>(
    client => { client.BaseAddress = new Uri(ollamaUri); client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AI:SynthesizerTimeoutSeconds", 120)); })
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt =>
            TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1))));

builder.Services.AddHttpClient<ICheckerAgent, CheckerAgent>(
    client => { client.BaseAddress = new Uri(ollamaUri); client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AI:CheckerTimeoutSeconds", 120)); })
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt =>
            TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1))));

// Orchestrator Service — coordinates the full pipeline
builder.Services.AddScoped<IAgentOrchestratorService, AgentOrchestratorService>();

// ─────────────────────────────────────────────────────────────────────────

// Embedding generator for RAG (bounded timeout 180s, retry via Polly incl. timeouts)
builder.Services.AddHttpClient<IEmbeddingGenerator<string, Embedding<float>>, OllamaEmbeddingGenerator>(
    client => { client.BaseAddress = new Uri(ollamaUri); client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AI:EmbeddingTimeoutSeconds", 180)); })
    .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
        .Or<TaskCanceledException>(ex => ex.InnerException is TimeoutException)
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1))));

// Ingestion Service
builder.Services.AddScoped<IngestionService>();

// Warmup — précharge les modèles Ollama en VRAM au démarrage (non bloquant, cf. OllamaWarmupService).
builder.Services.AddHostedService<OllamaWarmupService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    options.AddPolicy("ProdWhitelist", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Exception handler — MUST BE FIRST
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { Message = "Une erreur interne s'est produite" });
    });
});

app.UseCors(app.Environment.IsProduction() ? "ProdWhitelist" : "AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

// Init DB (skip when testing)
if (!bool.TryParse(builder.Configuration["SkipDbInit"], out var skipDbInit) || !skipDbInit)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            // Correction F1 — le seed des comptes démo (admin123/…) n'existe QU'EN
            // développement : jamais en Production (base fraîche = comptes connus).
            if (!builder.Environment.IsProduction() && !db.Employees.Any())
            {
                db.Employees.Add(new Agirh.Domain.Entities.Employee { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "AGIRH", Email = "admin@agirh.fr", Role = "Admin", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") });
                db.Employees.Add(new Agirh.Domain.Entities.Employee { Id = Guid.NewGuid(), FirstName = "Marie", LastName = "Martin", Email = "marie.martin@agirh.fr", Role = "Manager", IsActive = true, LeaveBalance = 25, PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123") });
                db.Employees.Add(new Agirh.Domain.Entities.Employee { Id = Guid.NewGuid(), FirstName = "Jean", LastName = "Dupont", Email = "jean.dupont@agirh.fr", Role = "Collaborator", IsActive = true, LeaveBalance = 15, PasswordHash = BCrypt.Net.BCrypt.HashPassword("collab123") });
                db.Employees.Add(new Agirh.Domain.Entities.Employee { Id = Guid.NewGuid(), FirstName = "Pierre", LastName = "Durand", Email = "pierre.durand@agirh.fr", Role = "Collaborator", IsActive = false, PasswordHash = BCrypt.Net.BCrypt.HashPassword("collab123") });
                db.SaveChanges();
            }

            var jeanDupont = db.Employees.FirstOrDefault(e => e.Email == "jean.dupont@agirh.fr");
            if (jeanDupont != null && !db.PayrollProfiles.Any(p => p.EmployeeId == jeanDupont.Id))
            {
                db.PayrollProfiles.Add(new Agirh.Domain.Entities.PayrollProfile
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = jeanDupont.Id,
                    NetSalary = 10000,
                    Iban = "FR7612345678901234567890123",
                    MaxAdvancePercentage = 0.50m,
                });
                db.SaveChanges();
            }

            // R4 — Seed de la table ChecklistItems (l'onboarding en dépend).
            if (!db.ChecklistItems.Any())
            {
                db.ChecklistItems.AddRange(
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Création du compte informatique et de l'adresse email", Category = "IT", IsRequired = true, Order = 1 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Installation du poste de travail et des logiciels", Category = "IT", IsRequired = true, Order = 2 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Création des accès aux applications RH", Category = "IT", IsRequired = true, Order = 3 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Signature du contrat de travail", Category = "Administratif", IsRequired = true, Order = 1 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Remise du badge et des documents d'identité", Category = "Administratif", IsRequired = true, Order = 2 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Ouverture du dossier RH et du compte bancaire", Category = "Administratif", IsRequired = true, Order = 3 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Présentation de l'équipe et du manager", Category = "RH", IsRequired = true, Order = 1 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Programme d'intégration et visite des locaux", Category = "RH", IsRequired = true, Order = 2 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Désignation du tuteur ou du référent", Category = "RH", IsRequired = false, Order = 3 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Briefing objectifs et feuille de route à 90 jours", Category = "Management", IsRequired = true, Order = 1 },
                    new Agirh.Domain.Entities.ChecklistItem { Id = Guid.NewGuid(), Title = "Revue hebdomadaire des premières semaines", Category = "Management", IsRequired = false, Order = 2 });
                db.SaveChanges();
            }
        }
    }
    catch (Exception ex)
    {
        // Log-Sentinel — un échec d'initialisation DB est désormais tracé dans
        // agirh-api.log (ILogger Critical) au lieu d'un crash console muet qui
        // laissait les deux fichiers vides.
        var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogCritical(ex, "Database initialization failed — exiting.");
        return 1;
    }
}

app.Run();
return 0;

public partial class Program { }
