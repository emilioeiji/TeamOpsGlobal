using System.Text;
using TeamOps.UI.Services;

var outputPath = args.FirstOrDefault()
    ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "haidai-validation", "haidai-dynamic-demo.html");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

var allRows = new List<HaidaiBoardRow>();
var areas = Enumerable.Range(1, 11).Select(areaIndex =>
{
    var locationCount = areaIndex % 3 == 0 ? 3 : areaIndex % 2 == 0 ? 2 : 1;
    var locations = Enumerable.Range(1, locationCount).Select(locationIndex =>
    {
        var operatorCount = (areaIndex + locationIndex) % 5;
        var rows = Enumerable.Range(1, operatorCount)
            .Select(operatorIndex => CreateRow(areaIndex, locationIndex, operatorIndex))
            .ToList();
        allRows.AddRange(rows);
        return new HaidaiDisplayLocation(
            areaIndex * 100 + locationIndex,
            locationIndex == 1 && areaIndex == 1
                ? "Local com denominação operacional extensa para teste"
                : $"Local {areaIndex}.{locationIndex}",
            $"L{areaIndex}{locationIndex}",
            locationIndex,
            rows);
    }).ToList();

    return new HaidaiDisplayArea(
        areaIndex,
        $"AREA-{areaIndex:00}",
        areaIndex == 1
            ? "Área de preparação e acabamento com nome longo"
            : $"Área operacional {areaIndex}",
        $"エリア {areaIndex}",
        areaIndex,
        locations);
}).ToList();

var board = new HaidaiBoardPayload(
    "2026-11-07",
    1,
    1,
    new HaidaiSummary(allRows.Count, allRows.Count, 1, 1, 0, 0, allRows.Count(row => row.IsTrainee), 3),
    new[] { new HaidaiGroupBlock(1, "Grupo demonstração", allRows.Count, allRows) },
    areas.Select(area => new HaidaiAreaTotal(area.Code, area.OperatorCount)).ToList(),
    areas);

var html = HaidaiHtmlRenderer.Render(
    "Setor Multissetor — Demonstração 4K",
    "Turno diurno",
    "#",
    board,
    new HaidaiDisplaySettings(1, 3840, 2160, 8),
    Path.Combine(Directory.GetCurrentDirectory(), "TeamOps.UI", "ui", "presence", "assets"));

File.WriteAllText(outputPath, html, new UTF8Encoding(false));
Console.WriteLine(Path.GetFullPath(outputPath));

static HaidaiBoardRow CreateRow(int areaIndex, int locationIndex, int operatorIndex)
{
    var code = $"FJ{areaIndex:00}{locationIndex:00}{operatorIndex:00}";
    var trainee = operatorIndex == 2;
    return new HaidaiBoardRow(
        code,
        operatorIndex == 3 ? "OPERADOR COM NOME EXTREMAMENTE LONGO" : $"Operador {areaIndex}.{locationIndex}.{operatorIndex}",
        $"作業者 {operatorIndex}",
        1,
        "Grupo demonstração",
        false,
        operatorIndex == 1 && areaIndex % 4 == 0,
        operatorIndex == 1 && areaIndex % 4 == 0 ? "GL" : "OP",
        areaIndex * 100 + locationIndex,
        $"Local {areaIndex}.{locationIndex}",
        $"L{areaIndex}{locationIndex}",
        1,
        areaIndex * 100 + locationIndex,
        $"Local {areaIndex}.{locationIndex}",
        $"L{areaIndex}{locationIndex}",
        1,
        $"L{areaIndex}{locationIndex}",
        $"L{areaIndex}{locationIndex}",
        operatorIndex is 1 or 2 ? $"P{areaIndex}" : string.Empty,
        trainee,
        trainee ? $"FJ{areaIndex:00}{locationIndex:00}01" : string.Empty,
        trainee ? $"Operador {areaIndex}.{locationIndex}.1" : string.Empty,
        true,
        true,
        false,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        "Escalado",
        string.Empty,
        0,
        Array.Empty<HaidaiBoardMovement>());
}
