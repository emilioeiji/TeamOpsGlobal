# Referencia de modulos

## Dashboard

- **Finalidade:** menu principal, indicadores resumidos e roteamento para modulos.
- **Arquivos principais:** `Forms/FormDashboardHtml.cs`, `ui/dashboard/*`.
- **Dependencias:** usuario logado, operador, turno, WebView2, repositorios.
- **Inputs:** usuario autenticado, operador associado, turno, locale e mensagens `open:*`.
- **Outputs:** abertura de forms HTML/WinForms, payload inicial para HTML e contadores resumidos.
- **Aberturas diretas:** Operadores, Atribuicoes, Relatorios, Presenca/Layout, Haidai, Follow-up, Tarefas, Master Card, Monitor de Producao, Hikitsugui Criacao, Hikitsugui Leitura Lider, Sobra de Peca, PR, CL, Yukyu/Paid Leave, Administracao e Controle de acesso.
- **Riscos:** usuario sem operador/turno impede abertura; WebView2 ausente gera tela branca.

## Operadores

- **Finalidade:** consulta/manutencao de operadores.
- **Arquivos principais:** `HTMLFormOperators.cs`, `ui/operators/*`, `OperatorRepository.cs`.
- **Dependencias:** SQLite, tabela `Operators`.
- **Inputs:** dados cadastrais de operador.
- **Outputs:** lista/alteracoes de operadores.
- **Riscos:** inconsistencias em `CodigoFJ` afetam login, tarefas e escala.

## Atribuicoes

- **Finalidade:** associar operadores a liderancas/grupos.
- **Arquivos principais:** `FormAssignments.cs`, `AssignmentRepository.cs`, `GroupLeaderRepository.cs`.
- **Dependencias:** `Assignments`, `GroupLeaders`, `Operators`.
- **Inputs:** operador, lider/grupo.
- **Outputs:** atribuicoes gravadas.
- **Riscos:** permissao administrativa exigida; atribuicoes incorretas afetam fluxo operacional.

## Relatorios

- **Finalidade:** hub de acesso a consultas consolidadas e relatorios especificos.
- **Arquivos principais:** `HTMLFormReports.cs`, `ui/reports/*`, `ui/operator-manager-report/*`, `ui/presence-report/*`, `ui/production-management-report/*`, `ui/mastercard-report/*`, `ui/pr-cl-report/*`, `ui/sobra-de-peca-report/*`, forms `HTMLForm*Report.cs` e services correspondentes.
- **Dependencias:** Hikitsugui, Follow-up, Tasks, operadores, presenca, producao, Master Card, PR/CL, Sobra de Peca, WebView2 e SQLite.
- **Inputs:** acao selecionada no hub e filtros de cada relatorio.
- **Outputs:** abertura de relatorios, visualizacao consolidada e possiveis exportacoes/impressao conforme tela.
- **Atalhos do hub:** Hikitsugui, Follow consolidado, Grafico Follow, Tasks, Operadores, Presenca, Gerencial de Producao, MasterCard, PR, CL e Sobra de Peca.
- **Relatorio gerencial de operadores:** `HTMLFormOperatorManagerReport` consolida presenca, producao, Master Card, follow-up e historico diario do operador selecionado. O percentual de producao usa minutos rodando divididos pelos minutos programados por maquina/local no periodo.
- **Relatorio de presenca:** `HTMLFormPresenceReport` consulta escala e comparecimento, com filtros por periodo, turno, setor, grupo, status e busca por FJ/nome. Considera Haidai, registros de presenca, Yukyu/Todoke e movimentos para destacar presenca conforme, falta, Yukyu, atraso, saida antecipada e Todoke pendente.
- **Relatorio gerencial de producao:** consolida producao, operadores, maquinas, setores, rankings, comparativo por turno/grupo, tendencia diaria, cruzamento de presenca e alertas.
- **Relatorios PR/CL:** filtram documentos por periodo, setor, categoria, prioridade e texto, com abertura do arquivo gerado.
- **Relatorios MasterCard, Tasks e Sobra de Peca:** listam registros, totais e detalhes/historico quando disponivel.
- **Riscos:** consultas grandes podem ficar lentas em banco de rede; percentuais dependem de escala/local, eventos de maquina, cadastros de maquinas e eventos de presenca cadastrados corretamente.

## Presenca/Layout

- **Finalidade:** exibir presenca e posicoes de operadores por setor/turno, com suporte a layout visual.
- **Arquivos principais:** `HTMLFormPresenceLayout.cs`, `FormPresenceLayout.cs`, `ui/presence-layout/*`, `ui/presence/*`.
- **Dependencias:** `OperatorPresence`, `OperatorPositions`, `OperatorSchedule`, CSVs de agenda.
- **Inputs:** setor, turno, data, CSV de schedule e posicoes salvas.
- **Outputs:** layout visual, posicoes e status de presenca.
- **Observacao:** `ui/presence-layout` e a tela moderna de layout; `ui/presence` e assets de presenca continuam no projeto e devem ser tratados como parte do dominio de presenca.
- **Riscos:** CSV ausente ou fora do padrao deixa operadores fora do layout.

