# Deployment

## Pre-requisitos

- Windows com suporte a .NET 9 desktop.
- Microsoft Edge WebView2 Runtime instalado.
- Acesso ao SQLite local configurado na estacao.
- Permissao de leitura e escrita nas pastas operacionais.
- Templates Excel necessarios para PR/CL.
- Compartilhamentos de rede estaveis, quando usados.

## Projetos publicaveis

- `TeamOps.UI`: aplicacao principal.
- `TeamOps.OperatorApp`: app especifico para leitura de Hikitsugui por operador.

Ambos usam `net9.0-windows`, WinForms e dependem de `TeamOps.Core`, `TeamOps.Data` e `TeamOps.Config`.

## Publicacao self-contained

O projeto foi desenhado para deploy simples. A publicacao self-contained reduz necessidade de instalar SDK/runtime .NET na estacao, mas o WebView2 Runtime ainda deve estar presente ou ser distribuido pela politica interna de TI.

Pontos de atencao:

- Publicar todos os arquivos do output, incluindo `ui`, `Assets`, `App.config` e DLLs.
- Validar que os arquivos HTML/JS/CSS foram copiados.
- Manter icones e logos no output.
- Publicar `TeamOps.OperatorApp` separadamente quando usado em estacoes de operador.

## Topologia de armazenamento aprovada

Nesta fase, o banco permanece local e os documentos/integracoes podem usar uma raiz UNC compartilhada:

```text
C:\TeamOps\
  DB\teamops.db

\\nome-do-servidor\compartilhamento\TeamOps\
  PR\
  CL\
  Modelos\ModeloPR.xlsx
  Modelos\ModeloCL.xlsx
  Anexo\Hikitsugui\
  Schedule\
  Haidai\
  Production\
    Events\
    Source\Events\
    Source\Dat\
    import-production.bat
    import.done
```

