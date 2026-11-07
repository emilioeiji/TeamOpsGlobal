# Troubleshooting

Este guia foca problemas plausiveis em ambiente industrial, com SQLite local e pastas operacionais em armazenamento local ou compartilhamento UNC.

## Sistema nao abre

**PROBLEMA:** o TeamOps nao inicia ou fecha antes do login.

**SINTOMA:** erro ao abrir executavel, tela nao aparece, mensagem de banco/caminho/permissao.

**CAUSA PROVAVEL:** `DatabasePath` local inacessivel, SQLite indisponivel, permissao insuficiente, antivirus bloqueando arquivos, deploy incompleto ou arquivo de configuracao incorreto.

**COMO RESOLVER:**

1. Verificar se `C:\TeamOps\DB\teamops.db` ou o caminho configurado existe.
2. Confirmar permissao de leitura e escrita na pasta do banco.
3. Confirmar que `DatabasePath` nao aponta para UNC ou drive mapeado.
4. Executar `scripts\Test-TeamOpsDeployment.ps1` contra a pasta publicada.
5. Conferir se todos os arquivos publicados estao na pasta do executavel.
6. Verificar quarentena/bloqueio do antivirus.

**COMO PREVENIR:** manter o SQLite local, usar UNC estavel somente para `StorageRoot`, validar permissoes por grupo e manter o pacote de deploy completo.

## Tela branca no WebView

**PROBLEMA:** um modulo abre em branco.

**SINTOMA:** janela aparece sem conteudo, sem botoes ou com carregamento infinito.

**CAUSA PROVAVEL:** Edge WebView2 Runtime ausente/corrompido, arquivo HTML nao copiado para o output, falha de JavaScript, pasta `ui/<modulo>` ausente, cache do WebView corrompido.

**COMO RESOLVER:**

1. Instalar ou reparar Microsoft Edge WebView2 Runtime.
2. Conferir se `ui/<modulo>/index.html`, `app.js` e `style.css` existem na pasta publicada.
3. Reabrir o sistema.
4. Se ocorrer em apenas um computador, limpar cache/local data do WebView2 ou reinstalar o runtime.
5. Se ocorrer apos deploy, publicar novamente garantindo `CopyToOutputDirectory`.

**COMO PREVENIR:** incluir WebView2 nos pre-requisitos de estacao, testar cada modulo apos publicacao e evitar copiar parcialmente a pasta do sistema.

## Erro de banco SQLite

**PROBLEMA:** falha ao consultar ou salvar dados.

**SINTOMA:** mensagens de lock, database is locked, disk I/O error, unable to open database file ou database disk image is malformed.

**CAUSA PROVAVEL:** arquivo SQLite em uso por multiplos processos, perda de conexao de rede, permissao insuficiente, arquivo corrompido, antivirus inspecionando o DB ou pasta temporariamente indisponivel.

**COMO RESOLVER:**

1. Verificar conectividade com a pasta do banco.
2. Fechar instancias extras do TeamOps na mesma estacao.
3. Confirmar permissao de escrita no arquivo e na pasta.
4. Reiniciar a estacao se houver processo travado.
5. Se houver suspeita de corrupcao, parar o uso e restaurar backup.
6. Solicitar analise tecnica antes de copiar/substituir o DB em producao.

**COMO PREVENIR:** manter backup regular, evitar banco em links de rede instaveis, excluir a pasta do DB de varreduras agressivas quando politica permitir e limitar acessos simultaneos desnecessarios.

## Performance ruim

**PROBLEMA:** sistema lento ao abrir telas, salvar ou carregar relatorios.

**SINTOMA:** janelas demoram, importacao demora, WebView responde lentamente.

**CAUSA PROVAVEL:** rede lenta, banco grande, muitos acessos simultaneos, falta de indices para consultas novas, computador com pouca memoria/CPU, antivirus analisando arquivos de dados.

**COMO RESOLVER:**

1. Testar velocidade de acesso ao compartilhamento.
2. Fechar telas/modulos nao usados.
3. Verificar tamanho do banco e historico acumulado.
4. Testar em outra estacao para separar problema local de problema geral.
5. Solicitar manutencao tecnica do banco quando a lentidao for recorrente.

**COMO PREVENIR:** planejar limpeza/arquivamento, revisar consultas pesadas, manter rede estavel e evitar rodar importacoes grandes em horarios criticos.

## Modulo nao carrega

**PROBLEMA:** um modulo especifico nao abre pelo dashboard.

**SINTOMA:** clique nao faz nada, aparece acesso negado ou erro de arquivo.

**CAUSA PROVAVEL:** usuario sem permissao, pasta HTML ausente, arquivo exportado nao encontrado, caminho invalido no `App.config`, erro de inicializacao de repositorio.

**COMO RESOLVER:**

1. Confirmar o nivel de acesso do usuario.
2. Conferir se o modulo existe na pasta `ui`.
3. Validar os caminhos do `App.config` usados pelo modulo.
4. Reabrir o sistema e tentar novamente.
5. Se o erro cita arquivo especifico, recriar/copiar esse arquivo.

**COMO PREVENIR:** testar deploy completo, manter controle de acesso atualizado e documentar alteracoes de caminhos.

