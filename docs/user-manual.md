# Manual do usuario

## Abrindo o sistema

1. Abra o executavel do TeamOps.
2. Aguarde a tela de login.
3. Informe seu usuario/senha.
4. Apos o login, o dashboard principal sera exibido.

Se o sistema nao abrir ou apresentar erro de banco/pasta, acione o suporte e informe a mensagem exibida.

## Tela principal

A tela principal funciona como menu de acesso aos modulos. Os botoes disponiveis podem variar conforme seu nivel de acesso.

Informacoes exibidas no dashboard:

- Usuario logado.
- Operador associado.
- Turno.
- Data/hora.
- Indicadores resumidos, como tarefas abertas e Master Cards em andamento.

Principais botoes do dashboard:

- **Operadores:** abre a consulta/manutencao de operadores.
- **Atribuicoes:** abre a associacao de operadores com liderancas/grupos.
- **Relatorios:** abre o hub de relatorios operacionais.
- **Presenca/Layout:** abre a visualizacao de presenca e posicoes por setor/turno.
- **Haidai:** abre o painel operacional com exportacao para monitor/TV.
- **Follow-up:** abre o registro de ocorrencias e pendencias.
- **Tarefas:** abre o controle de tarefas.
- **Master Card:** abre o acompanhamento de cards em andamento.
- **Monitor de Producao:** abre a tela de importacao e acompanhamento de eventos de maquinas.
- **Hikitsugui Criacao:** abre a criacao de comunicados/passagem de informacao.
- **Hikitsugui Leitura Lider:** abre a leitura e acompanhamento de comunicados pela lideranca.
- **Sobra de Peca:** abre o registro de sobras.
- **PR / CL:** abre a geracao e consulta de documentos PR ou CL.
- **Yukyu/Paid Leave:** abre o acompanhamento de folgas/licencas.
- **Administracao:** abre cadastros base e consultas administrativas.
- **Controle de Acesso:** abre a manutencao de usuarios e permissoes.

## Modulos

### Operadores

Consulta e manutencao de informacoes de operadores. Use para localizar dados basicos, conferir `CodigoFJ`, turno, setor/grupo e apoiar rotinas de equipe.

Cuidados:

- O `CodigoFJ` deve estar correto, porque ele impacta login, tarefas, presenca e associacao do usuario ao operador.
- Antes de alterar dados cadastrais, confirme se a mudanca vale para o turno atual ou se deve ser alinhada com administracao.

### Atribuicoes

Modulo administrativo para atribuir operadores a liderancas/grupos. Disponivel apenas para usuarios autorizados.

Use quando for necessario ajustar a relacao entre operador, grupo e lideranca. Atribuicoes incorretas podem afetar consultas, filtros por grupo e acompanhamentos de lider.

### Relatorios

Consulta dados consolidados e registros operacionais. Requer perfil de lideranca.

No hub de relatorios, os principais atalhos sao:

- **Hikitsugui:** consulta comunicados/passagens de informacao e registros de leitura.
- **Follow consolidado:** consulta ocorrencias de follow-up por periodo e filtros.
- **Grafico Follow:** visualiza distribuicoes e indicadores graficos de follow-up.
- **Tasks:** consulta tarefas, totais de abertas/concluidas/atrasadas e historico.
- **Operadores:** relatorio gerencial por operador. Use para analisar presenca, producao, Master Card, follow-up e historico diario. O percentual de producao compara o tempo rodando das maquinas do local com o tempo programado para essas maquinas no periodo filtrado.
- **Presenca:** relatorio focado somente em comparecimento. Use para acompanhar dias escalados, presenca conforme, Yukyu, faltas, atrasos, saidas antecipadas e Todoke pendente. Possui filtros por periodo, turno, setor, grupo, status e busca por FJ/nome.
- **Gerencial de Producao:** consolida producao por periodo, setor, local, turno, grupo, operador, maquina, part code e lider.
- **MasterCard:** lista cards por periodo/status/setor/equipamento/operador/treinador.
- **PR / CL:** consulta documentos por periodo, setor, categoria, prioridade e texto, com abertura do arquivo gerado quando disponivel.
- **Sobra de Peca:** consulta registros por periodo, turno, maquina, item ou busca textual, com totais de quantidade e peso.

Para usar o relatorio de presenca:

1. Abra **Relatorios** no dashboard.
2. Clique em **Presenca**.
3. Selecione o periodo e, se necessario, filtre por turno, setor, grupo ou status.
4. Use a busca para localizar um operador pelo codigo FJ ou nome.
5. Confira os cards de resumo e a tabela de operadores.

Para usar o relatorio gerencial de producao:

1. Abra **Relatorios** no dashboard.
2. Clique em **Gerencial de Producao**.
3. Informe o periodo e, se necessario, filtre por setor, local, turno, grupo, operador, maquina, part code ou lider.
4. Use as opcoes de comparativo de grupos e filtros de ativos/com producao quando precisar reduzir o resultado.
5. Confira resumo, rankings, comparativos, tendencia diaria, cruzamento com presenca e alertas.

