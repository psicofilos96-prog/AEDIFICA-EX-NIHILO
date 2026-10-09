# E2c.2 — Chunks visuais e comparação controlada

## Arquitetura

`VisualChunkManager` é uma camada experimental separada de `ConstructionWorld` e da cena ConstructionLab. Cada `PieceData` mantém um ID e uma única `PieceView` real. O gerenciador registra essa view em células XZ de tamanho configurável (64 m por padrão), inclusive em coordenadas negativas e nas células atravessadas por peças longas/rotacionadas. A view é agrupada sob um GameObject do chunk de âncora; múltiplas referências de célula não criam cópias. `Register`, `Update` e `Remove` usam o ID; `Update` recalcula limites após movimento ou redimensionamento; células vazias são descartadas. O mundo lógico continua sendo a fonte de verdade.

Para culling, cada célula mantém uma AABB que contém **os limites completos** de todas as suas peças. `GeometryUtility.TestPlanesAABB` verifica o frustum; uma peça só perde seu `MeshRenderer.enabled` quando nenhuma das suas células está ativa. Uma célula permanece ativa por 0,2 s após deixar o frustum, reduzindo alternância durante pequenas oscilações. A AABB completa pode manter peças fora de visão ativas: é uma decisão conservadora para evitar desaparecimento. GameObjects, malhas e colliders permanecem instanciados; a camada não reduz custo de memória das views, nem desativa física. Seleção e Snap de produção não são modificados. Integrar esse culling à ConstructionLab exigirá política explícita para permitir seleção de peças ocultas via collider; essa integração **não faz parte** da medição E2c.2.

## Comparação e métricas

O runner reutiliza `VisualBenchmarkScenario.PieceAt` da E2c.1: seed 2301, IDs, posições, geometrias e materiais idênticos. Mesma câmera fixa (0,160,-240), mesmo percurso circular (raio 240, altura 160), FOV 60°, iluminação, resolução alvo 1920×1080 e limites padrão de geração/memória/captura da E2c.1. Um terceiro modo `overview` posiciona a câmera em (0,1200,-900), olhando a origem, para expor o custo quando grande parte do conjunto entra no frustum. Ele não substitui os modos comparáveis à E2c.1.

Por cenário V1–V5, o runner cria as peças progressivamente, em lotes de 32; a geração é separada das capturas. Para cada repetição, alterna a ordem das três condições de layout:

| Repetição | Ordem |
|---|---|
| 1 | baseline, chunked_no_culling, chunked_culling |
| 2 | chunked_culling, baseline, chunked_no_culling |
| 3 | chunked_no_culling, chunked_culling, baseline |

`baseline` restaura a hierarquia E2c.1, sem gerenciador. `chunked_no_culling` cria a hierarquia e mantém todos os renderizadores ligados. `chunked_culling` usa a mesma hierarquia e avalia o frustum a cada frame. A troca de layout ocorre **fora** da captura e seu custo é reportado separadamente em `chunk_build_ms`; o benchmark não confunde essa construção com FPS. Cada layout executa `fixed`, `path` e `overview`, após 15 s de aquecimento e por 30 s de captura, três repetições. Uma execução integral gera 135 linhas `ok`. O tempo total pode ultrapassar 100 minutos; não reduza silenciosamente cenários ou durações para obter números melhores.

O CSV `aedifica_E2c2_chunks_*.csv` no `Application.persistentDataPath` mantém as colunas E2c.1 e adiciona `layout_mode`, renderizadores existentes, chunks existentes/ativos/visíveis/descartados, custo de criação da hierarquia, média de avaliação de visibilidade, número de avaliações e mudanças de estado. Contagens de renderizadores ativos/visíveis e chunks são snapshots após o aquecimento, no começo da captura; no percurso podem variar durante a captura. `chunks_visible` é `unavailable` quando não há avaliação do frustum (`baseline` e `chunked_no_culling`). `chunk_state_changes` mede somente a captura. `visibility_eval_mean_ms` mede a chamada de culling no thread principal; inclui seu próprio custo de cronômetro. Contadores CPU/GPU/draw calls/batches podem aparecer como `unavailable`; zero não é substituto. FPS e percentis usam o mesmo cálculo E2c.1. Uma falha de configuração, memória, geração, montagem de layout ou captura produz `failed` e marca capturas posteriores como `skipped`, preservando os tamanhos definidos.

## Execução Windows e homologação

1. Obter a branch após autorização de publicação, confirmar o SHA, abrir no Unity **6000.6.4f1** e executar EditMode e PlayMode completos. Conferir Console.
2. Usar **Tools > Aedifica > E2c.2 > Create Visual Benchmark Scene**. O comando gera localmente `Assets/Game/Scenes/VisualBenchmarkE2c2.unity`, aponta materiais URP existentes e a coloca como primeira cena habilitada no Build Settings. Revisar antes da build. A cena não é gerada nem versionada artificialmente no ambiente remoto.
3. Criar build Windows x64 Release, Development Build desligado. Executar no Lenovo LOQ em 1920×1080, sem VSync ou limitador, com condições de energia e GPU anotadas. Conferir resolução real, VSync, qualidade e limite de FPS no CSV; não mudar a qualidade entre os modos.
4. No PowerShell, dentro do repositório, executar `$env:AEDIFICA_COMMIT = (git rev-parse HEAD).Trim()` e iniciar o executável **pelo mesmo terminal**. Arquivar CSV e `Player.log` em `Application.persistentDataPath` (normalmente `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`). Procurar `scenario begin`, `layout prepared`, `capture begin/complete`, `failed` e `visual benchmark complete`.
5. Para cada cenário e câmera, comparar baseline, chunked_no_culling e chunked_culling **da mesma execução**, considerando as três repetições, FPS p01, frame p95/p99, memória, draw calls, renderizadores ativos/visíveis, `chunk_build_ms` e `visibility_eval_mean_ms`. Comparar `overview` para estimar o overhead quando o culling poupa pouco. Se um cenário parar por limite, não extrapolar resultados ausentes. Repetir a build em outra sessão para verificar variância térmica.

## Riscos e critérios

O frustum é conservador e pode deixar renderizadores extras ativos; o trabalho de testar chunks e atualizar renderizadores pode custar mais que economiza, sobretudo em `overview`. Hierarquia de GameObjects isolada não implica melhoria de FPS. `Renderer.isVisible` é snapshot e pode incluir outras câmeras da cena; usar a cena gerada com uma câmera. A proteção de memória acompanha heap gerenciado e memória Unity reservada, mas não cobre toda VRAM ou memória nativa; o sistema operacional pode encerrar o Player antes de registrar erro. V5 só é tentado enquanto os limites permitem.

A E2c.2 só estará homologada depois de compilação, testes Unity, verificação visual/lógica e CSV/Player.log válidos no Windows. A implementação remota não fornece FPS nem comprova ganhos. Nenhuma otimização de renderização posterior é inferida automaticamente destes resultados.
