using System.Text;
using Arhia.Api.Auth;
using Arhia.Core.Ports;
using Arhia.Core.UseCases;
using Arhia.Infrastructure.Llm;
using Arhia.Infrastructure.Persistence;
using Arhia.Infrastructure.Persistence.Repositories;
using Arhia.Infrastructure.Rag;
using Arhia.Infrastructure.Realtime;
using Arhia.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ArhiaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ArhiaDb")));

builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
builder.Services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();

builder.Services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();

var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Configuration Jwt:Issuer manquante."),
    Audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Configuration Jwt:Audience manquante."),
    SigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Configuration Jwt:SigningKey manquante."),
    TokenLifetimeMinutes = builder.Configuration.GetValue("Jwt:TokenLifetimeMinutes", 60)
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenGenerator>();

builder.Services.AddScoped<RegisterUseCase>();
builder.Services.AddScoped<AuthenticateUseCase>();
builder.Services.AddScoped<ElevateRoleUseCase>();
builder.Services.AddScoped<CreateEmployeeRecordUseCase>();
builder.Services.AddScoped<InstantiateWorkflowUseCase>();
builder.Services.AddScoped<CheckItemUseCase>();
builder.Services.AddScoped<CloseCaseUseCase>();
builder.Services.AddScoped<ProposeTemplateUseCase>();
builder.Services.AddScoped<VerifyTemplateUseCase>();
builder.Services.AddScoped<ApproveTemplateUseCase>();
builder.Services.AddScoped<RejectTemplateUseCase>();
builder.Services.AddScoped<ArchiveCaseUseCase>();
builder.Services.AddScoped<GetNotificationsUseCase>();
builder.Services.AddScoped<GetEmployeeUseCase>();
builder.Services.AddScoped<ListEmployeesUseCase>();
builder.Services.AddScoped<GetWorkflowInstanceUseCase>();
builder.Services.AddScoped<ListWorkflowInstancesUseCase>();
builder.Services.AddScoped<ListDepartmentsUseCase>();
builder.Services.AddSingleton<SseNotificationBroadcaster>();

builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// Pipeline RAG (docs/STACK_TECHNIQUE.md #4) + orchestration conversationnelle (#5).
// Racine du depot resolue dynamiquement (pas de chemin absolu fige) pour retrouver rag/models/ et
// rag/corpus/ quel que soit le repertoire de travail depuis lequel l'Api est lancee.
var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var embeddingModelsDir = Path.Combine(repoRoot, "rag", "models", "embedding");
var rerankerModelsDir = Path.Combine(repoRoot, "rag", "models", "reranker");

builder.Services.AddSingleton<IEmbeddingPort>(_ => new OnnxEmbeddingAdapter(
    Path.Combine(embeddingModelsDir, "model_quantized.onnx"),
    Path.Combine(embeddingModelsDir, "sentencepiece.bpe.model")));

builder.Services.AddSingleton<IRerankerPort>(_ => new OnnxRerankerAdapter(
    Path.Combine(rerankerModelsDir, "model_quantized.onnx"),
    Path.Combine(rerankerModelsDir, "sentencepiece.bpe.model")));

builder.Services.AddSingleton(_ => new QdrantClient(builder.Configuration["Qdrant:Host"] ?? "localhost"));
builder.Services.AddSingleton<IVectorSearchPort>(sp =>
    new QdrantVectorSearchAdapter(sp.GetRequiredService<QdrantClient>(), sp.GetRequiredService<IEmbeddingPort>().Dimension));

builder.Services.AddSingleton(_ => XlmRobertaTokenizer.LoadFromFile(
    Path.Combine(embeddingModelsDir, "sentencepiece.bpe.model")));
builder.Services.AddSingleton<IDocumentChunkerPort>(sp =>
    new MarkdownChunkerAdapter(sp.GetRequiredService<XlmRobertaTokenizer>()));
builder.Services.AddSingleton(new Arhia.Api.Controllers.CorpusOptions(Path.Combine(repoRoot, "rag", "corpus")));
builder.Services.AddScoped<IngestCorpusUseCase>();

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(120);
});
// Modeles configurables independamment de l'URL (Ollama:RouterModel / Ollama:GeneratorModel) :
// permet de basculer vers un serveur Ollama d'entreprise (URL différente ET modèles différents,
// pas seulement une autre URL avec le même modèle) sans recompiler.
builder.Services.AddScoped<ILlmRouterPort>(sp => new OllamaRouterAdapter(
    sp.GetRequiredService<OllamaClient>(),
    builder.Configuration["Ollama:RouterModel"] ?? "phi4-mini:3.8b"));
builder.Services.AddScoped<ILlmGeneratorPort>(sp => new OllamaGeneratorAdapter(
    sp.GetRequiredService<OllamaClient>(),
    builder.Configuration["Ollama:GeneratorModel"] ?? "phi4-mini:3.8b"));

builder.Services.AddScoped<AnswerConversationUseCase>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Idempotent (no-op si le schema est deja a jour) : applique automatiquement les migrations en
// attente au demarrage. Necessaire pour qu'un `docker compose up` sur une base SQL Server
// fraiche fonctionne sans etape manuelle (`dotnet ef database update`) - et supprime cette etape
// manuelle aussi pour le developpement local courant.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ArhiaDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string FindRepoRoot(string start)
{
    var directory = start;
    while (directory is not null && !File.Exists(Path.Combine(directory, "Arhia.sln")))
    {
        directory = Directory.GetParent(directory)?.FullName;
    }

    return directory ?? throw new InvalidOperationException("Arhia.sln introuvable en remontant depuis " + start);
}