## Haidai

- **Finalidade:** painel operacional com exportacao para monitor/TV.
- **Arquivos principais:** `HTMLFormHaidai.cs`, `HaidaiModuleService.cs`, `ui/haidai/*`.
- **Dependencias:** SQLite, `HaidaiExportDirectory`.
- **Inputs:** dados operacionais do modulo.
- **Outputs:** visualizacao e HTML exportado.
- **Riscos:** pasta de exportacao sem permissao impede atualizacao de monitor publico.

## Follow-up

- **Finalidade:** registrar e acompanhar ocorrencias/pendencias.
- **Arquivos principais:** `HTMLFormFollowUp.cs`, `ui/follow-up/*`, `FollowUpRepository.cs`, repositorios de motivos/tipos/equipamentos.
- **Dependencias:** `FollowUps`, `FollowUpReasons`, `FollowUpTypes`, `Equipments`, `Locals`.
- **Inputs:** tipo, motivo, equipamento, descricao, responsavel/status.
- **Outputs:** registros e relatorios.
- **Riscos:** cadastros auxiliares incompletos reduzem qualidade do registro.

## Relatorios de Follow-up

- **Finalidade:** analise por operador, consolidado, grafico ou item unico.
- **Arquivos principais:** `HTMLFormFollowReport.cs`, `HTMLFormFollowOperatorReport.cs`, `HTMLFormFollowSingleReport.cs`, `HTMLFormFollowChart.cs`, `ui/follow-report/*`, `ui/follow-operator-report/*`, `ui/follow-single-report/*`, `ui/follow-chart/*`.
- **Dependencias:** dados de follow-up e WebView2.
- **Inputs:** filtros e comandos de impressao/PDF.
- **Outputs:** relatorios e PDFs.
- **Riscos:** impressao/PDF depende do WebView2.

## Tarefas

- **Finalidade:** controle de tarefas por turno/responsavel/status.
- **Arquivos principais:** `HTMLFormTasks.cs`, `HTMLFormTasksReport.cs`, `ui/tasks/*`, `ui/tasks-report/*`.
- **Dependencias:** tabelas `Tasks` e `TaskStatusHistory`.
- **Inputs:** tarefa, responsavel, prazo, status.
- **Outputs:** tarefas, historico de status, relatorio com totais de abertas/concluidas/atrasadas e detalhe por tarefa.
- **Riscos:** status inconsistentes afetam contagem de tarefas abertas.

## Master Card

- **Finalidade:** acompanhamento de itens em andamento/follow-up.
- **Arquivos principais:** `HTMLFormMasterCard.cs`, `HTMLFormMasterCardReport.cs`, `MasterCardModuleService.cs`, `ui/mastercard/*`.
- **Dependencias:** schema garantido pelo service e SQLite.
- **Inputs:** operador, treinador, setor, equipamento, descricao, datas, status e filtros de relatorio.
- **Outputs:** cards, historico, relatorio por periodo/status/setor/equipamento/operador/treinador e contagens no dashboard.
- **Riscos:** schema dinamico precisa ser garantido antes das consultas.

## Monitor de producao

- **Finalidade:** importar eventos de maquinas e apresentar status atual/historico.
- **Arquivos principais:** `HTMLFormProductionMonitor.cs`, `ProductionFileImporter.cs`, `ProductionPlanDatImporter.cs`, `ProductionAnalyticsService.cs`, `ui/production-monitor/*`.
- **Dependencias:** `Machines`, `MachineEvents`, `MachineCurrentStatus`, `MachineStatuses`, arquivos TXT/DAT, BAT, EC2 Administrator e configuracoes em `App.config`.
- **Inputs:** arquivos `yyMMdd_211D_E.txt`, `yyMMdd_2400_E.txt`, DATs de plano.
- **Outputs:** eventos importados, maquinas criadas, status atual, indicadores de kadouritsu, EC2, codigos parametrizados e previsao G-Bareru quando configurada.
- **Probe:** comandos documentados em `docs/production-monitor-guide.md`.
- **Riscos:** layout/encoding inesperado, timeout do BAT, rede lenta, status desconhecido sem classificacao setorial, config apontando para banco errado.

## Relatorio gerencial de producao

