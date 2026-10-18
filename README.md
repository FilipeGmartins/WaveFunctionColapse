# Wave Function Collapse — simulador biológico 2D

Biblioteca C# / .NET 8 para geração procedural por WFC e futura simulação celular evolutiva.

## Estado atual

`Core`, `Simulation`, `Evolution` e `Visual` estão implementados. O `Runner` executa gerações headless,
e `Visual` abre uma janela Raylib-cs com a mesma simulação, logs e adaptações entre runs.
Por padrão, cada geração tem limite de 300 segundos simulados.

## Compilar e executar

Requer o **SDK .NET 8**, não apenas o runtime.

```powershell
dotnet build WaveFunctionCollapse.sln -c Release
dotnet run --project tests/Core.Tests -c Release
dotnet run --project tests/Simulation.Tests -c Release
dotnet run --project tests/Evolution.Tests -c Release
dotnet run --project tests/Visual.Tests -c Release
dotnet run --project src/Runner -c Release -- 42
dotnet run --project src/Visual -c Release -- 42 --generations 10
dotnet run --project src/Runner -c Release -- 42 --map-only
dotnet run --project src/Runner -c Release -- 42 --generations 10 --primitive
dotnet run --project src/Runner -c Release -- 42 --generations 10 --steps 10000 --log artifacts/experiment/deaths.jsonl
```

Neste workspace o SDK foi instalado localmente em `.tools/dotnet` (ignorado pelo Git).
Se ele não estiver no PATH, substitua `dotnet` por `& .\.tools\dotnet\dotnet.exe` nos comandos.
Os testes são um executável de regressão sem pacotes externos; devem ser executados com `dotnet run`,
e retornam código diferente de zero quando uma asserção falha.

## Contratos do Core

- `TileDefinition`: nome único, peso positivo e sockets Norte/Leste/Sul/Oeste imutáveis.
- `BiologicalTile`: glicose, lipídios, concentração e tipo de toxina, densidade e temperatura inicial em °C.
  As demais grandezas usam unidades normalizadas neste estágio; taxas metabólicas serão definidas em Simulation.
  As definições são compartilháveis; recursos consumidos por posição ficarão no estado mutável do ambiente.
- `TileCatalog`: IDs densos pela ordem de entrada, de 0 a 63, compatibilidades pré-calculadas.
  Sockets de mesmo ID em faces opostas permitem adjacência. Rotações devem ser cadastradas explicitamente.
- `WaveGrid`: matriz linear por linhas, com um `ulong` por posição; a API pública expõe apenas leitura.
- `ShannonEntropy`: entropia ponderada, usando logaritmo natural.
- `WfcSolver`: observação por menor entropia, amostragem por peso, propagação em fila, seed e cancelamento.
- `WfcResult`: sucesso ou contradição, seed, tentativas usadas e coordenadas da falha.

As bordas são abertas, sem vizinhos externos nem conexão entre lados opostos.
Pins permitem fixar tiles antes da primeira propagação. O solver verifica todos os domínios inicialmente,
inclusive em catálogos com apenas um tile.

O solver tenta no máximo `maxAttempts` mapas, usando uma sequência aleatória contínua a partir da seed.
Não há backtracking: uma contradição no resultado indica que as tentativas se esgotaram,
e não prova que o problema é insolúvel. O grid retornado nesse caso é a última tentativa parcial e não
deve ser usado como mapa biológico válido. Reproduza uma geração com o mesmo catálogo, ordem, pesos,
dimensões, pins, limite de tentativas, seed e versão do runtime.

Pesos são tendências locais de seleção, não garantias de proporção final de tiles.
O catálogo normaliza pesos para reduzir overflow numérico. A seleção de entropia faz uma varredura
do grid por colapso; otimizações e metas de gerações por segundo dependerão de benchmarks.

## Contratos da Simulation

- `BiologicalEnvironment` exige um WFC bem-sucedido e o mesmo catálogo usado na geração.
  Copia glicose e lipídios para arrays por posição. Novos ambientes iniciam recursos independentes.
  Toxinas e temperaturas são estáticas nesta versão; não há regeneração de recursos.
  Um ambiente deve pertencer a uma única execução; não compartilhe a instância entre runs paralelas.
- `CellGenome` copia a coleção de partes, mantém parâmetros imutáveis e pré-calcula capacidades agregadas.
  `CellAgent` expõe estado somente para leitura fora do assembly de simulação.
- `SimulationWorld` controla uma célula e avança um passo fixo por `Step(intent)`.
  Não usa relógio real nem aleatoriedade interna. `SimulationSeed` identifica a seed da política externa;
  a sequência de comandos, o genoma, o mundo e o passo também são necessários para reprodução.
- `Observe()` retorna amostras em um raio Manhattan. Sem receptor, somente o piso atual é percebido.
  A política de decisão reside no Runner, separada da física e pronta para substituição por entrada do usuário.