Observacoes importantes:

- Relatorios com muitos dias, muitos setores ou todos os operadores podem demorar mais.
- Percentuais e rankings dependem de cadastros corretos de maquinas, locais, turnos, presenca e eventos importados.
- Quando um relatorio parecer vazio, confira primeiro periodo, turno, setor e se a importacao/cadastro de origem foi feito.

### Presenca e layout

Exibe e organiza a presenca de operadores por setor/turno. Pode usar arquivos CSV de agenda importados pelo sistema.

Uso comum:

1. Abra **Presenca/Layout**.
2. Selecione data, turno e setor quando a tela solicitar.
3. Confira operadores previstos, status de presenca e posicoes.
4. Ajuste posicoes somente quando tiver permissao e certeza do layout correto.

Se operadores esperados nao aparecerem, verifique se o CSV/agenda do dia foi importado e se o operador esta cadastrado corretamente.

### Haidai

Modulo para acompanhamento/exportacao de informacoes para visualizacao em monitor ou TV. **Comportamento inferido pelo codigo:** o HTML exportado e gravado em pasta configurada para consulta publica/local.

Use para atualizar informacoes operacionais exibidas em monitor publico. Se o monitor/TV nao atualizar, confira se a pasta de exportacao esta acessivel e se a tela publica foi recarregada.

### Follow-up

Registro e acompanhamento de ocorrencias, motivos, tipos, equipamentos e status de tratativa.

Fluxo basico:

1. Abra **Follow-up**.
2. Informe tipo, motivo, equipamento/local, descricao e responsavel quando aplicavel.
3. Salve o registro.
4. Atualize o status conforme a tratativa evoluir.
5. Use os relatorios de Follow no hub de relatorios para acompanhamento consolidado, por operador, grafico ou item unico.

Cadastros de motivo, tipo, local e equipamento devem estar completos para facilitar filtros e analises.

### Tarefas

Controle de tarefas por turno, responsavel e status. O dashboard mostra a quantidade de tarefas abertas do turno atual.

Boas praticas:

- Cadastre tarefas com responsavel, prazo e status claros.
- Atualize o status quando a tarefa mudar de etapa.
- Use o relatorio de Tasks para conferir abertas, concluidas, atrasadas e historico.

### Master Card

Modulo para acompanhamento de itens em andamento e em follow-up. O dashboard mostra contagens por status.

Use para registrar cards com operador, treinador, setor, equipamento, descricao, datas e status. O relatorio permite consultar por periodo, status, setor, equipamento, operador e treinador.

Quando as contagens do dashboard parecerem incorretas, confira se os status dos cards foram atualizados.

### Monitor de producao

Importa e apresenta eventos de maquinas. Usa arquivos TXT/DAT e pode executar um BAT de sincronizacao antes da importacao.

Para detalhes de uso, validacao e comandos de suporte, consulte `docs/production-monitor-guide.md`.

Fluxo basico:

1. Abra **Monitor de Producao**.
2. Confira se a data/setor/local desejado esta correto.
3. Execute a importacao quando necessario.
4. Aguarde a conclusao antes de atualizar a visualizacao ou abrir relatorios.
5. Confira indicadores, maquinas, status atual, EC2, codigos parametrizados e previsao G-Bareru quando configurada.

Cuidados:

- Nao interrompa o computador durante importacao.
- Status desconhecido pode indicar cadastro de status incompleto.
- Se a tela nao trouxer dados, confirme se os arquivos TXT/DAT existem, se o BAT rodou e se o banco configurado e o correto.
- Comandos de suporte do `ProductionMonitorProbe` devem ser usados por equipe tecnica, principalmente comandos que alteram dados.

### Hikitsugui

Registro e leitura de passagem de informacao. Existem telas para criacao, leitura por lider e leitura por operador.

Fluxos:

- **Criacao:** registrar comunicado/passagem de informacao, anexos e dados necessarios.
- **Leitura Lider:** acompanhar mensagens, respostas, correcoes, anexos e rastreabilidade de leitura.
- **Leitura Operador:** visualizar comunicados no app do operador e registrar leitura/resposta quando aplicavel.

Se anexos nao abrirem, verifique permissao e disponibilidade da pasta de anexos.

### Sobra de peca

Registro e acompanhamento de sobras de peca. Requer perfil autorizado.

Ao registrar uma sobra, informe lote, operador, maquina, item, peso, quantidade, shain, lider e observacao quando aplicavel. Use o relatorio de Sobra de Peca para consultar por periodo, turno, maquina, item ou busca textual.

Padronize nomes de item e observacoes para facilitar filtros futuros.

### PR e CL

Geracao/controle de documentos PR e CL com uso de templates e pastas configuradas.

Fluxo basico:

1. Abra **PR** ou **CL** pelo dashboard.
2. Selecione setor, categoria e prioridade.
3. Informe titulo e nome do arquivo.
4. Gere o documento.
5. Abra o arquivo gerado quando precisar conferir ou complementar o documento.

