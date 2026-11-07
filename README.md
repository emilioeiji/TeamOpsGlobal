# TeamOps

Aplicacao industrial C# WinForms + WebView2 com SQLite local, autorizacao multissetor e documentos/integracoes em armazenamento local ou UNC.

## Validacao

```powershell
dotnet test .\TeamOps.Tests\TeamOps.Tests.csproj
dotnet build .\TeamOps.UI\TeamOps.UI.csproj
```

## Documentacao

- `docs/architecture.md`: arquitetura, inicializacao, seguranca e armazenamento.
- `docs/deployment.md`: publicacao, configuracao UNC, verificacao previa, homologacao e rollback.
- `docs/troubleshooting.md`: diagnostico dos problemas operacionais mais comuns.
- `docs/release-readiness.md`: estado validado e roteiro final para instalacao limpa.

Antes de implantar, use o modelo em `docs/examples/TeamOps.UI.production.config` e execute `scripts/Test-TeamOpsDeployment.ps1` contra a pasta publicada.
