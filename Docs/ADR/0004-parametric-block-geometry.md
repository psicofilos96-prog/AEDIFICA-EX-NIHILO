# ADR 0004 — Geometria paramétrica do Block

Status: implementado em P0.3; compilação, testes e inspeção visual no Unity local pendentes.

`PieceData` continua autoritativo. `BlockDimensions` define Width, Height e Depth em metros, com 1 Unity unit = 1 meter. `BlockGeometryGenerator` deriva uma malha local determinística dessas dimensões. `BlockMeshFactory` converte a geometria em `UnityEngine.Mesh`. A representação usa posição e rotação de `PieceTransform` e `localScale = (1,1,1)`; nem Mesh nem GameObject são fonte de dados e ambos podem ser reconstruídos.

Pivot local: X/Z no centro, Y na base. Um bloco de 4 × 2 × 6 ocupa X -2..2, Y 0..2, Z -3..3 antes da transformação. Seis faces usam 4 vértices independentes cada, total de 24 vértices, 12 triângulos e 36 índices. Normais constantes por face preservam quinas rígidas; a ordem dos triângulos aponta para fora. UVs começam em (0,0) e avançam uma unidade por metro de superfície, com orientação local previsível por face. Bounds são calculados das dimensões.

A geração matemática fica em `Aedifica.Geometry`, dependente apenas de Construction. A criação do Mesh e a demonstração ficam em `Aedifica.Rendering`, dependente de Geometry e Construction. A ConstructionLab cria três registros `PieceData` de exemplo, gera três malhas uma vez em Awake e usa um único material URP compartilhado. Há uma representação GameObject por bloco apenas para demonstração P0.3, sem Update por peça. O laboratório não serializa dimensões duplicadas e não usa `Transform.localScale` para definir tamanho. Colliders foram adiados.

Geração por frame, chunking, batching próprio, instancing, LOD/HLOD, pooling, Jobs/Burst e estratégia de representação de cidades massivas ficam adiados até requisitos e benchmarks justificarem. Não há seleção, interação com peças, save/load nem material semântico nesta etapa.
