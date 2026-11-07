# Observacoes de dominio

Este documento registra conhecimento inferido pela leitura do codigo. Pontos sem confirmacao explicita estao marcados como **inferido pelo codigo**.

## Contexto operacional

TeamOps apoia rotinas de fabrica com foco em turno, operadores, liderancas, producao e comunicacao operacional. O sistema parece ser usado no inicio e durante o turno para consultar pessoas, registrar ocorrencias e acompanhar status.

## Turnos, setores e operadores

O cadastro de operador se relaciona com turno e setor. O login do usuario precisa estar associado a um operador por `CodigoFJ`; caso contrario, o dashboard falha com "Operador nao encontrado".

**Inferido pelo codigo:** `CodigoFJ` e identificador importante para usuario, operador, tarefas e escala.

## Niveis de acesso

O sistema usa perfis com um unico cargo por usuario, uma matriz de permissoes por modulo/acao e escopo organizacional calculado. Diretor, Gerente e SV recebem setores descendentes da unidade de referencia; GL, KL e OP recebem somente os setores explicitamente associados.

A autorizacao efetiva exige simultaneamente permissao funcional e setor dentro do escopo. O backend repete a validacao mesmo quando a interface oculta a funcionalidade.

## Hikitsugui

Hikitsugui representa passagem de informacao entre turnos/liderancas/operadores. O dominio inclui registros, respostas, correcoes, leitura e anexos.

**Inferido pelo codigo:** existem fluxos separados para criar, ler como lider e ler como operador, indicando necessidade de rastreabilidade de leitura.

## Follow-up

Follow-up usa motivos, tipos, equipamentos, locais, operadores e status. Relatorios dedicados sugerem uso para acompanhamento de pendencias ou ocorrencias ate conclusao.

## Tarefas

Tarefas possuem status, data de vencimento, turno, responsavel e historico de alteracao de status. O dashboard conta tarefas abertas do turno atual, excluindo `completed` e `cancelled`.

## Master Card

Master Card possui status como `in_progress` e `follow`, contados no dashboard. **Inferido pelo codigo:** o modulo acompanha itens ativos que exigem execucao ou acompanhamento posterior.

## Monitor de producao

O monitor importa eventos de maquinas de arquivos TXT de ontem e hoje, com sufixos `211D` e `2400`. O codigo tenta ler UTF-8, Shift-JIS/CP932 e encoding padrao, e interpreta linhas separadas por `|`.

**Inferido pelo codigo:** os codigos de linha `211D` e `2400` sao mapeados para setores internos especificos. Eventos alimentam status atual da maquina e historico.

## Presenca e escala

Agenda de operadores e importada de CSV cujo nome combina setor, turno e data. Linhas invalidas ou de outro setor sao ignoradas. O layout de presenca usa posicoes de operadores e ativos de imagem.

## PR e CL

PR e CL usam categorias, prioridades, setor, operador e templates Excel. **Inferido pelo codigo:** esses modulos geram documentos operacionais salvos em pastas configuradas.

## Paid leave/Yukyu

O dominio inclui `TodokeMotivo`, `AcompYukyu`, `YukyuConferencia`, `YukyuTodoke` e `YukyuFolhaControle`. **Inferido pelo codigo:** o modulo acompanha solicitacoes, conferencia e folhas de controle relacionadas a folga/licenca.

## Dependencias criticas de dominio

- Banco SQLite centralizado.
- Pastas de anexos e documentos.
- Templates Excel.
- Arquivos CSV/TXT/DAT recebidos de processos externos.
- BAT de importacao de producao.
- Monitores publicos que dependem de exportacao HTML.

## Riscos observados

- Caminhos locais em `C:\TeamOps\...` precisam existir em todas as estacoes ou serem substituidos por compartilhamentos consistentes.
- SQLite em rede pode sofrer com locks, latencia ou corrupcao em caso de queda.
- Arquivos externos com layout/encoding diferente sao ignorados ou geram erro.
- Modulos WebView dependem de arquivos estaticos no output; deploy parcial causa tela branca.
