using System.Text;
using Agirh.Api.Auth;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Infrastructure.Llm;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using Agirh.Infrastructure.Rag;
using Agirh.Infrastructure.Realtime;
using Agirh.Infrastructure.Security;
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

builder.Services.AddDbContext<AgirhDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AgirhDb")));

builder.Services.AddScoped<IPoleRepository, PoleRepository>();
builder.Services.AddScoped<ICompteUtilisateurRepository, CompteUtilisateurRepository>();
builder.Services.AddScoped<ICollaborateurRepository, CollaborateurRepository>();
builder.Services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
builder.Services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();

builder.Services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();

var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Configuration Jwt:Issuer manquante."),
    Audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Configuration Jwt:Audience manquante."),
    SigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Configuration Jwt:SigningKey manquante."),
    DureeValiditeMinutes = builder.Configuration.GetValue("Jwt:DureeValiditeMinutes", 60)
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenGenerator>();

builder.Services.AddScoped<InscrireUseCase>();
builder.Services.AddScoped<AuthentifierUseCase>();
builder.Services.AddScoped<ElevRoleUseCase>();
builder.Services.AddScoped<CreerFicheCollaborateurUseCase>();
builder.Services.AddScoped<InstancierWorkflowUseCase>();
builder.Services.AddScoped<CocherItemUseCase>();
builder.Services.AddScoped<CloturerDossierUseCase>();
builder.Services.AddScoped<ProposerTemplateUseCase>();
builder.Services.AddScoped<VerifierTemplateUseCase>();
builder.Services.AddScoped<ApprouverTemplateUseCase>();
builder.Services.AddScoped<RejeterTemplateUseCase>();
builder.Services.AddScoped<ArchiverDossierUseCase>();
builder.Services.AddScoped<ObtenirNotificationsUseCase>();
builder.Services.AddSingleton<SseNotificationBroadcaster>();

builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// Pipeline RAG (STACK_TECHNIQUE.md #4) + orchestration conversationnelle (#5).
// Racine du depot resolue dynamiquement (pas de chemin absolu fige) pour retrouver models/ et corpus/
// quel que soit le repertoire de travail depuis lequel l'Api est lancee.
var racineDepot = TrouverRacineDepot(AppContext.BaseDirectory);
var modelesEmbeddingDir = Path.Combine(racineDepot, "models", "embedding");
var modelesRerankerDir = Path.Combine(racineDepot, "models", "reranker");

builder.Services.AddSingleton<IEmbeddingPort>(_ => new OnnxEmbeddingAdapter(
    Path.Combine(modelesEmbeddingDir, "model_quantized.onnx"),
    Path.Combine(modelesEmbeddingDir, "sentencepiece.bpe.model")));

builder.Services.AddSingleton<IRerankerPort>(_ => new OnnxRerankerAdapter(
    Path.Combine(modelesRerankerDir, "model_quantized.onnx"),
    Path.Combine(modelesRerankerDir, "sentencepiece.bpe.model")));

builder.Services.AddSingleton(_ => new QdrantClient(builder.Configuration["Qdrant:Host"] ?? "localhost"));
builder.Services.AddSingleton<IVectorSearchPort>(sp =>
    new QdrantVectorSearchAdapter(sp.GetRequiredService<QdrantClient>(), sp.GetRequiredService<IEmbeddingPort>().Dimension));

builder.Services.AddSingleton(_ => XlmRobertaTokenizer.ChargerDepuisFichier(
    Path.Combine(modelesEmbeddingDir, "sentencepiece.bpe.model")));
builder.Services.AddSingleton<IDocumentChunkerPort>(sp =>
    new MarkdownChunkerAdapter(sp.GetRequiredService<XlmRobertaTokenizer>()));
builder.Services.AddSingleton(new Agirh.Api.Controllers.CorpusOptions(Path.Combine(racineDepot, "corpus")));
builder.Services.AddScoped<IngererCorpusUseCase>();

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ILlmRouterPort, OllamaRouterAdapter>();
builder.Services.AddScoped<ILlmGeneratorPort, OllamaGeneratorAdapter>();

builder.Services.AddScoped<RepondreConversationUseCase>();

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

static string TrouverRacineDepot(string depart)
{
    var repertoire = depart;
    while (repertoire is not null && !File.Exists(Path.Combine(repertoire, "Agirh.sln")))
    {
        repertoire = Directory.GetParent(repertoire)?.FullName;
    }

    return repertoire ?? throw new InvalidOperationException("Agirh.sln introuvable en remontant depuis " + depart);
}
