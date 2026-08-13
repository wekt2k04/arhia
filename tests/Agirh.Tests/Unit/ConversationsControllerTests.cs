using System.Security.Claims;
using Agirh.Api.Controllers;
using Agirh.Api.Dtos;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="ConversationsController"/> + the REAL
/// <see cref="AgentConversationRepository"/> (correctif 3.x — historique RH).
///
/// The controller is instantiated directly with a constructed ClaimsPrincipal
/// and the REAL <see cref="HardStateExtractor"/> (claims NameIdentifier, Role,
/// is_active, managerId), so the identity extraction is production code — the
/// only seam mocked away is the HTTP layer. Every identifier-targeted action is
/// verified for ownership (IDOR): conversations of OTHER users are invisible
/// (404) or forbidden (403), never leaked.
/// </summary>
public class ConversationsControllerTests
{
    // --------------------------------------------------------------------- //
    // Given — deterministic harness
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal BuildPrincipal(string userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new("is_active", "true"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test-auth"));
    }

    private static ConversationsController BuildController(AppDbContext context, ClaimsPrincipal principal)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new EmployeeRepository(context),
            new LeaveRequestRepository(context),
            new PayrollProfileRepository(context),
            new SalaryAdvanceRepository(context),
            new AgentConversationRepository(context));

        var controller = new ConversationsController(
            new HardStateExtractor(), // REAL extractor — reads JWT claims
            unitOfWork,
            NullLogger<ConversationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
        return controller;
    }

    private static async Task<AgentConversation> AddConversationAsync(
        AppDbContext context,
        Guid userId,
        string title,
        DateTime updatedAt,
        int messageCount = 0)
    {
        var conversation = new AgentConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt,
        };

        for (var i = 0; i < messageCount; i++)
        {
            conversation.Messages.Add(new AgentMessage
            {
                Id = Guid.NewGuid(),
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"message-{i}",
                Timestamp = updatedAt.AddMinutes(i),
            });
        }

