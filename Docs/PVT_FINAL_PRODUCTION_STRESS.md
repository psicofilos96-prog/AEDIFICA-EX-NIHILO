# PVT-FINAL — fatia arquitetônica e teste de ruptura

**Estado:** implementação para homologação. Sem execução Unity/Windows ou parecer de viabilidade neste commit. A PVT-1, a PVT-2 e os benchmarks E1/E2 não são alterados nem substituídos. O critério oficial permanece na [Issue #1](https://github.com/psicofilos96-prog/AEDIFICA-EX-NIHILO/issues/1).

## Escopo fechado e composição

A fonte lógica é `PvtFinalScenario`, com seed 7319 e IDs estáveis. Cada edifício simples possui 10 peças, médio 30 e complexo 80; não confundir edifícios com peças. Todos usam `PieceData`, `ConstructionWorld` e `PvtChunkVisualEngine` (páginas por região/material com colisores e mapeamento de triângulos para `PieceId`). Nenhum edifício é apenas um prefab ineditável. Há paredes com passagem/janelas, pisos, escadas, arcos, colunas, vigas, parapeitos e telhados inclinados; complexos usam também cúpulas/abóbadas pequenas. As coberturas são peças manuais independentes, nunca telhados automáticos obrigatórios. A variação de yaw, posição, telhado e alvenaria é determinística. O kit não constitui ainda arte final.

| Cenário | Simples | Médios | Complexos | Edifícios | Peças |
| --- | ---: | ---: | ---: | ---: | ---: |
| A | 300 | 100 | 50 | 450 | 10.000 |
| B | 500 | 400 | 100 | 1.000 | 25.000 |
| C | 1.000 | 800 | 200 | 2.000 | 50.000 |
| D60 | 1.000 | 800 | 325 | 2.125 | 60.000 |
| D70 | 1.000 | 800 | 450 | 2.250 | 70.000 |
| D80 | 1.000 | 800 | 575 | 2.375 | 80.000 |

D progride de dez em dez mil peças e para depois do primeiro alvo com média inferior a 60 FPS em qualquer uma das 12 capturas (4 câmeras × 3 repetições). Todas as capturas do alvo de ruptura terminam antes da parada. Os alvos posteriores recebem `skipped` com motivo. Se D80 não romper, o limite é **censurado em 80 mil peças**; não extrapolar. O limite de 4 GB de memória reservada e o prazo de 1.200 s por construção impedem consumo descontrolado e geram falha explícita. Uma falha por memória/timeout também caracteriza limite operacional, não `ok`.

## Materiais e representatividade

`Tools > Aedifica > PVT-FINAL > Create Production Stress Scene` gera, via `PvtFinalMaterialBuilder`, texturas 256² de albedo e normal e cinco materiais URP/Lit persistentes para pedra, tijolo, madeira, reboco e cerâmica. A geração é determinística. As texturas criadas ficam em `Assets/Game/Materials/PvtFinalGenerated` na cópia Windows; guardá-las com a cena e registrar SHA256. Materiais existentes nessa pasta não são sobrescritos silenciosamente. Os materiais PVT-2 persistentes continuam a servir terreno, água, estrada e vegetação; o terreno usa URP Terrain/Lit. A cena liga sombras suaves e câmera de 60°. Não há asset comercial no repositório nem foi medido custo de asset externo.

Os materiais procedurais fornecem custo de shader, mapas e variedade, mas a qualidade **não pode ser atestada por código**. Conferir capturas em street, fixed e overview. Se parecerem repetitivos ou irreais, a prova visual é inconclusiva, mesmo com FPS alto. O manifesto registra vértices/triângulos de uma amostra de cada tipo, e o CSV registra vértices e triângulos totais de páginas combinadas, edifícios e peças. Draw calls e memória **por categoria** não são separáveis de páginas compartilhadas, logo ficam `unavailable`, não zero. Draw calls totais vêm do ProfilerRecorder quando válidos.

## Capturas e custo simultâneo

Cada alvo cria o mundo lógico do zero e páginas visuais em lotes de 64 peças e uma região por frame. Os tempos de criação incluem a distribuição ao longo dos frames e ficam separados do FPS de gameplay. Captura 4 câmeras (fixed, rua, panorama e percurso) em três repetições por alvo, com 20 s de aquecimento e 30 s de coleta por captura. Movimento no percurso atravessa regiões de vegetação e volta à área inicial em cada ciclo. Durante cada captura, 15 operações reversíveis testam seleção física, extração de `PieceView`, criação, move, resize, troca de material, delete, undo/redo e reconstrução localizada. Uma edição real de heightmap/collider ocorre após as operações; na primeira captura fixa, uma amostra de terreno alterada é gravada, revertida e recarregada de arquivo. O snapshot das peças é salvo/carregado e validado contra IDs/dimensões/materiais/aberturas iniciais. É um ensaio de **uma amostra de terreno**, não uma implementação completa de edição/persistência territorial.

Cada sessão escreve `*_frames.csv`, `*_events.csv`, `*_manifest.txt`, capturas `*_{fixed,street,overview,path}.png` de cada alvo executado e snapshots `.pvt2` mais `.terrain-sample` em `Application.persistentDataPath`. `Player.log` deve ser copiado junto. O CSV de eventos inclui bytes gerenciados alocados na thread da operação de edição; eventos sem leitura confiável usam `unavailable`. No CSV de frames, `fps_p01 = 1000 / p99(frame_ms)`. FPS do Editor e dados com Profiler ativo não entram nos gates Release. `process_rss_mb` só aparece quando a API retorna valor positivo; VRAM usada, temperatura, clocks, jobs e custo de Snap contínuo são `unavailable` neste runner. O manifesto informa CPU/GPU/capacidades, resolução, VSync, frame cap, qualidade, seed e SHA informado por ambiente. Resultados `failed`/`skipped` não podem ser tratados como sucesso.

## Lacunas que impedem aprovação automática

- Não existe LOD arquitetônico nem HLOD nesta fatia; o motor combinado usa frustum culling padrão de renderizadores, sem streaming das páginas arquitetônicas.
- O terreno é 1 × 1 km. Não há simulação equivalente de 4 × 4 km com cidades regionais persistidas; **expansão 4 × 4 km pendente**.
- A vegetação ainda usa malha procedural simples da PVT-2 e pode subestimar assets finais. Água é opaca e não há pós-processamento/antialiasing configurado especificamente para arte final. Esses itens devem constar da avaliação de fidelidade.
- Snapshot de terreno persiste somente uma amostra editada. A edição completa do terreno, save de múltiplas regiões e streaming de arquitetura ainda não são representados.
- Não há medição confiável de VRAM usada, custo financeiro, GPU clocks ou temperatura. Coletar externamente quando possível; registrar `unavailable` caso contrário.

**Consequência:** se essas lacunas forem relevantes ao produto final, o parecer global deve ser `INCONCLUSIVO`, mesmo que A/B/C e D atinjam FPS. Uma falha clara de FPS ou integridade pode reprovar o escopo medido; um sucesso parcial não aprova o jogo completo.

## Execução no Lenovo LOQ

1. Preserve mudanças locais e stashes. Obtenha `origin/pvt/final-production-stress` em uma cópia/worktree limpa. Abra no Unity 6000.6.4f1 com URP 17.6.0, aguarde compilação e rode **EditMode > Run All** e **PlayMode > Run All**. Se algum teste falhar, não gere build de homologação.
2. Use **Tools > Aedifica > PVT-FINAL > Create Production Stress Scene**. O menu se recusa a sobrescrever cena existente. Inspecione materiais/texturas gerados, sombras e a prévia A em Play. A cena deve estar em primeiro lugar no Build Settings; a checagem pré-build exige PC_RPAsset e referências dos materiais.
3. Gere **Windows x64 Release**, Development Build e Autoconnect Profiler desativados, Direct3D 11, 1920 × 1080. Use notebook na tomada, RTX 3050 e plano de energia registrados. Desative VSync e limitador de FPS para a sessão; registre todos os ajustes de qualidade. Feche o Editor antes da medição. Não altere qualidade entre sessões.
4. Em PowerShell, na cópia do código usada para a build, execute três **processos independentes**, preservando a pasta de resultados de cada um:

```powershell
cd 'C:\Projetos\AEDIFICA-PVT-FINAL'
$env:AEDIFICA_COMMIT = (git rev-parse HEAD).Trim()
$env:AEDIFICA_WORKTREE = if (@(git status --porcelain).Count -eq 0) { 'clean' } else { 'dirty' }
1..3 | ForEach-Object {
    & 'C:\CAMINHO\BUILD\AEDIFICA-EX-NIHILO.exe' -force-d3d11 -screen-width 1920 -screen-height 1080 -pvt-final-run
}
```

5. Em `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`, arquive cada sessão separadamente: CSVs, manifesto, PNGs, `.pvt2`, `.terrain-sample`, `Player.log`, hash do executável, hash da cena, hash dos materiais gerados. Não misturar sessões. Confirmar dados A/B/C e D, inclusive linhas `skipped`; investigar qualquer `failed`, imagem magenta, peça ausente, lacuna visual ou erro fatal.

Uma sessão completa sem ruptura produz 72 linhas de dados (6 alvos × 3 × 4). Se D romper, linhas posteriores são `skipped` e o manifesto registra `rupture_at`. Repetições internas não substituem as três sessões independentes. Revisar as quatro capturas de cada alvo; tirar capturas adicionais se houver artefatos. Separar métricas com Profiler de métricas Release.

## Gates e decisão

Antes de olhar resultados: A requer média ≥80 e p01 ≥60; B média ≥60 e p01 ≥50; C média ≥60 (55 apenas como piso extremo sinalizado) e p01 ≥45. Para aprovação estrita da meta original, p01 ≥55 na cidade grande e investigação de outliers. Avaliar cada câmera e as três sessões, não apenas a melhor linha. Travamentos repetidos, falha de save/integridade, material rosa ou visual inadequado bloqueiam aprovação. D identifica a carga de ruptura dentro dos limites testados. `VIÁVEL` exige fidelidade e sistemas essenciais representados, gates e estabilidade repetidos. `VIÁVEL COM RESTRIÇÕES` exige limites quantitativos baseados nas medições. `INVIÁVEL NA CONFIGURAÇÃO ATUAL` exige falha persistente após otimização razoável. Caso as lacunas acima permaneçam, usar `INCONCLUSIVO` e identificar o trabalho/medição faltante. Não atribuir valores de FPS ou custos sem execução real.
