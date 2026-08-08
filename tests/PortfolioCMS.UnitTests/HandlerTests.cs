using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Application.Features.Projects;
using PortfolioCMS.Application.Features.Sections;
using PortfolioCMS.Domain.Entities;
using PortfolioCMS.Domain.Enums;
using PortfolioCMS.Infrastructure.Persistence;

namespace PortfolioCMS.UnitTests;

// ── Test helpers ──────────────────────────────────────────────────────────────

public static class DbHelper
{
    /// <summary>Creates a fresh in-memory DbContext for each test.</summary>
    public static AppDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION HANDLER TESTS
// ═══════════════════════════════════════════════════════════════════════════════

public class GetAllSectionsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSectionsOrderedByDisplayOrder()
    {
        // Arrange
        using var db = DbHelper.CreateDb(nameof(GetAllSectionsHandlerTests));
        db.Sections.AddRange(
            new PortfolioSection { Type = SectionType.Contact, Title = "Contact", Content = "c", DisplayOrder = 3 },
            new PortfolioSection { Type = SectionType.Hero,    Title = "Hero",    Content = "h", DisplayOrder = 1 },
            new PortfolioSection { Type = SectionType.About,   Title = "About",   Content = "a", DisplayOrder = 2 }
        );
        await db.SaveChangesAsync();

        var handler = new GetAllSectionsHandler(db);

        // Act
        var result = await handler.Handle(new GetAllSectionsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].Type.Should().Be("Hero");
        result[1].Type.Should().Be("About");
        result[2].Type.Should().Be("Contact");
    }
}

public class UpdateSectionHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesSection_AndReturnsDto()
    {
        // Arrange
        using var db = DbHelper.CreateDb(nameof(UpdateSectionHandlerTests));
        var section = new PortfolioSection
        {
            Type = SectionType.Hero, Title = "Old Title", Content = "old content",
            DisplayOrder = 1, BackgroundColor = "#000000", TextColor = "#ffffff"
        };
        db.Sections.Add(section);
        await db.SaveChangesAsync();

        var mockAudit  = new Mock<IAuditService>();
        var mockNotify = new Mock<IPortfolioNotificationService>();
        mockAudit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockNotify.Setup(n => n.NotifyContentUpdatedAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateSectionHandler(db, mockAudit.Object, mockNotify.Object);
        var request  = new UpdateSectionRequest("New Title", "New Sub", "new content",
            true, 1, "#ffffff", "#000000");

        // Act
        var result = await handler.Handle(
            new UpdateSectionCommand(section.Id, request), CancellationToken.None);

        // Assert
        result.Title.Should().Be("New Title");
        result.Content.Should().Be("new content");
        mockAudit.Verify(a => a.LogAsync("UpdateSection", "PortfolioSection",
            section.Id, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        mockNotify.Verify(n => n.NotifyContentUpdatedAsync("Hero", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsKeyNotFound_WhenSectionMissing()
    {
        using var db = DbHelper.CreateDb(nameof(UpdateSectionHandlerTests) + "_missing");
        var mockAudit  = new Mock<IAuditService>();
        var mockNotify = new Mock<IPortfolioNotificationService>();
        var handler    = new UpdateSectionHandler(db, mockAudit.Object, mockNotify.Object);
        var request    = new UpdateSectionRequest("T", null, "c", true, 1, "#fff", "#000");

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new UpdateSectionCommand(999, request), CancellationToken.None));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PROJECT HANDLER TESTS
// ═══════════════════════════════════════════════════════════════════════════════

public class GetAllProjectsHandlerTests
{
    [Fact]
    public async Task Handle_VisibleOnly_FiltersCorrectly()
    {
        using var db = DbHelper.CreateDb(nameof(GetAllProjectsHandlerTests));
        db.Projects.AddRange(
            new ProjectCard { Title = "Visible",  Description = "d", TechStack = "C#", IsVisible = true,  DisplayOrder = 1 },
            new ProjectCard { Title = "Hidden",   Description = "d", TechStack = "C#", IsVisible = false, DisplayOrder = 2 }
        );
        await db.SaveChangesAsync();

        var handler = new GetAllProjectsHandler(db);

        // Act — visible only
        var visible = await handler.Handle(new GetAllProjectsQuery(true), CancellationToken.None);
        visible.Should().HaveCount(1);
        visible[0].Title.Should().Be("Visible");

        // Act — all
        var all = await handler.Handle(new GetAllProjectsQuery(false), CancellationToken.None);
        all.Should().HaveCount(2);
    }
}

public class CreateProjectHandlerTests
{
    [Fact]
    public async Task Handle_CreatesProject_WithCorrectFields()
    {
        using var db = DbHelper.CreateDb(nameof(CreateProjectHandlerTests));
        var mockAudit = new Mock<IAuditService>();
        mockAudit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateProjectHandler(db, mockAudit.Object);
        var request = new CreateProjectRequest("Portfolio CMS", "A great project",
            "C#,.NET", "https://github.com/test", null, 1);

        // Act
        var result = await handler.Handle(new CreateProjectCommand(request), CancellationToken.None);

        // Assert
        result.Title.Should().Be("Portfolio CMS");
        result.TechStack.Should().Be("C#,.NET");
        result.IsVisible.Should().BeTrue();
        db.Projects.Should().HaveCount(1);
    }
}

public class DeleteProjectHandlerTests
{
    [Fact]
    public async Task Handle_DeletesProject_Successfully()
    {
        using var db = DbHelper.CreateDb(nameof(DeleteProjectHandlerTests));
        var project = new ProjectCard { Title = "ToDelete", Description = "d", TechStack = "C#" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var mockAudit = new Mock<IAuditService>();
        mockAudit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeleteProjectHandler(db, mockAudit.Object);

        // Act
        var result = await handler.Handle(new DeleteProjectCommand(project.Id), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        db.Projects.Should().BeEmpty();
    }
}
