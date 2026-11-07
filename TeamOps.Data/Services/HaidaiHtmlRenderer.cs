using System.Globalization;
using System.Text;

namespace TeamOps.UI.Services;

public sealed class HaidaiDisplaySettings
{
    public HaidaiDisplaySettings()
    {
    }

    public HaidaiDisplaySettings(int sectorId, int referenceWidth, int referenceHeight, int rotationSeconds)
    {
        SectorId = sectorId;
        ReferenceWidth = referenceWidth;
        ReferenceHeight = referenceHeight;
        RotationSeconds = rotationSeconds;
    }

    public int SectorId { get; set; }
    public int ReferenceWidth { get; set; }
    public int ReferenceHeight { get; set; }
    public int RotationSeconds { get; set; }

    public static HaidaiDisplaySettings Default(int sectorId) => new(sectorId, 3840, 2160, 12);
}

public static class HaidaiHtmlRenderer
{
    public static string Render(
        string sectorName,
        string shiftName,
        string otherFileName,
        HaidaiBoardPayload board,
        HaidaiDisplaySettings settings,
        string assetRoot)
    {
        var allRows = board.Groups.SelectMany(group => group.Rows).ToList();
        var leaders = allRows
            .Where(row => row.IsLeader && row.ExceptionMotiveId is not (1 or 2))
            .OrderBy(row => LeaderRank(row.ProfileCode))
            .ThenBy(row => row.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var trainees = board.Areas
            .SelectMany(area => area.Locations)
            .SelectMany(location => location.Operators)
            .Where(row => row.IsTrainee || !string.IsNullOrWhiteSpace(row.TrainerCodigoFJ))
            .DistinctBy(row => row.CodigoFJ, StringComparer.OrdinalIgnoreCase)
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var areasMarkup = board.Areas.Count == 0
            ? "<article class=\"empty-state\"><strong>Nenhuma área ativa</strong><span>Cadastre ou ative áreas e locais nas configurações do TeamOps.</span></article>"
            : string.Join(Environment.NewLine, board.Areas.Select(area => BuildArea(area, assetRoot)));
        var leadersMarkup = leaders.Count == 0
            ? "<div class=\"empty-compact\">Nenhum líder presente</div>"
            : string.Join(Environment.NewLine, leaders.Select(BuildLeader));
        var trainingMarkup = trainees.Count == 0
            ? "<div class=\"empty-compact\">Sem treinamento neste turno</div>"
            : string.Join(Environment.NewLine, trainees.Select(BuildTraining));

        var ratio = settings.ReferenceWidth / (double)settings.ReferenceHeight;
        var rotationMilliseconds = settings.RotationSeconds * 1000;
        var title = $"Haidai {sectorName} - {shiftName}";

        return $$$"""
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>{{E(title)}}</title>
<style>
:root{color-scheme:light;--navy:#082c45;--blue:#0c5879;--teal:#0c796a;--ink:#102536;--muted:#587080;--line:#c9d8e2;--paper:#fff;--soft:#edf4f7;--warn:#a65f00;--danger:#b02736;--ratio:{{{ratio.ToString("0.####", CultureInfo.InvariantCulture)}}};--scale:min(1vw,1.78vh)}
*{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#dfe9ee;color:var(--ink);font-family:"Segoe UI","Yu Gothic UI",Arial,sans-serif}body{padding:clamp(8px,calc(var(--scale)*.65),26px)}
.tv{height:100%;display:grid;grid-template-rows:auto minmax(0,1fr) auto;gap:clamp(7px,calc(var(--scale)*.55),22px)}
.top{display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;gap:2vw;padding:clamp(10px,calc(var(--scale)*.75),30px) clamp(14px,calc(var(--scale)*1.15),46px);border-radius:clamp(10px,calc(var(--scale)*.6),24px);background:linear-gradient(125deg,var(--navy),var(--blue));color:#fff;box-shadow:0 .5vh 2vh #082c4528}
.identity{min-width:0}.eyebrow{font-size:clamp(11px,calc(var(--scale)*.62),25px);font-weight:800;letter-spacing:.12em;text-transform:uppercase;color:#bfe9f0}.top h1{margin:.15em 0 0;font-size:clamp(24px,calc(var(--scale)*2.05),82px);line-height:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.meta{display:flex;align-items:center;gap:clamp(10px,1vw,38px)}.meta-item{display:grid;gap:.1em;text-align:right}.meta-item span{font-size:clamp(10px,calc(var(--scale)*.52),21px);color:#c9e2eb;text-transform:uppercase;font-weight:700;letter-spacing:.08em}.meta-item strong{font-size:clamp(16px,calc(var(--scale)*1.05),42px)}.switch{display:inline-flex;align-items:center;min-height:2.5em;padding:0 1em;border-radius:999px;background:#ffffff18;border:1px solid #ffffff4d;color:#fff;text-decoration:none;font-size:clamp(11px,calc(var(--scale)*.62),25px);font-weight:800}
.content{min-height:0;display:grid;grid-template-columns:minmax(0,1fr) clamp(250px,17vw,650px);gap:clamp(7px,calc(var(--scale)*.55),22px)}.area-panel,.side-card,.training{min-height:0;border:1px solid var(--line);border-radius:clamp(10px,calc(var(--scale)*.6),24px);background:var(--paper);box-shadow:0 .35vh 1.4vh #16344818;overflow:hidden}.area-panel{position:relative;padding:clamp(8px,calc(var(--scale)*.55),22px)}.pages{height:100%;position:relative}.area-page{position:absolute;inset:0;display:grid;gap:clamp(6px,calc(var(--scale)*.42),17px);opacity:0;pointer-events:none;transition:opacity .35s ease}.area-page.active{opacity:1;pointer-events:auto}
.area-card{min-width:0;min-height:0;display:grid;grid-template-rows:auto minmax(0,1fr);border:2px solid #b7ccd8;border-radius:clamp(9px,calc(var(--scale)*.5),20px);background:linear-gradient(180deg,#f8fbfc,#edf4f7);overflow:hidden}.area-head{display:flex;align-items:center;justify-content:space-between;gap:.5vw;padding:clamp(7px,calc(var(--scale)*.44),18px) clamp(9px,calc(var(--scale)*.55),22px);background:#dcebf1;border-bottom:1px solid #bdd0db}.area-title{min-width:0}.area-title strong{display:block;font-size:clamp(16px,calc(var(--scale)*1.05),42px);line-height:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--navy)}.area-title span{display:block;margin-top:.2em;font-size:clamp(9px,calc(var(--scale)*.5),20px);font-weight:700;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.count{display:grid;place-items:center;min-width:2.05em;height:2.05em;padding:0 .45em;border-radius:999px;background:var(--blue);color:#fff;font-size:clamp(13px,calc(var(--scale)*.8),32px);font-weight:900}.locations{min-height:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,240px),1fr));gap:clamp(5px,calc(var(--scale)*.32),13px);padding:clamp(6px,calc(var(--scale)*.4),16px);overflow:hidden}.location{min-width:0;min-height:0;display:grid;grid-template-rows:auto minmax(0,1fr);gap:.25em;padding:clamp(5px,calc(var(--scale)*.3),12px);border:1px solid #c6d7e0;border-radius:clamp(7px,calc(var(--scale)*.38),15px);background:#fff}.location-head{display:flex;justify-content:space-between;gap:.4em;font-size:clamp(10px,calc(var(--scale)*.57),23px);font-weight:900;color:var(--blue)}.operator-grid{min-height:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,150px),1fr));align-content:start;gap:clamp(4px,calc(var(--scale)*.24),10px);overflow:hidden}.operator{min-width:0;display:grid;grid-template-columns:auto minmax(0,1fr);align-items:center;gap:clamp(5px,calc(var(--scale)*.3),12px);padding:clamp(4px,calc(var(--scale)*.25),10px);border:1px solid #d4e0e6;border-radius:clamp(6px,calc(var(--scale)*.32),13px);background:#f9fbfc}.operator.trainee{background:#fff4d7;border-color:#e9c56e}.operator.leader{background:#e6f3f5;border-color:#9bc6cf}.photo{width:clamp(30px,calc(var(--scale)*1.75),70px);height:clamp(30px,calc(var(--scale)*1.75),70px);border-radius:50%;object-fit:cover;background:#dce6eb}.operator-copy{min-width:0}.operator-name{display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:2;overflow:hidden;font-size:clamp(11px,calc(var(--scale)*.7),28px);font-weight:900;line-height:1.05;overflow-wrap:anywhere}.operator-note{margin-top:.15em;font-size:clamp(8px,calc(var(--scale)*.43),17px);font-weight:700;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.location-empty{display:grid;place-items:center;color:#78909d;font-size:clamp(10px,calc(var(--scale)*.55),22px);font-weight:700}
.side{min-height:0;display:grid;grid-template-rows:auto minmax(0,1fr);gap:clamp(7px,calc(var(--scale)*.55),22px)}.metrics{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:clamp(5px,calc(var(--scale)*.35),14px);padding:clamp(7px,calc(var(--scale)*.5),20px)}.metric{padding:clamp(6px,calc(var(--scale)*.4),16px);border-radius:clamp(7px,calc(var(--scale)*.38),15px);background:var(--soft)}.metric strong{display:block;color:var(--blue);font-size:clamp(22px,calc(var(--scale)*1.7),68px);line-height:.95}.metric span{display:block;margin-top:.2em;color:var(--muted);font-size:clamp(9px,calc(var(--scale)*.48),19px);font-weight:800;text-transform:uppercase}.leaders{display:grid;grid-template-rows:auto minmax(0,1fr);padding:clamp(8px,calc(var(--scale)*.55),22px)}.panel-title{margin:0 0 .5em;font-size:clamp(15px,calc(var(--scale)*.95),38px)}.leader-list{min-height:0;display:grid;align-content:start;gap:clamp(4px,calc(var(--scale)*.28),11px);overflow:hidden}.leader{padding:clamp(6px,calc(var(--scale)*.38),15px);border:1px solid var(--line);border-radius:clamp(6px,calc(var(--scale)*.35),14px);background:#f8fbfc}.leader strong{display:block;font-size:clamp(11px,calc(var(--scale)*.68),27px);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.leader span{display:block;color:var(--muted);font-size:clamp(8px,calc(var(--scale)*.46),18px);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.role{display:inline-grid;place-items:center;min-width:2.6em;margin-right:.45em;padding:.15em .4em;border-radius:999px;background:#cce6e1;color:#075f53;font-size:.8em}.bottom{min-height:0;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:1vw;align-items:center;padding:clamp(7px,calc(var(--scale)*.48),19px) clamp(10px,calc(var(--scale)*.65),26px)}.training-list{display:flex;gap:.5vw;min-width:0;overflow:hidden}.training-item{min-width:0;max-width:24vw;padding:.35em .7em;border-radius:999px;background:#fff3d1;border:1px solid #e4c46e;font-size:clamp(9px,calc(var(--scale)*.52),21px);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.page-control{display:none;align-items:center;gap:.55em;font-size:clamp(11px,calc(var(--scale)*.62),25px);font-weight:900}.page-control.visible{display:flex}.page-control button{width:2.2em;height:2.2em;border:0;border-radius:999px;background:var(--navy);color:#fff;font:inherit;cursor:pointer}.empty-state,.empty-compact{display:grid;place-items:center;text-align:center;color:var(--muted)}.empty-state{height:100%;gap:.4em}.empty-state strong{font-size:clamp(20px,2vw,64px)}.empty-state span{font-size:clamp(12px,1vw,32px)}
@media(max-aspect-ratio:4/3){.content{grid-template-columns:minmax(0,1fr) 27vw}.top h1{font-size:clamp(22px,3vw,54px)}}
@media(prefers-reduced-motion:reduce){.area-page{transition:none}}
</style>
</head>
<body>
<main class="tv">
 <header class="top"><div class="identity"><span class="eyebrow">Haidai • distribuição operacional</span><h1>{{{E(sectorName)}}} — {{{E(shiftName)}}}</h1></div><div class="meta"><div class="meta-item"><span>Data</span><strong>{{{E(board.DateIso)}}}</strong></div><a class="switch" href="{{{E(otherFileName)}}}">Outro turno</a></div></header>
 <section class="content"><section class="area-panel"><div id="pages" class="pages"></div><div id="areaSource" hidden>{{{areasMarkup}}}</div></section><aside class="side"><section class="side-card metrics">{{{Metric("Escalados", board.Summary.AssignedCount)}}}{{{Metric("Áreas", board.Areas.Count)}}}{{{Metric("Treino", trainees.Count)}}}{{{Metric("Faltas", board.Summary.FaltaCount)}}}{{{Metric("Yukyu", board.Summary.YukyuCount)}}}{{{Metric("Duplas", board.Summary.PairCount)}}}</section><section class="side-card leaders"><h2 class="panel-title">Liderança presente</h2><div class="leader-list">{{{leadersMarkup}}}</div></section></aside></section>
 <footer class="training bottom"><div class="training-list"><strong class="panel-title">Treinamentos</strong>{{{trainingMarkup}}}</div><div id="pageControl" class="page-control"><button id="previous" type="button">‹</button><span id="pageLabel">1/1</span><button id="next" type="button">›</button></div></footer>
</main>
<script>
(() => {
  const host = document.getElementById('pages');
  const source = document.getElementById('areaSource');
  const cards = [...source.children];
  const control = document.getElementById('pageControl');
  const label = document.getElementById('pageLabel');
  let pages = [];
  let index = 0;
  let timer = 0;

  const show = nextIndex => {
    if (!pages.length) return;
    pages[index]?.classList.remove('active');
    index = (nextIndex + pages.length) % pages.length;
    pages[index].classList.add('active');
    label.textContent = `${index + 1}/${pages.length}`;
  };

  const rotate = () => {
    clearInterval(timer);
    if (pages.length > 1) timer = setInterval(() => show(index + 1), {{{rotationMilliseconds}}});
  };

  const rebuild = () => {
    const width = host.clientWidth;
    const height = host.clientHeight;
    const minimumWidth = width > 2600 ? 520 : width > 1600 ? 360 : 280;
    const minimumHeight = height > 1300 ? 330 : height > 800 ? 235 : 185;
    const maximumColumns = Math.max(1, Math.min(6, Math.floor(width / minimumWidth)));
    const maximumRows = Math.max(1, Math.min(4, Math.floor(height / minimumHeight)));
    const rowWeight = card => {
      const density = Number(card.dataset.density || 1);
      return density > 16 ? 3 : density >= 8 ? 2 : 1;
    };
    const columnWeight = card => Number(card.dataset.locations || 0) >= 3 && maximumColumns > 1 ? 2 : 1;
    const units = cards.reduce((sum, card) => sum + rowWeight(card) * columnWeight(card), 0);
    const visibleUnits = Math.max(1, Math.min(units, maximumColumns * maximumRows));
    const aspect = Math.max(1, width / Math.max(1, height));
    const columns = Math.max(1, Math.min(maximumColumns, Math.ceil(Math.sqrt(visibleUnits * aspect))));
    const rows = Math.max(1, Math.min(maximumRows, Math.ceil(visibleUnits / columns)));

    host.innerHTML = '';
    pages = [];

    const createPage = () => {
      const element = document.createElement('div');
      element.className = 'area-page';
      element.style.gridTemplateColumns = `repeat(${columns},minmax(0,1fr))`;
      element.style.gridTemplateRows = `repeat(${rows},minmax(0,1fr))`;
      host.appendChild(element);
      const page = {
        element,
        cells: Array.from({ length: rows }, () => Array(columns).fill(false))
      };
      pages.push(element);
      return page;
    };

    let page = createPage();
    const findPosition = (candidate, rowSpan, columnSpan) => {
      for (let row = 0; row <= rows - rowSpan; row += 1) {
        for (let column = 0; column <= columns - columnSpan; column += 1) {
          let available = true;
          for (let y = row; y < row + rowSpan && available; y += 1) {
            for (let x = column; x < column + columnSpan; x += 1) {
              if (candidate.cells[y][x]) { available = false; break; }
            }
          }
          if (available) return { row, column };
        }
      }
      return null;
    };

    cards.forEach(card => {
      const rowSpan = Math.min(rows, rowWeight(card));
      const columnSpan = Math.min(columns, columnWeight(card));
      let position = findPosition(page, rowSpan, columnSpan);
      if (!position) {
        page = createPage();
        position = findPosition(page, rowSpan, columnSpan);
      }
      for (let y = position.row; y < position.row + rowSpan; y += 1) {
        for (let x = position.column; x < position.column + columnSpan; x += 1) page.cells[y][x] = true;
      }
      card.style.gridRow = `${position.row + 1} / span ${rowSpan}`;
      card.style.gridColumn = `${position.column + 1} / span ${columnSpan}`;
      page.element.appendChild(card);
    });

    index = Math.min(index, pages.length - 1);
    pages.forEach((pageElement, pageIndex) => pageElement.classList.toggle('active', pageIndex === index));
    control.classList.toggle('visible', pages.length > 1);
    label.textContent = `${index + 1}/${pages.length}`;
    rotate();
  };

  document.getElementById('previous').onclick = () => { show(index - 1); rotate(); };
  document.getElementById('next').onclick = () => { show(index + 1); rotate(); };
  let resizeToken;
  addEventListener('resize', () => {
    clearTimeout(resizeToken);
    resizeToken = setTimeout(rebuild, 180);
  });
  rebuild();
})();
</script>
</body>
</html>
""";
    }

    private static string BuildArea(HaidaiDisplayArea area, string assetRoot)
    {
        var locations = area.Locations.Count == 0
            ? "<div class=\"location location-empty\">Sem locais ativos</div>"
            : string.Join(Environment.NewLine, area.Locations.Select(location => BuildLocation(location, assetRoot)));
        var density = Math.Max(1, area.Locations.Count * 2 + area.OperatorCount);
        return $"""
<article class="area-card" data-density="{density}" data-locations="{area.Locations.Count}">
 <header class="area-head"><div class="area-title"><strong>{E(area.Code)}</strong><span>{E(area.NamePt)}</span></div><span class="count">{area.OperatorCount}</span></header>
 <div class="locations">{locations}</div>
</article>
""";
    }

    private static string BuildLocation(HaidaiDisplayLocation location, string assetRoot)
    {
        var operators = location.Operators.Count == 0
            ? "<div class=\"location-empty\">Sem operador</div>"
            : string.Join(Environment.NewLine, location.Operators.Select(row => BuildOperator(row, assetRoot)));
        return $"""
<section class="location"><header class="location-head"><span>{E(location.ShortCode)}</span><span>{E(location.Name)}</span></header><div class="operator-grid">{operators}</div></section>
""";
    }

    private static string BuildOperator(HaidaiBoardRow row, string assetRoot)
    {
        var classes = "operator" + (row.IsTrainee ? " trainee" : "") + (row.IsLeader ? " leader" : "");
        var notes = new List<string>();
        if (row.IsLeader) notes.Add(Role(row.ProfileCode));
        if (row.IsTrainee) notes.Add("Treino");
        if (!row.CountsTowardKousu) notes.Add("Não conta kousu");
        if (!string.IsNullOrWhiteSpace(row.MovementSummary)) notes.Add(row.MovementSummary);
        var note = notes.Count == 0 ? string.Empty : $"<div class=\"operator-note\">{E(string.Join(" • ", notes))}</div>";
        return $"<article class=\"{classes}\"><img class=\"photo\" src=\"{PhotoDataUri(assetRoot, row.CodigoFJ)}\" alt=\"\"><div class=\"operator-copy\"><div class=\"operator-name\">{E(row.Name)}</div>{note}</div></article>";
    }

    private static string BuildLeader(HaidaiBoardRow row) =>
        $"<article class=\"leader\"><strong><span class=\"role\">{E(Role(row.ProfileCode))}</span>{E(row.Name)}</strong><span>{E(row.GroupName)} • {E(row.Status)}</span></article>";

    private static string BuildTraining(HaidaiBoardRow row) =>
        $"<span class=\"training-item\"><strong>{E(row.Name)}</strong> • {E(string.IsNullOrWhiteSpace(row.TrainerName) ? "Treinador não informado" : row.TrainerName)}</span>";

    private static string Metric(string label, int value) => $"<article class=\"metric\"><strong>{value}</strong><span>{E(label)}</span></article>";

    private static string Role(string value) => string.IsNullOrWhiteSpace(value) ? "GL" : value.Trim().ToUpperInvariant();

    private static int LeaderRank(string profile) => Role(profile) switch
    {
        "KL" => 0,
        "GL" => 1,
        "SV" => 2,
        "MANAGER" => 3,
        "DIRECTOR" => 4,
        _ => 5
    };

    private static string PhotoDataUri(string assetRoot, string codigoFJ)
    {
        var operatorsRoot = Path.Combine(assetRoot, "operators");
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var candidate = Path.Combine(operatorsRoot, (codigoFJ ?? string.Empty).Trim() + extension);
            if (File.Exists(candidate)) return ToDataUri(candidate, extension);
        }

        var fallback = Path.Combine(assetRoot, "default-operator.png");
        if (File.Exists(fallback)) return ToDataUri(fallback, ".png");
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='80' height='80'><rect width='80' height='80' rx='40' fill='#dce6eb'/><circle cx='40' cy='30' r='15' fill='#78909d'/><path d='M14 76c2-20 14-30 26-30s24 10 26 30' fill='#78909d'/></svg>"));
    }

    private static string ToDataUri(string path, string extension)
    {
        var mime = extension.ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => "image/png" };
        return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }

    private static string E(string? value) => (value ?? string.Empty)
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