- `CellDied` emite um `CellDeathEvent` uma única vez. O snapshot inclui causa, posição contínua e tile,
  ATP, déficit do passo e acumulado, toxina fatal, temperatura, custo de manutenção, genoma e dados da execução.
  O evento é síncrono; os consumidores devem tratar suas próprias falhas de persistência.
  Um mundo morto para de avançar e conserva seu último snapshot em `Death`.

### Modelo metabólico inicial

As taxas usam segundos simulados e unidades abstratas de ATP/HP; são parâmetros de jogo,
não estimativas bioquímicas. Uma unidade de glicose fornece 4 ATP, uma de lipídio 9 ATP.
A boca limita a absorção por segundo, consome glicose antes de lipídios e respeita a capacidade de ATP.
Sem boca não há absorção.

Cada passo absorve no piso atual, paga custo basal e manutenção de partes e aplica dano de inanição.
Se sobreviver, move e recebe dano de toxina e, depois, temperatura no destino. A causa mortis é o primeiro
dano que zera HP nessa ordem; exposição ambiental permanece no snapshot mesmo se não for a causa fatal.

Flagelos somam velocidade e custo de ATP por unidade de distância; a densidade multiplica o custo
e divide a velocidade por `1 + densidade`. O movimento respeita ATP disponível, bordas e um máximo
de uma unidade de grid por passo para evitar saltar tiles perigosos. Choques com a borda cobram somente
a distância efetiva. O passo permitido é maior que zero e no máximo um segundo.

Bocas, flagelos e receptores cobram manutenção contínua, mesmo quando o agente está parado.
Adicionar partes pode melhorar alimentação, mobilidade e decisões, mas também acelerar mortes
por falta de ATP; o snapshot permite associar essas mortes ao genoma e à manutenção.
Resistência a toxinas reduz linearmente o dano entre 0 e 1. Temperatura fora da faixa ideal ± tolerância
gera dano proporcional ao excesso. Não há cura, reprodução ou adaptação durante a run neste módulo.

O exemplo termina por morte ou limite de tempo; sobreviver ao limite não gera uma morte artificial.

## Evolution e Death Analytics

`DeathLogger` escreve um registro JSON por linha, em UTF-8 e schema versão 1. O registro contém
ID da execução, fingerprint SHA-256 do catálogo ordenado (incluindo pesos e propriedades biológicas),
passo de tempo e o evento completo de morte. O codec explícito do genoma preserva os parâmetros
de cada boca, flagelo e receptor por um discriminador `kind`; partes novas exigem um codec correspondente.
Não há referência de Raylib nem escrita em disco dentro de `Simulation`.

Cada `Append` serializa antes de escrever e descarrega o buffer de texto. O logger permite um escritor
por arquivo e leitores simultâneos, sem prometer transações ou durabilidade contra falta de energia.
Um registro inválido é recusado; linhas corrompidas ou schemas desconhecidos geram erro com o número da linha.
Um arquivo sem quebra de linha final é recusado ao abrir para append e deve ser reparado antes de continuar.
Falhas de escrita interrompem o Runner, evitando evolução silenciosa sem o registro da morte.

`GenomeEvolver.Evolve` recebe um lote de registros já carregados. `EvolveFromLog` lê JSONL e seleciona
execução e geração. O catálogo, os pesos e o genoma pai precisam corresponder aos registros do lote;
isso evita usar IDs de tiles de outra versão ou misturar fenótipos inadvertidamente. A frequência de cada
causa no lote controla a intensidade das correções:

- Inanição: acrescenta boca e mobilidade quando ausentes, melhora a vazão de bocas existentes,
  aumenta reserva de ATP e reduz custo basal. As bocas melhoradas também aumentam manutenção.
  Glicose e lipídios sem toxinas recebem aumento relativo de peso no WFC.
- Toxina: aumenta resistência geral e acrescenta percepção se ausente; a resistência aumenta o custo basal.
  O ecossistema reduz pesos dos tiles que contêm o tipo de toxina fatal. A resistência atual é geral,
  e não específica para uma substância.
- Temperatura: amplia a tolerância térmica com aumento de custo basal e reduz o peso dos tiles fatais.

As correções têm tetos para os parâmetros que aumentam; valores iniciais já acima dos tetos são preservados.
Os pesos são normalizados e recebem um piso positivo, conservando IDs, sockets e condições ambientais.
Não se remove um tipo de tile. Genomas e catálogos anteriores permanecem imutáveis.

Esta versão usa **mutações corretivas determinísticas**, sem população, crossover, seleção por fitness
ou garantia de melhoria de sobrevivência. Adaptações protetoras e novas partes podem elevar o gasto de ATP
e gerar mortes por outra causa. Esse efeito será mensurável nos logs das próximas gerações.

### Execução em gerações