`StorageRoot` recebe a raiz UNC. Os demais caminhos operacionais permanecem relativos a ela. Nao usar `H:\`, `Z:\` ou outro drive mapeado: o mapeamento varia por usuario, sessao e politica da estacao.

Modelos prontos:

- `docs/examples/TeamOps.UI.production.config`
- `docs/examples/TeamOps.OperatorApp.production.config`

Substituir `nome-do-servidor` e `compartilhamento` pelos valores homologados pela TI. Nao versionar credenciais; o acesso ao compartilhamento deve usar a identidade Windows e permissoes do grupo.

## Banco SQLite

`DatabasePath` define onde o SQLite sera aberto. Ele deve continuar em disco local nesta etapa, por exemplo `C:\TeamOps\DB\teamops.db`. Alterar `StorageRoot` nao move nem recria o banco.

Recomendacoes:

- Usar pasta com backup regular.
- Nao configurar o SQLite em SMB/UNC.
- Garantir permissao de escrita no arquivo e na pasta.
- Evitar multiplas copias divergentes do banco.
- Definir rotina de restauracao em caso de corrupcao.

Antes de copiar ou restaurar o arquivo, fechar todas as instancias do TeamOps na estacao. Copiar um SQLite enquanto ha gravacao em andamento nao e um backup consistente.

## Publicacao recomendada

Executar a partir da raiz do repositorio:

```powershell
dotnet test .\TeamOps.Tests\TeamOps.Tests.csproj -c Release
dotnet publish .\TeamOps.UI\TeamOps.UI.csproj -c Release -r win-x86 --self-contained true -o .\artifacts\TeamOps.UI
dotnet publish .\TeamOps.OperatorApp\TeamOps.OperatorApp.csproj -c Release -r win-x86 --self-contained true -o .\artifacts\TeamOps.OperatorApp
```

Aplicar os modelos de configuracao no pacote publicado, preservando os nomes esperados pelo runtime: `TeamOps.UI.dll.config` e `TeamOps.OperatorApp.dll.config`.

## Verificacao previa

O verificador nao altera dados por padrao:

```powershell
.\scripts\Test-TeamOpsDeployment.ps1 -AppDirectory .\artifacts\TeamOps.UI
```

Por padrao, a verificacao de implantacao exige `StorageRoot` UNC. Para diagnosticar somente um ambiente local de desenvolvimento, usar `-AllowLocalStorageRoot`.

Durante uma janela controlada de homologacao, confirmar tambem escrita na raiz UNC e na pasta local do banco:

```powershell
.\scripts\Test-TeamOpsDeployment.ps1 -AppDirectory .\artifacts\TeamOps.UI -TestWrite
```

O teste de escrita cria e remove somente um arquivo temporario com prefixo `.teamops-write-probe-`. Qualquer item `FAIL` bloqueia a implantacao. Itens `WARN` precisam ser avaliados e registrados.

## WebView2

Todos os modulos HTML dependem do Edge WebView2 Runtime. Sem ele, telas podem ficar brancas ou falhar ao abrir.

Validacao pos-deploy:

1. Abrir dashboard.
2. Abrir um modulo HTML simples.
3. Abrir um modulo com ida/volta de dados, como operadores ou tarefas.
4. Verificar se nao ha tela branca.

## Permissoes

Conceder aos usuarios/grupos:

- Leitura no diretorio da aplicacao.
- Leitura/escrita no banco SQLite local e em sua pasta.
- Leitura/escrita em anexos, PR, CL, Schedule, Haidai e Production, conforme uso.
- Execucao do BAT de importacao de producao quando configurado.

Conceder acesso pelo grupo corporativo, nunca por senha gravada em arquivo de configuracao.

## Dependencias externas por modulo

- PR/CL: templates Excel e pastas de saida.
- Hikitsugui: pasta de anexos.
- Presenca: arquivos CSV em `OperatorScheduleDirectory`.
- Haidai: pasta de exportacao HTML.
- Producao: pastas de eventos, DAT, BAT e arquivo de conclusao.
- WebView: runtime WebView2 e arquivos `ui`.

## Checklist de deploy

- 54 testes automatizados (ou total mais recente) aprovados em Release.
- Build Release concluido sem erro.
- Pasta publicada copiada integralmente.
- Configuracoes da UI e OperatorApp revisadas para o ambiente alvo.
- `StorageRoot` usa UNC real, sem drive mapeado.
- `DatabasePath` permanece local e acessivel.
- Verificador previo concluido sem `FAIL`.
- WebView2 instalado.
- Templates PR/CL presentes.
- Pastas de anexos/exportacao/importacao presentes.
- Teste de login realizado.
- Teste de abertura dos modulos principais realizado.
- Backup inicial do banco configurado.

## Implantacao gradual e homologacao

1. Publicar em pasta versionada, sem sobrescrever a versao ativa.
2. Parar o TeamOps na estacao piloto e copiar o SQLite local para backup com data/hora.
3. Executar o verificador previo com `-TestWrite` usando a mesma conta Windows do usuario final.
4. Iniciar somente uma estacao piloto.
5. Validar login, selecao de setor, Hikitsugui com anexo, PR/CL, schedule, Haidai e importacao de producao.
6. Redefinir a senha de um usuario de teste e confirmar encerramento da sessao antiga em ate cinco segundos.
7. Conferir o Diagnostico de Armazenamento e as auditorias no painel administrativo.
8. Liberar as demais estacoes apenas apos aceite da estacao piloto.

## Rollback

1. Impedir novos acessos e fechar todas as instancias.
2. Preservar uma copia do banco e dos logs gerados durante a tentativa.
3. Reapontar o atalho para a pasta versionada anterior; nao misturar arquivos de duas versoes.
4. Restaurar o SQLite somente se a equipe confirmar que houve alteracao incompatível ou corrupcao.
5. Registrar horario, versao, usuario, sintomas e decisao de restauracao.

As migrations sao progressivas. Voltar apenas o executavel nao desfaz schema; por isso o backup local anterior a implantacao deve ser mantido ate o encerramento da homologacao.
