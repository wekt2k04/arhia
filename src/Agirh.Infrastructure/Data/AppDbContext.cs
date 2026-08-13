using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Agirh.Domain.Entities;

namespace Agirh.Infrastructure.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Convertisseur byte[] (float32 LE) <-> chaîne JSON "[f1,f2,...]".
    // Requis par le type vector(768) de SQL Server 2025, qui n'accepte que des
    // représentations varchar/nvarchar/json et jamais varbinary. Le round-trip
    // est exact (System.Text.Json est culture-invariant) et préserve la dimension
    // (768 pour embeddinggemma) : ni troncature, ni padding.
    private static readonly ValueConverter<byte[], string> EmbeddingConverter = new(
        bytes => bytes == null ? null! : SerializeEmbedding(bytes),
        json => json == null ? Array.Empty<byte>() : DeserializeEmbedding(json));

    // Comparateur structurel : byte[] est une collection ; sans comparateur EF
    // compare par référence et émet un warning de validation de modèle (10620).
    private static readonly ValueComparer<byte[]> EmbeddingComparer = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        v => v == null ? 0 : v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
        v => v == null ? Array.Empty<byte>() : v.ToArray());

    internal static string SerializeEmbedding(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length % 4 != 0)
            throw new ArgumentException("Embedding doit contenir des float32 (longueur multiple de 4).", nameof(bytes));

        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return JsonSerializer.Serialize(floats);
    }

    private static byte[] DeserializeEmbedding(string json)
    {
        var floats = JsonSerializer.Deserialize<float[]>(json);
        if (floats is null)
            return Array.Empty<byte>();

        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<PayrollProfile> PayrollProfiles => Set<PayrollProfile>();
    public DbSet<SalaryAdvanceRequest> SalaryAdvanceRequests => Set<SalaryAdvanceRequest>();
    public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
            e.Property(x => x.LeaveBalance).HasColumnType("decimal(18,2)");
            e.Property(x => x.CompteEpargneTemps).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Manager).WithMany(x => x.Subordinates).HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<LeaveRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
            e.Property(x => x.Type).HasMaxLength(50).IsRequired();
            e.Property(x => x.DaysRequested).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.ChunkText).IsRequired();
            e.Property(x => x.SourceFile).HasMaxLength(500);
            e.Property(x => x.Embedding)
                .HasColumnType("vector(768)")
                .HasConversion(EmbeddingConverter, EmbeddingComparer);
            // Anti-doublon (R2) : un même chunk (fichier + index) ne peut exister
            // qu'une seule fois — l'ingestion étant un append, un re-run sans
            // DELETE préalable échouera proprement au lieu de dupliquer.
            e.HasIndex(x => new { x.SourceFile, x.ChunkIndex }).IsUnique();
        });

        modelBuilder.Entity<ChecklistItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<PayrollProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NetSalary).HasColumnType("decimal(18,2)");
            e.Property(x => x.Iban).HasMaxLength(34).IsRequired();
            e.Property(x => x.MaxAdvancePercentage).HasColumnType("decimal(5,4)");
            e.HasOne(x => x.Employee).WithOne().HasForeignKey<PayrollProfile>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EmployeeId).IsUnique();
        });

        modelBuilder.Entity<SalaryAdvanceRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AmountRequested).HasColumnType("decimal(18,2)");
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            // Anti-TOCTOU : un seul "Pending" par employé, garanti par la base
            // (SQL Server convention de nommage : IX_{Entite}_{Propriete}).
            e.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("Status = 'Pending'");
        });

        modelBuilder.Entity<AgentConversation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => x.UserId);
            e.HasMany(x => x.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.ToolCalled).HasMaxLength(100);
            e.HasIndex(x => x.ConversationId);
        });
    }
}