- **Finalidade:** consolidar indicadores gerenciais de producao por periodo, setor, local, turno, grupo, operador, maquina, part code e lider.
- **Arquivos principais:** `HTMLFormProductionManagementReport.cs`, `ProductionManagementReportService.cs`, `ui/production-management-report/*`.
- **Dependencias:** `ProductionAnalyticsService`, `HaidaiAssignments`, `OperatorPresence`, `Operators`, `Shifts`, `Sectors`, `Groups`, `Locals`, `Machines`, `MachineEvents`, `Ec2MachineCurrentState` e `SystemLog`.
- **Inputs:** periodo, setor, local, turno, grupo, comparativo grupo A/B, operador, maquina, part code, lider, somente ativos e somente com producao.
- **Outputs:** resumo de producao/meta/eficiencia, ranking de operadores/setores/grupos/maquinas, comparativo de turnos, comparativo de grupos, tendencia diaria, linhas de operador/maquina/setor, cruzamento de presenca, alertas e tempos de execucao.
- **Riscos:** consultas por muitos dias/turnos podem ficar pesadas; resultado depende da consistencia entre Haidai, presenca, maquinas e eventos importados.

## ProductionMonitorProbe

- **Finalidade:** ferramenta de linha de comando para diagnostico, validacao, auditoria e manutencao do monitor de producao.
- **Arquivos principais:** `ProductionMonitorProbe/Program.cs`, `ProductionMonitorProbe/ProductionMonitorProbe.csproj`, `ProductionMonitorProbe/App.config`, `ProductionMonitorProbe/Comandos.txt`.
- **Dependencias:** `TeamOps.Config`, `TeamOps.Data`, `TeamOps.Core`, banco SQLite e mesmos arquivos/configuracoes usados pelo monitor de producao.
- **Inputs:** comandos CLI como `schema-check`, `schema-repair`, `db-index-check`, `production-diagnostics`, `production-audit`, `validate-reports`, `validate-yakin-production`, `validate-overtime-rules`, `validate-overtime-real`, `ec2-diagnostics`, `status-report`, `import`, `import-profile`, `machine-cleanup`, `machine-location-guard` e `dashboard`.
- **Outputs:** diagnosticos no console, validacoes de schema, auditorias de producao/presenca/horas extras, relatorios CSV opcionais e importacoes quando comandos de alteracao sao usados.
- **Riscos:** comandos de diagnostico sao seguros, mas `import`, `import-profile`, `schema-repair`, `ec2-reset-latest` e `machine-cleanup --apply` podem alterar dados; sempre conferir `DatabasePath` antes de executar.

## Hikitsugui

- **Finalidade:** passagem de informacao, leitura, respostas, correcoes e anexos.
- **Arquivos principais:** `HTMLHikitsuguiCreate.cs`, `HTMLHikitsuguiLeaderRead.cs`, `HTMLFormHikitsuguiReader.cs`, `TeamOps.OperatorApp/Forms/HTMLHikitsuguiOperatorRead.cs`, `ui/hikitsugui-create/*`, `ui/hikitsugui-leader-read/*`, `ui/hikitsugui-reader/*`, `TeamOps.OperatorApp/ui/hikitsugui-operator-read/*`.
- **Dependencias:** `Hikitsugui`, `HikitsuguiResponses`, `HikitsuguiCorrections`, `HikitsuguiReads`, `HikitsuguiAttachments`, pasta de anexos.
- **Inputs:** registro, resposta, leitura, anexo.
- **Outputs:** comunicados e rastreabilidade de leitura.
- **Riscos:** pasta de anexos indisponivel; leituras podem nao ser registradas se houver erro de banco.

## Sobra de peca

- **Finalidade:** registrar e acompanhar sobra de pecas.
- **Arquivos principais:** `HTMLFormSobraDePeca.cs`, `HTMLFormSobraDePecaReport.cs`, `ui/sobra-de-peca/*`, `ui/sobra-de-peca-report/*`, `SobraDePecaRepository.cs`.
- **Dependencias:** tabela `SobraDePeca`, operador atual, `Shifts`, `Machines`, `Operators` e `Shain`.
- **Inputs:** dados da sobra, lote, operador, maquina, item, peso, quantidade, shain, lider, observacao e filtros de relatorio.
- **Outputs:** registros consultaveis, relatorio por periodo/turno/maquina/item/busca, totais de quantidade, peso e itens distintos.
- **Riscos:** falta de padronizacao nos campos pode dificultar relatorio.

## PR

- **Finalidade:** criar/controlar documentos PR.
- **Arquivos principais:** `HTMLFormPrCl.cs`, `HTMLFormPrClReport.cs`, `ui/pr-cl/*`, `ui/pr-cl-report/*`, `PRRepository.cs`, `PRCategoriaRepository.cs`, `PRPrioridadeRepository.cs`, `FormPR.cs` legado.
- **Dependencias:** `PR`, `PRCategorias`, `PRPrioridades`, setores, operadores, `PRTemplate`, `PRDirectory`, ClosedXML.
- **Inputs:** setor, categoria, prioridade, titulo e nome do arquivo.
- **Outputs:** registro no banco, arquivo Excel gerado a partir do template, aba de operadores preenchida e relatorio com filtros por periodo/setor/categoria/prioridade/busca.
- **Riscos:** template/pasta ausente.

