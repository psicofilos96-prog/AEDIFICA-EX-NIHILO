# ADR 0009 — P0.7.2 Geometric Snap

Status: implementação para homologação local no Unity.

Surface, Edge e Endpoint são estados independentes em `SnapSettings`, alternados temporariamente por `T`, `H` e `P`. `G` continua Grid e `R` continua Angle. A distância padrão de captura é 0,25 m; a de liberação é 0,4 m. A liberação maior retém a mesma feature durante pequenos movimentos, e a captura só ocorre dentro de 0,25 m.

`SnapGeometry` deriva, a cada consulta ativa, os oito cantos, doze segmentos e seis faces de Block, Wall ou Slab a partir de `PieceData` e sua rotação. Não usa collider ou escala de Transform como fonte geométrica. `SnapResolver` percorre as peças existentes somente durante Move ou Face Resize; a fronteira `IEnumerable<PieceData> nearbyPieces` permite substituir a varredura do ConstructionLab por índice espacial no futuro. A peça em movimento é excluída pelo ID.

`ManipulationSession.Evaluate` calcula o gesto desde o estado inicial e aplica Grid Snap em Move. O resolver procura então uma correção geométrica local. Em Face Resize, apenas cantos, arestas e a superfície da face móvel participam, a correção é projetada sobre seu eixo local, e `ResizeToDimension` reconstrói o resultado desde o estado inicial, preservando a face oposta e o mínimo de 0,1 m. O modo bilateral e Rotate não recebem snap geométrico.

Entre candidatos, vence a menor correção. Distâncias que diferem até 0,00001 m são tratadas como empate: Endpoint > Edge > Surface; depois `PieceId`, índice da feature móvel e índice da feature alvo. O alvo adquirido permanece até exceder `ReleaseDistance` ou desaparecer/deixar de ser válido. Com Grid e geométrico ligados, o geométrico refina localmente o resultado do Grid. O feedback de laboratório usa duas linhas `Debug.DrawLine` no ponto alvo enquanto o snap está ativo; a Game View precisa de Gizmos habilitados para exibi-las.

Esta fundação usa cantos de cuboides como endpoints, arestas lineares e faces planas finitas. Ela não resolve intenção, alinhamentos globais, superfícies não paralelas ou constraints de múltiplas peças. A consulta linear atual é adequada ao ConstructionLab pequeno e fica isolada para substituição futura.