`--generations N` define quantas runs serão tentadas; `--steps N` define o máximo de passos por run.
O passo é de 0,1 s. `--primitive` inicia sem partes para observar uma morte por inanição e a primeira correção.
`--map-only` desenha o mapa ASCII e termina sem criar logs.

O Runner assina `CellDied`, persiste o registro e entrega o mesmo snapshot ao evolver sem reler todo o
arquivo em cada geração. Cada nova run recebe outro mapa, sementes registradas e recursos restaurados.
Sobreviventes ao limite não geram uma morte nem uma mutação; falhas de WFC também são registradas
separadamente e não contam como mortes biológicas. Um WFC que falha deixa o processo com código de saída 1.

Por padrão, os arquivos ficam em `artifacts/<runId>/` (ignorado pelo Git):

- `deaths.jsonl`: somente mortes, com genomas completos e métricas ambientais.
- `deaths.jsonl.generations.jsonl`: resultados de cada geração, incluindo sobrevivência, sementes,
  pesos e genomas antes/depois, além das correções aplicadas.
- `deaths.jsonl.<runId>.manifest.json`: configuração, catálogo e genoma iniciais, versões das políticas,
  runtime, dimensões, spawn e pins para reproduzir o experimento.

Com `--log`, os mesmos sufixos são usados no caminho escolhido. Execuções novas recebem um novo `runId`
e acrescentam registros aos arquivos existentes; não retomam automaticamente uma execução interrompida.
Reprodução exige o mesmo código/runtime e as configurações registradas; o ID aleatório da execução não influencia a simulação.

## Visualizador Raylib-cs

Para abrir a janela neste workspace:

```powershell
& .\.tools\dotnet\dotnet.exe run --project src/Visual -c Release -- 42 --generations 10
```

Acrescente `--primitive` para iniciar sem partes e acompanhar as primeiras correções após inanição.
O pacote Raylib-cs 8.1.0 e suas bibliotecas nativas são restaurados pelo NuGet. O cache local de pacotes
fica em `.tools/nuget`, definido em `NuGet.Config`. A janela requer um ambiente desktop com OpenGL.

A interface mostra o mapa e seus nutrientes remanescentes, a célula com boca/flagelo/receptor,
percepção química, ATP, HP, manutenção, déficit energético, genoma e causa da morte.
Passe o mouse sobre um tile para inspecionar nutrientes, concentração de toxina, temperatura e densidade.
Sem hover sobre o mapa, o painel mostra o piso atual da célula.

- **Espaço** ou botão Pausar: pausa/retoma.
- **N** ou botão Passo: pausa e executa exatamente um passo de 0,1 s.
- **+ / -** ou botões: altera a velocidade entre 0,25x e 32x, preservando o passo da simulação.
- **A**: alterna entre decisões automáticas e controle manual pelas setas; sem seta, a célula fica parada.
- **H**: exibe/oculta a área percebida.
- **Enter** ou botão Próxima: avança após morte ou limite de tempo; na última geração, conclui.
- **G**: ativa/desativa o avanço automático entre gerações concluídas (desligado por padrão).
- **Esc** ou fechar a janela: encerra a execução. Uma run interrompida aparece como `UserStopped`, sem morte artificial.

A janela redimensiona o mapa, respeitando o tamanho mínimo de 1100 × 760. O tempo real só controla
quantos passos fixos são executados por frame. Pausa e velocidade não alteram o metabolismo por segundo simulado.
Longas interrupções do sistema têm recuperação limitada para manter a interface responsiva.

### Separação entre aplicação e renderização

`Application` contém catálogo de demonstração, configuração, política de busca por alimento e a composição
do ciclo evolutivo. Depende de `Evolution` e, transitivamente, de `Simulation` e `Core`; não conhece Raylib.
`Runner` é uma entrada headless pequena para essa aplicação. `Visual` é outra entrada e implementa
`ISimulationDriver`, agendando os passos e desenhando o estado. Somente `Visual` referencia Raylib-cs.
`GridRenderer` e `CellRenderer` não alteram o agente nem o ambiente.

Os arquivos de mortes, resultados e manifesto também são produzidos no modo gráfico. A entrada manual
e as alternâncias de controle são registradas por geração e tick em `deaths.jsonl.inputs.jsonl`.
Esse histórico é necessário para reproduzir uma execução controlada pelo usuário; não há leitor de replay manual nesta versão.

### Verificação gráfica

```powershell
dotnet run --project src/Visual -c Release -- 42 --smoke-test --capture artifacts/visual-preview.png
```

Esse comando cria uma janela oculta, executa 16 passos, renderiza, salva um PNG e encerra.
Ainda precisa de OpenGL; a verificação automática dos testes de relógio/layout e integração não abre janela.
Para capturar durante uma execução normal, forneça `--capture caminho.png` e pressione **F12**.