## CL

- **Finalidade:** criar/controlar documentos CL.
- **Arquivos principais:** `HTMLFormPrCl.cs`, `HTMLFormPrClReport.cs`, `ui/pr-cl/*`, `ui/pr-cl-report/*`, `CLRepository.cs`, `CLCategoriaRepository.cs`, `CLPrioridadeRepository.cs`, `FormCL.cs` legado.
- **Dependencias:** `CL`, `CLCategorias`, `CLPrioridades`, setores, operadores, `CLTemplate`, `CLDirectory`, ClosedXML.
- **Inputs:** setor, categoria, prioridade, titulo e nome do arquivo.
- **Outputs:** registro no banco, arquivo Excel gerado a partir do template, aba de operadores preenchida e relatorio com filtros por periodo/setor/categoria/prioridade/busca.
- **Riscos:** template/pasta ausente.

## Yukyu/Paid Leave

- **Finalidade:** acompanhar folgas/licencas e conferencia.
- **Arquivos principais:** `FormPaidLeaveTracking.cs`, `ui/paidleave/*`, SQL em `Sql/paidleave`.
- **Dependencias:** `AcompYukyu`, `YukyuConferencia`, `YukyuTodoke`, `YukyuFolhaControle`, `TodokeMotivo`.
- **Inputs:** operador, turno, motivos, conferencia.
- **Outputs:** acompanhamento e registros de controle.
- **Riscos:** dados de motivo/operador incompletos.

## Administracao

- **Finalidade:** manter cadastros e consultas administrativas usados por producao, Haidai, presence, follow-up, relatorios e suporte.
- **Arquivos principais:** `HTMLFormAdmin.cs`, `ui/admin/*`.
- **Dependencias:** usuario administrador e SQLite.
- **Inputs:** cadastros base, maquinas, locais, status de maquina, codigos de producao, tempos de procedimento, motivos/tipos de follow, shain e filtros do log do sistema.
- **Outputs:** alteracoes administrativas, consultas de pendencias de maquina e consulta ao log do sistema.
- **Riscos:** alteracoes incorretas em locais, maquinas, status ou tempos de procedimento afetam producao, relatorios e indicadores. Acesso indevido pode afetar operacao geral.
- **Guia detalhado:** `docs/admin-panel.md`.

## Controle de acesso

- **Finalidade:** gerenciar usuarios e permissoes.
- **Arquivos principais:** `HTMLFormAccessControl.cs`, `FormAccessControl.cs`, `FormAddUser.cs`, `FormChangePassword.cs`, `ui/access-control/*`, `UserRepository.cs`.
- **Dependencias:** `Users`, BCrypt, `AccessProfiles`, `ModulePermissions`, arvore organizacional e atribuicoes explicitas de setor.
- **Inputs:** usuario, senha, nivel, CodigoFJ, troca de senha e comandos de ativacao/edicao conforme tela.
- **Outputs:** usuarios, permissoes, senhas protegidas por BCrypt e contadores de usuarios/admins.
- **Riscos:** usuario sem `CodigoFJ` valido nao abre dashboard corretamente.

## Aplicacao do operador

- **Finalidade:** leitura de Hikitsugui por operador em app separado.
- **Arquivos principais:** `TeamOps.OperatorApp/Program.cs`, `Forms/HTMLHikitsuguiOperatorRead.cs`, `ui/hikitsugui-operator-read/*`.
- **Dependencias:** WebView2, SQLite, anexos.
- **Inputs:** acao de leitura/resposta do operador.
- **Outputs:** registro de leitura e visualizacao de comunicados.
- **Riscos:** configuracao do `DatabasePath` deve apontar para o mesmo banco operacional.

## Infraestrutura, configuracao e dados

- **Finalidade:** fornecer base compartilhada para configuracao, entidades, validacoes, banco, migrations, repositorios e services.
- **Arquivos principais:** `TeamOps.Config/*`, `TeamOps.Core/*`, `TeamOps.Data/*`, `TeamOps.UI/App.config`, `TeamOps.OperatorApp/App.config`.
- **Dependencias:** SQLite, Dapper, app settings, migrations SQL e caminhos configurados.
- **Inputs:** `DatabasePath`, caminhos de templates/pastas, arquivos TXT/DAT/CSV, dados operacionais e comandos dos modulos.
- **Outputs:** conexoes SQLite, schema atualizado, entidades, validacoes, repositorios e services usados pelas telas.
- **Riscos:** configuracao divergente entre UI, OperatorApp e Probe pode apontar para bancos diferentes; migrations incompletas ou app settings ausentes geram erro em tempo de execucao.
