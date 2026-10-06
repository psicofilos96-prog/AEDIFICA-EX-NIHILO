# ADR 0005 — Seleção e manipulação inicial

Status: implementação P0.4; compilação, testes Unity e homologação manual local pendentes.

Seleção usa `SelectionState` com `PieceId?`, fora de `ConstructionWorld` e `PieceData`. Um clique LMB de até 5 pixels seleciona a View atingida ou limpa seleção no vazio. Um arraste maior conserva o pan da câmera. O hit test usa raycast e `BoxCollider` derivado de `BlockDimensions` (centro local em Height/2, size Width/Height/Depth). `PieceView` mapeia o collider ao `PieceId`, sem guardar dimensões ou transform autoritativos. A View selecionada recebe apenas um `MaterialPropertyBlock` de cor; o material URP é compartilhado.

Handles simples de runtime são criados na ConstructionLab. Atalhos 1/2/3 alternam Move/Rotate/Resize sem roubar WASD ou Q/E. Move usa X/Y/Z mundiais; Rotate usa Y mundial; Resize usa dimensões locais da peça. O tamanho do gizmo acompanha aproximadamente a distância da câmera. Handles são primitives apenas para a ferramenta, não para a geometria do Block. O clique em handle inicia uma `ManipulationSession`, que retém o estado inicial e calcula o novo `PieceData` a partir do deslocamento total do cursor. Isso deixa uma futura ação de Undo conceitualmente agrupável, mas não implementa histórico.

O fluxo da alteração é intenção → `PieceData.WithTransform` ou `WithBlockDimensions` → `ConstructionWorld.Replace` → `PieceView.Refresh`. ID e tipo permanecem. Resize limita cada dimensão a 0,1 m como regra da ferramenta; o contrato universal de `BlockDimensions` permanece >0. Width/Depth mudam simetricamente em X/Z local; Height cresce desde a base Y=0. A View regenera Mesh apenas se as dimensões mudarem e atualiza o collider, preservando material, posição, rotação e `localScale = (1,1,1)`. A View pode ser recriada a partir do mundo.

`ConstructionLabInteraction` é o único controlador com Update para seleção e manipulação. Sua ordem de execução antecede a câmera. Apenas durante o arraste de um handle, ele solicita à câmera que ignore PanPixels de LMB; RMB, teclado e zoom permanecem no caminho existente. Sem sessão, o comportamento homologado da câmera não muda. Não há Update por peça, busca global por frame ou regeneração de malha sem alteração.

Gizmos são técnicos e temporários em aparência. Seleção múltipla, snap, resize por face com âncora oposta, Undo/Redo, menu de ferramentas, rebinding, pooling, batching e representação massiva permanecem adiados.
