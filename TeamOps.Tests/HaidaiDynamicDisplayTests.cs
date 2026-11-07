using Dapper;
using TeamOps.Data.Repositories;
using TeamOps.UI.Services;
using Xunit;

namespace TeamOps.Tests;

public sealed class HaidaiDynamicDisplayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void Board_ContainsEveryActiveArea_InConfiguredOrder(int areaCount)
    {
        using var context = new DatabaseTestContext();
        var sectorId = AddSector(context, $"DISPLAY-{areaCount}");
        using (var conn = context.Factory.CreateOpenConnection())
        {
            for (var index = 0; index < areaCount; index++)
            {
                var sortOrder = areaCount - index;
                conn.Execute(@"
                    INSERT INTO HaidaiAreas (SectorId, Code, NamePt, NameJp, SortOrder, IsActive)
                    VALUES (@sectorId, @code, @name, @name, @sortOrder, 1);",
                    new { sectorId, code = $"A{index + 1}", name = $"Area {index + 1}", sortOrder });
            }
        }

        var board = new HaidaiModuleService(context.Factory)
            .GetBoard(new DateTime(2026, 2, 1), 1, sectorId, new[] { sectorId });

        Assert.Equal(areaCount, board.Areas.Count);
        Assert.Equal(board.Areas.OrderBy(area => area.SortOrder).Select(area => area.Id), board.Areas.Select(area => area.Id));
    }

    [Fact]
    public void Board_GroupsActiveLocations_AndDropsDisabledAreaAndLocation()
    {
        using var context = new DatabaseTestContext();
        var sectorId = AddSector(context, "DISPLAY-GROUP");
        using (var conn = context.Factory.CreateOpenConnection())
        {
            var visibleArea = conn.ExecuteScalar<int>(@"
                INSERT INTO HaidaiAreas (SectorId, Code, NamePt, NameJp, SortOrder, IsActive)
                VALUES (@sectorId, 'VISIBLE', 'Area visivel', 'Visible', 2, 1);
                SELECT last_insert_rowid();", new { sectorId });
            var disabledArea = conn.ExecuteScalar<int>(@"
                INSERT INTO HaidaiAreas (SectorId, Code, NamePt, NameJp, SortOrder, IsActive)
                VALUES (@sectorId, 'OFF', 'Area desativada', 'Off', 1, 0);
                SELECT last_insert_rowid();", new { sectorId });
            conn.Execute(@"
                INSERT INTO Locals (NamePt, NameJp, ShortCode, SectorId, HaidaiAreaId, SortOrder, IsActive)
                VALUES
                    ('Local 2', 'L2', 'L2', @sectorId, @visibleArea, 2, 1),
                    ('Local 1', 'L1', 'L1', @sectorId, @visibleArea, 1, 1),
                    ('Local inativo', 'Off', 'OFF', @sectorId, @visibleArea, 0, 0),
                    ('Local area off', 'Off', 'AOFF', @sectorId, @disabledArea, 0, 1);",
                new { sectorId, visibleArea, disabledArea });
        }

        var board = new HaidaiModuleService(context.Factory)
            .GetBoard(new DateTime(2026, 2, 1), 1, sectorId, new[] { sectorId });

        var area = Assert.Single(board.Areas);
        Assert.Equal("VISIBLE", area.Code);
        Assert.Equal(new[] { "L1", "L2" }, area.Locations.Select(location => location.ShortCode));
    }

    [Theory]
    [InlineData(2025, 2, 28)]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 7, 31)]
    public void MonthlyPlan_UsesActualNumberOfDays(int year, int month, int expectedDays)
    {
        using var context = new DatabaseTestContext();
        var sectorId = AddSector(context, $"MONTH-{year}-{month}");

        var plan = new HaidaiModuleService(context.Factory)
            .GetMonthlyPlan(year, month, 1, sectorId, new[] { sectorId });

        Assert.Equal(expectedDays, plan.Days.Count);
        Assert.Equal(expectedDays, plan.Days[^1]);
    }

    [Fact]
    public void DisplaySettings_ArePersistentAndValidated()
    {
        using var context = new DatabaseTestContext();
        var sectorId = AddSector(context, "DISPLAY-SETTINGS");
        var service = new HaidaiModuleService(context.Factory);

        service.SaveDisplaySettings(new HaidaiDisplaySettings(sectorId, 1920, 1080, 25));
        var saved = service.GetDisplaySettings(sectorId);

        Assert.Equal(1920, saved.ReferenceWidth);
        Assert.Equal(1080, saved.ReferenceHeight);
        Assert.Equal(25, saved.RotationSeconds);
        Assert.Throws<InvalidOperationException>(() =>
            service.SaveDisplaySettings(new HaidaiDisplaySettings(sectorId, 800, 600, 2)));
    }

    [Theory]
    [InlineData(3840, 2160)]
    [InlineData(1920, 1080)]
    public void Renderer_IsOfflineResponsiveAndContainsEveryArea(int width, int height)
    {
        var longName = "Área com nome muito longo & caracteres <dinâmicos> para validar escape e legibilidade";
        var areas = Enumerable.Range(1, 10)
            .Select(index => new HaidaiDisplayArea(
                index,
                $"A{index:00}",
                index == 1 ? longName : $"Area {index}",
                string.Empty,
                index,
                new[]
                {
                    new HaidaiDisplayLocation(index, $"Local {index}", $"L{index}", 0, Array.Empty<HaidaiBoardRow>())
                }))
            .ToList();
        var board = EmptyBoard(areas);

        var html = HaidaiHtmlRenderer.Render(
            "Setor de teste",
            "Turno 1",
            "outro-turno.html",
            board,
            new HaidaiDisplaySettings(1, width, height, 7),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.StartsWith("<!doctype html>", html.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("setInterval", html);
        Assert.Contains("7000", html);
        Assert.Contains("Área com nome muito longo &amp; caracteres &lt;dinâmicos&gt;", html);
        foreach (var area in areas) Assert.Contains(area.Code, html);
    }

    private static int AddSector(DatabaseTestContext context, string code)
    {
        var organizations = new OrganizationRepository(context.Factory);
        var unit = organizations.GetByCode("5552")!;
        return organizations.AddSector(code, code, code, unit.Id).Id;
    }

    private static HaidaiBoardPayload EmptyBoard(IReadOnlyList<HaidaiDisplayArea> areas) => new(
        "2026-11-07",
        1,
        1,
        new HaidaiSummary(0, 0, 0, 0, 0, 0, 0, 0),
        Array.Empty<HaidaiGroupBlock>(),
        Array.Empty<HaidaiAreaTotal>(),
        areas);
}