## Monitor publico nao atualiza

**PROBLEMA:** TV/monitor publico continua mostrando informacao antiga.

**SINTOMA:** usuario atualiza dados no TeamOps, mas a tela publica nao muda.

**CAUSA PROVAVEL:** exportacao HTML falhou, pasta `HaidaiExportDirectory` sem permissao, arquivo exportado nao foi substituido, navegador em cache, trigger/agendamento de atualizacao nao executado.

**COMO RESOLVER:**

1. Verificar se a pasta de exportacao existe e aceita escrita.
2. Conferir data/hora do arquivo HTML exportado.
3. Atualizar o browser com recarregamento completo.
4. Limpar cache do navegador do monitor.
5. Reexecutar a exportacao pelo TeamOps.

**COMO PREVENIR:** usar pasta dedicada com permissao correta, validar atualizacao apos mudancas e configurar recarregamento automatico quando aplicavel.

## Importacao de producao falha

**PROBLEMA:** monitor de producao nao importa eventos.

**SINTOMA:** erro no BAT, timeout, arquivo `.done` ausente, zero arquivos lidos, linhas ignoradas.

**CAUSA PROVAVEL:** `ProductionImportBatchPath` incorreto, origem sem arquivos, sem permissao nas pastas, formato TXT inesperado, encoding incompativel, arquivos com nome fora do padrao `yyMMdd_211D_E.txt` ou `yyMMdd_2400_E.txt`, arquivo DAT ausente.

**COMO RESOLVER:**

1. Confirmar caminhos `ProductionEventsDirectory`, `ProductionSourceEventsDirectory`, `ProductionSourceDatDirectory` e BAT.
2. Executar o BAT manualmente em ambiente controlado para validar permissao.
3. Verificar se `import.done` e criado.
4. Conferir nomes dos arquivos de ontem/hoje.
5. Validar se as linhas TXT possuem campos separados por `|`.
6. Ajustar timeout se a rede for lenta.

**COMO PREVENIR:** monitorar copia de arquivos, padronizar nomes/layouts, manter amostras validas e registrar erros recorrentes de encoding/layout.

## Importacao de agenda/presenca falha

**PROBLEMA:** escala ou presenca nao aparece corretamente.

**SINTOMA:** operadores ausentes no layout, arquivo nao encontrado ou dados incompletos.

**CAUSA PROVAVEL:** `OperatorScheduleDirectory` incorreto, CSV ausente, nome fora do padrao `<setor><turno>-yyyyMMdd.csv`, linhas com menos de 3 colunas, setor no CSV diferente da tela, codigo FJ invalido.

**COMO RESOLVER:**

1. Conferir a pasta configurada.
2. Verificar o nome do arquivo esperado para setor, turno e data.
3. Abrir o CSV e validar colunas: codigo FJ, local, setor.
4. Corrigir linhas invalidas e importar novamente.

**COMO PREVENIR:** gerar CSV sempre pelo mesmo processo, validar antes do turno e manter exemplos de arquivos corretos.

## PR ou CL nao gera arquivo

**PROBLEMA:** falha ao criar documento PR/CL.

**SINTOMA:** erro de template, pasta nao encontrada ou sem permissao.

**CAUSA PROVAVEL:** `PRTemplate`, `CLTemplate`, `PRDirectory` ou `CLDirectory` incorretos, arquivo Excel bloqueado, falta de permissao, template removido.

**COMO RESOLVER:**

1. Confirmar os templates em `C:\TeamOps\Modelos` ou, antes do primeiro uso, em `Modelos` ao lado do executavel publicado.
2. Confirmar permissao de escrita nas pastas `PR` e `CL`.
3. Fechar arquivos Excel abertos que possam estar bloqueando.
4. Restaurar template padrao se foi alterado/removido.

**COMO PREVENIR:** copiar o pacote completo, incluindo `Modelos`, controlar alteracoes nos templates personalizados e manter backup dos modelos.

## Anexos do Hikitsugui nao abrem

**PROBLEMA:** anexos nao sao salvos, listados ou abertos.

**SINTOMA:** link sem efeito, arquivo nao encontrado ou erro de permissao.

**CAUSA PROVAVEL:** `HikitsuguiAttachmentPath` inacessivel, anexo movido, permissao insuficiente, caminho gravado antigo.

**COMO RESOLVER:**

1. Conferir a pasta de anexos.
2. Verificar se o arquivo existe fisicamente.
3. Validar permissao de leitura/escrita.
4. Reanexar o arquivo quando o original foi movido.

**COMO PREVENIR:** nao mover anexos manualmente e usar pasta compartilhada estavel.

## Acesso negado

**PROBLEMA:** usuario nao consegue abrir um modulo.

**SINTOMA:** mensagem "Acesso negado. Permissao insuficiente."

**CAUSA PROVAVEL:** nivel de acesso abaixo do exigido ou cadastro de usuario desatualizado.

**COMO RESOLVER:**

1. Solicitar revisao do nivel de acesso ao administrador.
2. Confirmar se o usuario esta associado ao operador correto.
3. Sair e entrar novamente apos alteracao de permissao.

**COMO PREVENIR:** revisar acessos periodicamente e remover permissoes antigas.
