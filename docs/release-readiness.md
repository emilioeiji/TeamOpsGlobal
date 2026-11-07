# Prontidao para instalacao

## Estado validado

- Schema V2 recriavel do zero com 11 migrations versionadas.
- Primeiro diretor provisionado sem senha padrao.
- Autorizacao por perfil, modulo, acao, arvore organizacional e setores atribuidos.
- Hikitsugui com audiencia explicita por setor, multiplos setores, unidade ou organizacao.
- Haidai compartilhado e processos operacionais configurados por IDs no SQLite.
- SQLite bloqueado em UNC; documentos e integracoes aceitam caminhos UNC por `StorageRoot`.
- UI principal, OperatorApp e ferramenta de diagnostico compilam na mesma solution.

## Validacao antes de copiar para a rede

1. Executar `dotnet test .\TeamOps.Tests\TeamOps.Tests.csproj`.
2. Publicar com `dotnet publish .\TeamOps.UI\TeamOps.UI.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\TeamOps.UI`.
3. Ajustar `TeamOps.UI.dll.config` usando `docs/examples/TeamOps.UI.production.config` como referencia.
4. Manter `DatabasePath` em disco local.
5. Configurar `StorageRoot` com o UNC real da rede, nunca com unidade mapeada.
6. Executar `scripts\Test-TeamOpsDeployment.ps1 -AppDirectory .\artifacts\TeamOps.UI`.
7. Na maquina isolada, confirmar WebView2 Runtime e permissao de leitura/escrita nos compartilhamentos.

Os modelos `ModeloPR.xlsx` e `ModeloCL.xlsx` seguem dentro da pasta `Modelos` do pacote. O aplicativo os provisiona no caminho configurado quando necessario, preservando qualquer modelo personalizado que ja exista.

## Primeira execucao

1. Iniciar o TeamOps com um arquivo SQLite novo.
2. Cadastrar o primeiro diretor e escolher uma senha forte; nao existe senha padrao.
3. Em **Administracao**, confirmar a arvore, setores e **Processos por Setor**.
4. Em **Controle de acesso**, cadastrar os usuarios, definir um unico perfil e associar os setores.
5. Validar login e troca de setor com uma conta de cada perfil utilizado.

## Criterio de liberacao

A instalacao pode ser liberada quando o script de deploy nao apresentar falhas e o roteiro de homologacao de `docs/deployment.md` for concluido na maquina de destino.
