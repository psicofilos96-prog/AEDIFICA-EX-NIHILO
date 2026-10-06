# ADR 0002 — Câmera de city-builder

Status: implementação P0.1, validação local no Unity pendente.

A câmera serve a navegação entre detalhe arquitetônico e escala urbana, sem depender dos futuros sistemas de construção. O componente `CityBuilderCamera` lê o Unity Input System por `CameraInputReader`; `CameraMotion` mantém foco, yaw, pitch e distância e calcula movimento; o componente aplica a transformação. O foco é um valor de estado, não um GameObject obrigatório.

Controles: WASD move no plano horizontal de acordo com o yaw; scroll muda a distância física ao foco; botão direito com arraste ajusta yaw e pitch; botão do meio com arraste desloca o foco como se agarrasse o mapa; Q/E ajustam yaw. ESC não tem ação. O Input Actions do template não foi alterado, pois seus mapas de Player/UI não são necessários para a câmera e não estão ligados à ConstructionLab. A leitura direta dos dispositivos usa o Input System instalado, sem Legacy Input Manager.

O zoom é multiplicativo e limitado, cobrindo diferentes escalas sem mudar FOV. O foco inicial está no plano y=0, e o pitch fica entre 1° e 89° após normalização; com distância positiva, a câmera permanece acima do plano de teste. Não há colisão com geometria futura. Movimento é horizontal, multiplicado por deltaTime, e sua velocidade interpola entre mínimo e máximo conforme a distância. Pan escala com a distância. Alvos de movimento, distância e orientação respondem imediatamente; a transformação converge com interpolação exponencial configurável, independente do frame rate. Smoothing zero desativa a interpolação.

`CameraSettings` centraliza limites e sensibilidades, normaliza valores inválidos e impõe teto finito de 10000 para escalares positivos. A câmera não usa busca de objetos, raycast, alocação intencional, LINQ ou criação de GameObjects por frame. ConstructionLab contém apenas malha de piso e cubos de referência.

Alternativas rejeitadas: Cinemachine, Legacy Input Manager, FOV como zoom, pivô GameObject obrigatório, física de mola e raycasts contínuos. Extensões futuras possíveis: foco por seleção, limites opcionais de mundo e prevenção de colisão com geometria; nenhuma foi implementada aqui.

Correção após teste local: o arraste com botão direito captura o cursor enquanto estiver pressionado e restaura seu estado ao soltar, perder foco ou desativar a câmera. Isso mantém `Mouse.current.delta` disponível durante o arraste na Game View. A velocidade de zoom passou de 0,12 para 0,30 por passo normalizado de scroll (120 unidades do Input System); a distância continua multiplicativa e limitada. WASD, Q/E e pan não mudaram.

Correção de especificação ainda em validação local: LMB com arraste substitui MMB como fonte de pan; a matemática do pan permanece. MMB não executa pan. O futuro sistema de seleção/construção deverá resolver o conflito de uso de LMB quando for implementado. O diagnóstico visual continua ativo para RMB e para comparar scroll bruto da roda física e do touchpad; nenhuma escala de zoom foi alterada sem esses dados.
