# Marco Visual da Prova Definitiva — candidato à revisão humana

**Estado:** assets CC0, cidade paramétrica e ferramenta de inspeção implementados na branch isolada. A Unity 6000.6.4f1 não está disponível neste ambiente; compilação, EditMode, PlayMode e aparência na Game View **não foram homologados**. Este marco não executa A/B/C nem as três sessões Windows. Os gates da [Issue #1](https://github.com/psicofilos96-prog/AEDIFICA-EX-NIHILO/issues/1) permanecem inalterados.

## Recursos incorporados

Os mapas 1K e modelos derivados estão em `Assets/Game/Art/PolyHaven`. A origem, a URL direta de cada mapa, MD5 fornecido pela API e SHA-256 do arquivo incorporado estão em `Docs/PVT_VISUAL_ASSET_MANIFEST.csv`. Todos os assets escolhidos constavam na API da Poly Haven e estão sob a [licença CC0 declarada pelo fornecedor](https://polyhaven.com/license). Os FBX de porta, portão e estátua foram convertidos pelo Blender 4.3.2. O pinheiro original tinha aproximadamente 400 mil triângulos nas três variantes; apenas uma variante foi usada como origem e reduzida a três níveis. Os FBX originais de alta densidade não entram no projeto. `Tools/build_visual_lods.py` documenta a derivação; `Tools/pack_visual_masks.py` recompõe máscaras metálico/smoothness e albedo com alfa. Os derivados estão incluídos na branch, de modo que Blender e Pillow não são necessários para abrir a cena.

| Papel | ID Poly Haven | Resolução importada | Material URP |
| --- | --- | --- | --- |
| Pedra | `old_stone_wall` | 1K | Stone |
| Tijolo | `brick_wall_001` | 1K | Brick |
| Madeira | `oak_wood_planks` | 1K | Wood |
| Reboco | `clay_plaster` | 1K | Plaster |
| Telha | `clay_roof_tiles` | 1K | Roof |
| Pavimento | `cobblestone_floor_01` | 1K | Road |
| Solo | `forest_ground_04` | 1K | Ground/TerrainLayer |
| Pinheiro | `pine_sapling_small` | 1K | PineBark/PineTwig, LOD 0/1/2 |
| Arbusto | `shrub_03` | 1K | Shrub, LOD 0/1/2 |
| Porta | `large_castle_door` | 1K | Door |
| Portão | `large_iron_gate` | 1K | Gate |
| Estátua | `gothic_statue` | 1K | Statue |

As texturas de normal são importadas como NormalMap, sRGB desligado; máscaras metálico/smoothness são lineares, com smoothness no alfa. Os materiais usam URP/Lit persistente, mapas compartilhados, mipmaps e limite de importação 1024. A folhagem utiliza recorte alfa, duas faces e LODGroup. A cena gera os materiais e prefabs em `Assets/Game/Art/VisualMaterials` e `Assets/Game/Art/VisualPrefabs`. Se esses arquivos já existirem com propriedades divergentes, o menu interrompe a operação; não há substituição silenciosa. Água e vidro usam URP/Lit transparente sem mapas externos; sua adequação visual depende de inspeção.

## Cidade e editabilidade

O estilo proposto é uma cidade histórica de alvenaria, reboco claro, telhas de barro, madeira, rua de pedra, pinheiros e uma praça monumental. `PvtVisualCityScenario` distribui 6 residências simples, 4 médias, 2 complexas e 1 edifício monumental. As fachadas têm passagens e janelas; molduras e painéis de vidro são peças independentes, com `PieceId` próprio. O monumento combina pisos, paredes, arcos, colunas, escadas, abóbadas e cúpula. A semente 7319 e os IDs são estáveis. Os edifícios não são prefabs indivisíveis. A porta FBX acompanha uma abertura na parede monumental; portão e estátua são **adornos ambientais**, não novas peças paramétricas independentes.

O `ConstructionWorld` mantém o estado lógico. `PvtChunkVisualEngine` usa páginas por região/material e mapeia triângulos para `PieceId`; a edição extrai uma `PieceView`, depois reconstrói as regiões alteradas. A malha combinada e a `PieceView` calculam tangentes. Roof/curved/circulation usam projeção UV conforme o plano dominante da face, evitando UV colapsada em superfícies verticais. A cena oferece controles de inspeção pelo histórico reversível e salva o conjunto de peças com `Pvt2Snapshot`. O paisagismo da cena usa modelos CC0 com LOD carregados por células; os benchmarks PVT-2 e PVT-Final antigos continuam separados.

## Abrir e revisar no Windows

1. Preserve alterações locais e stashes da cópia Windows. Em uma cópia ou worktree limpo da branch publicada, abra com Unity **6000.6.4f1**, URP **17.6.0**. Aguarde o import dos binários. Nenhuma dependência de asset externo precisa ser baixada durante a abertura.
2. Use **Tools → Aedifica → Visual Milestone → Create Historic City Review Scene**. O comando prepara materiais e prefabs, cria `Assets/Game/Scenes/PvtVisualCity.unity` e a inclui no Build Settings. Ele se recusa a sobrescrever uma cena existente.
3. Confira no Inspector os materiais `PVT Visual Stone/Brick/Wood/Plaster/Roof/Road`, normal maps, árvores LOD e sombra. Execute **EditMode → Run All** e **PlayMode → Run All**. Qualquer erro de compilação, import, shader magenta ou teste falho bloqueia a revisão visual.
4. Abra `PvtVisualCity.unity`, inicie Play na aba Game. Os botões `facade`, `materials`, `street`, `landscape` e `monument` fixam cinco avaliações. `Capture 5 views` grava cinco PNGs em `Application.persistentDataPath` (normalmente `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`). Copie também `Player.log` e informe a resolução da Game View.
5. Clique numa peça para selecionar. Teste `Move +X`, `Resize +X`, `Change material`, `Delete`, `Undo`, `Redo`, `Add block`, `Save` e `Load`. Confirme que apenas a peça escolhida muda e que as páginas vizinhas não desaparecem. O arquivo de save é `aedifica_visual_city.pvt2` na mesma pasta persistente.
6. Envie as cinco capturas, os totais dos testes, avisos do Console e sua avaliação da qualidade da fachada, telhas, vegetação, terreno e monumento. **Somente após aprovação visual humana** a cidade deve ser usada como referência para os cenários de escala.

## Limites explícitos do marco

- Não há captura real da Unity nem validação visual neste ambiente. Qualidade realista, orientação dos FBX, normal maps e transparência da água precisam ser julgados na Game View Windows. As cinco capturas são produzidas pela Unity local, não por este repositório.
- A cidade é um recorte pequeno de inspeção. O terreno mantém 1×1 km e altura 257²; não demonstra streaming arquitetônico de 4×4 km, LOD arquitetônico ou custo de 50 mil peças.
- O LOD do pinheiro foi derivado por redução de malha, sem atlas/impostor. Confirmar contorno, alpha clipping e custo de sombra antes de repetição em escala.
- Porta e portão são modelos de decoração associados à referência visual. Portas e esquadrias independentes plenamente manipuláveis exigem uma etapa de modelagem de dados e não são alegadas como concluídas aqui.
- A água transparente URP/Lit não possui ondas, refração ou margem fluvial detalhada. Se parecer artificial na revisão, registrar como bloqueio artístico, não substituí-la silenciosamente por outro placeholder.
- Não há FPS, draw calls, VRAM ou custo medido neste marco. Quando a cidade for aprovada, executar uma única rodada de escala/otimização sob os gates da Issue #1, com três sessões Windows separadas.

## Critério para avançar

Prosseguir somente se compilação/suítes estiverem verdes, as cinco imagens mostrarem materiais e silhuetas aceitáveis ao usuário, seleção/edição/salvamento funcionarem no teste manual e os itens provisórios relevantes tiverem uma decisão explícita. Caso contrário, corrigir o defeito demonstrado ou registrar o asset exato que falta antes de medir desempenho.
