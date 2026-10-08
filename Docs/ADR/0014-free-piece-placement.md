# ADR 0014 — Criação livre no ConstructionLab

## Decisão

O catálogo provisório na Game View lista apenas os 15 `PieceType` para os quais já existem dimensões e geradores de malha. Cada botão chama `ConstructionLabInteraction.BeginPlacement`. A classe `FreePieceCatalog` fornece parâmetros iniciais válidos; futuras opções de dimensões podem ampliá-la sem criar um segundo modelo de domínio.

A prévia é um GameObject com `MeshFilter` e `MeshRenderer`, sem `PieceView`, collider ou registro em `ConstructionWorld`. Ela usa a mesma geração de malha e um material compartilhado. A malha é construída uma vez por escolha de tipo; movimento e rotação atualizam só o Transform. O ID temporário usado para consultar o `SnapResolver` nunca é registrado como peça. Ao confirmar, a ferramenta cria um novo `PieceData` com ID único, insere-o em `ConstructionWorld`, cria sua `PieceView` por `ConstructionLabBlocks.Add` e seleciona a peça real. Uma falha de inicialização da View reverte a inserção.

## Posição e controles

O raio da câmera procura primeiro uma superfície superior (`normal.y >= 0,5`) até 500 m. Se não encontrar, intersecta o plano horizontal `y=0`; se o raio apontar para o céu, for inválido ou ultrapassar 500 m, a prévia fica oculta e o clique não cria nada. O pivô da peça é o centro de sua base; em superfícies superiores recebe um offset de 1 mm. Interseções entre peças não são proibidas, pois podem ser parte da arquitetura. Grid Snap quantiza X/Z e conserva a altura de contato; Angle Snap define o passo de Z/X; Surface, Edge e Endpoint utilizam o `SnapResolver` existente. O usuário pode desligar todos os snaps para posicionamento livre.

O catálogo permanece visível. Selecionar um tipo ativa a prévia; LMB fora do catálogo confirma e mantém o mesmo tipo pronto para outra colocação; Esc cancela. Z/X giram em passos de 15° ou no incremento do Angle Snap. G, R, T, H e P conservam seus comandos de snap. Clique no catálogo não posiciona peça. Enquanto a ferramenta está ativa, LMB não inicia pan, seleção ou gizmo; RMB ainda pode orbitar e WASD/Q/E e zoom continuam controlados pela câmera. Após Esc, seleção, gizmos, C/Home, materiais e as aberturas por Insert voltam ao fluxo normal. A peça recém-criada já fica selecionada, de modo que uma nova Wall pode receber Insert imediatamente após Esc.

## Limitações e extensões

Os tamanhos e materiais iniciais são fixos por tipo e editáveis com os gizmos ou comandos já existentes após a colocação. O catálogo é IMGUI provisório, sem miniaturas nem presets. A prévia não tem transparência de material; após confirmar, ela fica oculta até o cursor se mover para evitar sobreposição visual com a peça criada. O limite de 64 hits na consulta física e o plano horizontal de fallback são adequados ao laboratório; uma futura cena urbana pode precisar de regras de superfície e índice espacial próprios. Não há encaixe obrigatório, física estrutural ou CSG genérico.
