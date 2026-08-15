using System.Text;
using Agirh.Api.Auth;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using Agirh.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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
builder.Services.AddScoped<ProposerTemplateUseCase>();
builder.Services.AddScoped<VerifierTemplateUseCase>();
builder.Services.AddScoped<ApprouverTemplateUseCase>();
builder.Services.AddScoped<RejeterTemplateUseCase>();
builder.Services.AddScoped<ArchiverDossierUseCase>();

builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

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
