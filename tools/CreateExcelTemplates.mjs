import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const repositoryRoot = path.resolve(import.meta.dirname, "..");
const outputDirectory = path.join(repositoryRoot, "TeamOps.UI", "Templates");
await fs.mkdir(outputDirectory, { recursive: true });

async function createTemplate(kind, fileName) {
    const workbook = Workbook.create();
    const documentSheet = workbook.worksheets.add("PR文書");
    const operatorsSheet = workbook.worksheets.add("Operadores");

    documentSheet.showGridLines = false;
    operatorsSheet.showGridLines = false;
    documentSheet.getRange("A1:T30").format.font = { name: "Yu Gothic", size: 10, color: "#1F2937" };
    documentSheet.getRange("A1:T30").format.rowHeight = 21;

    for (const column of "ABCDEFGHIJKLMNOPQRST") {
        documentSheet.getRange(`${column}:${column}`).format.columnWidth = 5;
    }

    documentSheet.getRange("A1:C1").merge();
    documentSheet.getRange("A1").values = [["優先度 / Prioridade"]];
    documentSheet.getRange("D1:H1").merge();
    documentSheet.getRange("I1:Q1").merge();
    documentSheet.getRange("I1").values = [[kind === "PR" ? "PR 文書 / Documento PR" : "CL 文書 / Documento CL"]];
    documentSheet.getRange("R1:S1").merge();
    documentSheet.getRange("R1").values = [["種類"]];
    documentSheet.getRange("T1").values = [[kind]];

    documentSheet.getRange("A2:S2").merge();
    documentSheet.getRange("A2").values = [["発行日 / Data de emissão"]];
    documentSheet.getRange("A4:C4").merge();
    documentSheet.getRange("A4").values = [["文書番号 / Nº"]];
    documentSheet.getRange("E4:S4").merge();
    documentSheet.getRange("E4").values = [["作成者 / Autor"]];
    documentSheet.getRange("A5:E5").merge();
    documentSheet.getRange("A5").values = [["件名 / Título"]];
    documentSheet.getRange("F5:T5").merge();

    documentSheet.getRange("A6:T6").merge();
    documentSheet.getRange("A6").values = [["カテゴリ / Categoria"]];
    documentSheet.getRange("A7:C7").merge();
    documentSheet.getRange("A7").values = [["品質 / Qualidade"]];
    documentSheet.getRange("E7:M7").merge();
    documentSheet.getRange("E7").values = [["安全 / Segurança"]];
    documentSheet.getRange("O7:T7").merge();
    documentSheet.getRange("O7").values = [["選択 / Seleção"]];
    documentSheet.getRange("A9:C9").merge();
    documentSheet.getRange("A9").values = [["生産 / Produção"]];
    documentSheet.getRange("E9:M9").merge();
    documentSheet.getRange("E9").values = [["改善 / Melhoria"]];
    documentSheet.getRange("O9:T9").merge();
    documentSheet.getRange("O9").values = [["選択 / Seleção"]];

    documentSheet.getRange("A11:T11").merge();
    documentSheet.getRange("A11").values = [["内容 / Conteúdo"]];
    documentSheet.getRange("A12:T25").merge();
    documentSheet.getRange("A27:F27").merge();
    documentSheet.getRange("A27").values = [["承認 / Aprovação"]];
    documentSheet.getRange("G27:M27").merge();
    documentSheet.getRange("G27").values = [["確認 / Verificação"]];
    documentSheet.getRange("N27:T27").merge();
    documentSheet.getRange("N27").values = [["完了 / Conclusão"]];
    documentSheet.getRange("A28:F30").merge();
    documentSheet.getRange("G28:M30").merge();
    documentSheet.getRange("N28:T30").merge();

    documentSheet.getRange("A1:T1").format = {
        fill: "#0F4C81",
        font: { name: "Yu Gothic", bold: true, color: "#FFFFFF", size: 11 },
        verticalAlignment: "center"
    };
    documentSheet.getRange("I1:Q1").format.font = { name: "Yu Gothic", bold: true, color: "#FFFFFF", size: 16 };
    documentSheet.getRange("A2:T2").format = {
        fill: "#DCEAF7",
        font: { name: "Yu Gothic", bold: true, color: "#123B5D", size: 10 }
    };
    documentSheet.getRange("A4:T11").format.borders = { preset: "all", style: "thin", color: "#9CA3AF" };
    documentSheet.getRange("A12:T25").format.borders = { preset: "all", style: "thin", color: "#9CA3AF" };
    documentSheet.getRange("A27:T30").format.borders = { preset: "all", style: "thin", color: "#9CA3AF" };
    for (const range of ["A6:T6", "A11:T11"]) {
        documentSheet.getRange(range).format = {
            fill: "#2B6F9F",
            font: { name: "Yu Gothic", bold: true, color: "#FFFFFF", size: 10 }
        };
    }
    documentSheet.getRange("A27:T27").format = {
        fill: "#DCEAF7",
        font: { name: "Yu Gothic", bold: true, color: "#123B5D", size: 10 }
    };
    for (const cell of ["D7", "D9", "N7", "N9"]) {
        documentSheet.getRange(cell).format.font = { name: "Yu Gothic", bold: true, color: "#0F766E", size: 15 };
    }
    documentSheet.getRange("A1:T30").format.wrapText = true;
    documentSheet.getRange("A1:T30").format.verticalAlignment = "center";
    documentSheet.getRange("A1:T30").format.horizontalAlignment = "center";
    documentSheet.getRange("A12:T25").format.horizontalAlignment = "left";
    documentSheet.freezePanes.freezeRows(2);

    operatorsSheet.getRange("A1:C1").values = [["Código FJ", "Turno dia / 昼勤", "Turno noite / 夜勤"]];
    operatorsSheet.getRange("A1:C1").format = {
        fill: "#0F4C81",
        font: { name: "Yu Gothic", bold: true, color: "#FFFFFF", size: 11 },
        borders: { preset: "all", style: "thin", color: "#9CA3AF" }
    };
    operatorsSheet.getRange("A2:C100").format = {
        font: { name: "Yu Gothic", size: 10 },
        borders: { preset: "all", style: "thin", color: "#D1D5DB" }
    };
    operatorsSheet.getRange("A:A").format.columnWidth = 16;
    operatorsSheet.getRange("B:C").format.columnWidth = 28;
    operatorsSheet.freezePanes.freezeRows(1);

    workbook.recalculate();
    const preview = await workbook.render({ sheetName: "PR文書", autoCrop: "all", scale: 1, format: "png" });
    await fs.writeFile(path.join(outputDirectory, `${kind.toLowerCase()}-preview.png`), new Uint8Array(await preview.arrayBuffer()));
    const operatorsPreview = await workbook.render({ sheetName: "Operadores", autoCrop: "all", scale: 1, format: "png" });
    await fs.writeFile(path.join(outputDirectory, `${kind.toLowerCase()}-operators-preview.png`), new Uint8Array(await operatorsPreview.arrayBuffer()));

    const workbookFile = await SpreadsheetFile.exportXlsx(workbook);
    await workbookFile.save(path.join(outputDirectory, fileName));

    return workbook.inspect({ kind: "sheet,region", range: "A1:T30", maxChars: 3500, tableMaxRows: 8, tableMaxCols: 20 });
}

const prInspection = await createTemplate("PR", "ModeloPR.xlsx");
const clInspection = await createTemplate("CL", "ModeloCL.xlsx");
console.log(String(prInspection));
console.log(String(clInspection));