        context.AgentConversations.Add(conversation);
        await context.SaveChangesAsync();
        return conversation;
    }

    private static async Task<List<ConversationSummaryDto>> ActListAsync(ConversationsController controller)
    {
        var result = await controller.List(CancellationToken.None);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeAssignableTo<IEnumerable<ConversationSummaryDto>>().Subject.ToList();
    }

    // ===================================================================== //
    // 4.3.1 — Repository behavior (EF InMemory, real repository)
    // ===================================================================== //

    [Fact]
    public async Task GetRecentByUserIdAsync_Should_Filter_Order_And_Limit()
    {
        // Given 5 conversations for user A (staggered updates) and 2 for user B
        var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            await AddConversationAsync(context, userA, $"A-{i}", DateTime.UtcNow.AddHours(i));
        await AddConversationAsync(context, userB, "B-1", DateTime.UtcNow.AddHours(100));
        await AddConversationAsync(context, userB, "B-2", DateTime.UtcNow.AddHours(101));
        var repo = new AgentConversationRepository(context);

        // When listing the 3 most recent conversations of user A
        var result = (await repo.GetRecentByUserIdAsync(userA, limit: 3)).ToList();

        // Then ONLY user A's conversations are returned, ordered by UpdatedAt desc, capped at 3
        result.Should().HaveCount(3);
        result.Should().OnlyContain(c => c.UserId == userA);
        result.Select(c => c.UpdatedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetByIdWithMessagesAsync_Should_Return_Null_For_Another_Users_Conversation()
    {
        // Given a conversation owned by user B
        var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, userB, "secret", DateTime.UtcNow, messageCount: 2);
        var repo = new AgentConversationRepository(context);

        // When user A asks for it by id
        var result = await repo.GetByIdWithMessagesAsync(conversation.Id, userA);

        // Then the conversation is invisible (null) — ownership is enforced at the repository
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithMessagesAsync_Should_Return_Messages_In_Order()
    {
        // Given a conversation with 3 messages
        var context = CreateContext();
        var owner = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, owner, "historique", DateTime.UtcNow, messageCount: 3);
        var repo = new AgentConversationRepository(context);

        // When the owner asks for it with messages
        var result = await repo.GetByIdWithMessagesAsync(conversation.Id, owner);

        // Then the conversation is returned with its messages ordered by timestamp
        result.Should().NotBeNull();
        result!.Messages.Should().HaveCount(3);
        result.Messages.Select(m => m.Content).Should().ContainInOrder("message-0", "message-1", "message-2");
    }

    [Fact]
    public async Task AddAsync_And_DeleteAsync_Should_Persist_And_Remove()
    {
        // Given an empty store
        var context = CreateContext();
        var repo = new AgentConversationRepository(context);
        var owner = Guid.NewGuid();

        // When a conversation is added and saved
        var conversation = new AgentConversation { UserId = owner, Title = "nouvelle" };
        await repo.AddAsync(conversation);
        await context.SaveChangesAsync();

        // Then it is persisted…
        (await repo.GetByIdAsync(conversation.Id)).Should().NotBeNull();

        // …and when deleted and saved it is gone
        await repo.DeleteAsync(conversation);
        await context.SaveChangesAsync();
        (await repo.GetByIdAsync(conversation.Id)).Should().BeNull();
    }

    // ===================================================================== //
    // 4.3.2 — List: history limit depends on the role (3/5/7)
    // ===================================================================== //

    [Theory]
    [InlineData("Collaborator", 3)]
    [InlineData("Manager", 5)]
    [InlineData("Admin", 7)]
    public async Task List_Should_Apply_Role_History_Limit_And_Exclude_Other_Users(string role, int expectedLimit)
    {
        // Given 8 conversations for the current user + 3 conversations of other users
        var context = CreateContext();
        var currentUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        for (var i = 0; i < 8; i++)
            await AddConversationAsync(context, currentUser, $"mine-{i}", DateTime.UtcNow.AddMinutes(i));
        for (var i = 0; i < 3; i++)
            await AddConversationAsync(context, otherUser, $"theirs-{i}", DateTime.UtcNow.AddMinutes(100 + i));

        var controller = BuildController(context, BuildPrincipal(currentUser.ToString(), role));

        // When the current user lists their conversations
        var dtos = await ActListAsync(controller);

        // Then ONLY the most recent {limit} OWNED conversations are returned
        dtos.Should().HaveCount(expectedLimit);
        dtos.Should().OnlyContain(d => d.Title.StartsWith("mine-"));
        dtos.Select(d => d.UpdatedAt).Should().BeInDescendingOrder();
    }

    // ===================================================================== //
    // 4.3.3 — Messages: content, 404 for missing, 404 for other users (IDOR)
    // ===================================================================== //

    [Fact]
    public async Task Messages_Should_Return_Content_For_Owned_Conversation()
    {
        // Given an owned conversation with messages
        var context = CreateContext();
        var owner = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, owner, "discussion", DateTime.UtcNow, messageCount: 2);
        var controller = BuildController(context, BuildPrincipal(owner.ToString(), "Collaborator"));

        // When the owner reads the messages
        var result = await controller.Messages(conversation.Id, CancellationToken.None);

        // Then the messages are returned with their content, in order
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ConversationMessageDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos.Select(m => m.Content).Should().ContainInOrder("message-0", "message-1");
        dtos[0].Role.Should().Be("user");
        dtos[1].Role.Should().Be("assistant");
    }

    [Fact]
    public async Task Messages_Should_Return_NotFound_When_Conversation_Does_Not_Exist()
    {
        // Given a user with no conversation at all
        var context = CreateContext();
        var controller = BuildController(context, BuildPrincipal(Guid.NewGuid().ToString(), "Collaborator"));

        // When they ask for a random id
        var result = await controller.Messages(Guid.NewGuid(), CancellationToken.None);

        // Then 404 is returned
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Messages_Should_Return_NotFound_For_Another_Users_Conversation()
    {
        // Given a conversation owned by user B
        var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, userB, "secret", DateTime.UtcNow, messageCount: 1);
        var controller = BuildController(context, BuildPrincipal(userA.ToString(), "Collaborator"));

        // When user A reads it
        var result = await controller.Messages(conversation.Id, CancellationToken.None);

        // Then 404 — the other user's conversation is invisible (IDOR closed)
        result.Should().BeOfType<NotFoundResult>();
    }

    // ===================================================================== //
    // 4.3.4 — Delete: NoContent for owned, Forbid (403) for IDOR, 404 missing
    // ===================================================================== //

    [Fact]
    public async Task Delete_Should_Remove_Owned_Conversation_And_Return_NoContent()
    {
        // Given an owned conversation
        var context = CreateContext();
        var owner = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, owner, "à supprimer", DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(owner.ToString(), "Collaborator"));

        // When the owner deletes it
        var result = await controller.Delete(conversation.Id, CancellationToken.None);

        // Then NoContent is returned AND the row is really gone from the database
        result.Should().BeOfType<NoContentResult>();
        context.AgentConversations.Count(c => c.Id == conversation.Id).Should().Be(0);
    }

    [Fact]
    public async Task Delete_Should_Forbid_Deleting_Another_Users_Conversation_And_Leave_It_Intact()
    {
        // Given a conversation owned by user B
        var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var conversation = await AddConversationAsync(context, userB, "secret", DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(userA.ToString(), "Collaborator"));

        // When user A tries to delete it (IDOR)
        var result = await controller.Delete(conversation.Id, CancellationToken.None);

        // Then 403 Forbid is returned…
        result.Should().BeOfType<ForbidResult>();

        // …and the conversation is NOT deleted from the database
        context.AgentConversations.Count(c => c.Id == conversation.Id).Should().Be(1);
    }

    [Fact]
    public async Task Delete_Should_Return_NotFound_When_Conversation_Does_Not_Exist()
    {
        // Given a user with no conversation at all
        var context = CreateContext();
        var controller = BuildController(context, BuildPrincipal(Guid.NewGuid().ToString(), "Collaborator"));

        // When they delete a random id
        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Then 404 is returned
        result.Should().BeOfType<NotFoundResult>();
    }
}
