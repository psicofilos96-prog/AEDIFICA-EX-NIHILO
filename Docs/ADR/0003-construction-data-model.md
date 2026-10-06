# ADR 0003 — Modelo de dados de construção

Status: implementado em P0.2; execução dos testes Unity locais pendente.

O mundo de construção é dado autoritativo. GameObjects, Transform, hierarquia de cena, Renderer e índices de coleção não definem identidade nem estado persistente. O fluxo futuro permanece WORLD DATA → GEOMETRY → RENDERING → INTERACTION; P0.2 implementa somente o primeiro nível. Uma peça pode existir sem representação visual.

`PieceId` é um valor GUID não vazio, comparável e convertido para texto canônico de 32 dígitos hexadecimais. `Parse`/`TryParse` permitem IDs conhecidos, testes determinísticos e futura persistência textual. `PieceIdGenerator.New` é separado da representação e usa `Guid.NewGuid`; duplicar uma peça deverá criar um novo ID. O valor padrão da struct é inválido e rejeitado por `PieceData`. Não usamos `GetInstanceID`, nome de GameObject nem posição em lista.

`PieceTransform` armazena posição em metros e rotação quaternion por valor, usando `UnityEngine.Vector3`/`Quaternion` como tipos matemáticos compactos já disponíveis. Não referencia `UnityEngine.Transform` ou cena. A posição deve ser finita; a rotação deve ser finita, não nula e é normalizada na criação. A convenção é 1 Unity unit = 1 meter. A escala não define dimensões arquitetônicas.

`BlockDimensions` contém Width, Height e Depth em metros, todos finitos e maiores que zero. `PieceData` compõe ID, tipo Block, transform e dimensões, com propriedades somente de leitura. Neste estágio apenas Block é válido; outros tipos e parâmetros tipados serão adicionados quando implementados. Não há hierarquia de classes por tipo nem dicionário genérico de parâmetros. MaterialId e GroupId ficam adiados.

`ConstructionWorld` armazena peças em `Dictionary<PieceId, PieceData>`, com consulta por ID sem busca linear obrigatória. `Add` retorna false para ID duplicado; `TryGet` e `Remove` retornam false para ID ausente ou inválido. `Add(null)` e construtores com estado inválido lançam exceções de argumento, pois violam o contrato de programação. A enumeração fornece as peças presentes sem criar cópias de registros. `PieceData` é imutável para que mutações futuras possam substituir registros de forma controlada.

Undo/Redo, comandos, Save/Load, formatos de serialização, blueprints, geometria, renderização, seleção, chunking, materiais e grupos ficam para etapas futuras. A representação canônica do ID facilita persistência futura, mas nenhum arquivo ou JSON é produzido agora. Não existe MonoBehaviour por peça nem callback por frame.