Os arquivos sao gerados a partir de templates Excel e dependem das pastas configuradas. Se houver erro de template ou pasta, acione suporte com o tipo do documento e a mensagem exibida.

Use os relatorios PR/CL no hub de relatorios para consultar documentos por periodo, setor, categoria, prioridade e texto.

### Yukyu/Paid leave

Acompanhamento de solicitacoes/controles relacionados a folga/licenca paga. Usa dados de operador, turno e motivos cadastrados.

Use para acompanhar folgas/licencas, conferencias, Todoke e motivos. Dados incompletos de operador, turno ou motivo podem deixar o controle inconsistente.

### Administracao

Modulo restrito para manter os cadastros base do sistema. Use para revisar a arvore organizacional, setores, turnos, grupos, locais, maquinas, equipamentos, status de maquina, processos por setor, codigos visuais da producao, tempos de procedimento, categorias, motivos/tipos de follow, shain, auditoria, diagnostico de armazenamento e log do sistema.

Operacoes principais:

- Selecione um cadastro na lateral esquerda.
- Use **Buscar** para filtrar os registros carregados.
- Clique em **Novo** para cadastrar um item editavel.
- Use o icone de lapis para editar uma linha existente.
- Use o icone de lixeira para excluir uma linha, apos confirmar.
- Em telas somente leitura, como **Pendencias de Maquina** e **Log do Sistema**, os botoes de novo, editar e excluir ficam indisponiveis.
- No **Log do Sistema**, preencha os filtros de data, usuario, modulo ou acao e clique em **Atualizar**.
- Em **Processos por Setor**, associe cada importador, forecast ou Sobra de Peca ao setor responsavel. Desative a associacao atual antes de ativar o mesmo processo em outro setor.

Para a documentacao completa do painel e dos botoes, consulte `docs/admin-panel.md`.

### Controle de acesso

Modulo para usuarios, perfis, matriz de permissoes e configuracoes de acesso. Uso restrito.

Use para:

- Criar ou editar usuarios.
- Definir um unico perfil/cargo.
- Definir a unidade ou setor de referencia.
- Associar os setores permitidos para GL, KL e OP.
- Ajustar a matriz de permissoes por modulo e acao, quando autorizado.
- Associar usuario ao `CodigoFJ` correto.
- Alterar senha quando necessario.
- Ativar, desativar ou revisar usuarios conforme permissao da tela.

Cuidados:

- Senhas sao protegidas por BCrypt; nao ha consulta de senha original.
- A conta administrativa inicial pode operar Administracao e Controle de Acesso sem `CodigoFJ`; modulos operacionais exigem identidade FJ associada.
- Alteracoes de perfil, escopo e permissoes invalidam as sessoes afetadas e devem seguir autorizacao da lideranca responsavel.

### Aplicacao do operador

Aplicacao separada para leitura de Hikitsugui pelo operador.

Uso esperado:

1. O operador abre a aplicacao do operador.
2. Consulta comunicados disponiveis.
3. Registra leitura e resposta quando solicitado.
4. Informa a lideranca se houver erro de banco, anexo ou mensagem nao exibida.

O app do operador deve apontar para o mesmo banco operacional usado pelo TeamOps principal.

## Operacoes comuns

- Entrar no sistema no inicio do turno.
- Conferir se seu nome, turno e setor estao corretos.
- Abrir o modulo necessario pelo dashboard.
- Registrar informacoes com atencao a data, turno, setor, operador e anexos.
- Salvar antes de fechar a tela.
- Em relatorios, usar filtros antes de imprimir/exportar.
- Em modulos de monitor, aguardar a conclusao da importacao antes de atualizar a visualizacao.

## Boas praticas

- Nao abrir varias janelas do mesmo modulo sem necessidade.
- Evitar desligar o computador durante importacao ou salvamento.
- Confirmar se o drive/pasta de rede esta acessivel antes de iniciar o turno.
- Informar mensagens de erro completas ao suporte.
- Nao mover arquivos de templates, anexos, banco ou exportacoes sem alinhar com a equipe tecnica.
- Em monitores publicos, atualizar a tela quando houver suspeita de cache.

## Mensagens importantes

- **Acesso negado:** seu usuario nao possui permissao para o modulo.
- **Operador nao encontrado:** o usuario logado nao esta associado a um operador valido.
- **Turno nao encontrado:** o cadastro do operador aponta para um turno inexistente ou inconsistente.
- **Arquivo nao encontrado:** uma pasta ou template configurado esta ausente ou inacessivel.
- **Erro de banco:** o SQLite pode estar indisponivel, bloqueado ou inacessivel pela rede.

## Em caso de erro

1. Anote a mensagem.
2. Feche somente a tela com erro, se possivel.
3. Verifique se a rede e as pastas compartilhadas estao acessiveis.
4. Tente abrir novamente o modulo.
5. Se persistir, acione o suporte com usuario, modulo, horario e acao realizada.
