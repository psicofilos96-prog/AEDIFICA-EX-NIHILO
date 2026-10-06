# P0.1 — Diagnóstico temporário da câmera

Este painel é instrumentação de desenvolvimento, não parte da câmera final. Está anexado apenas à Main Camera de ConstructionLab e aparece no Play Mode. Remover o componente, o script e os acessos `Diagnostic*`/`LastDiagnostics` após identificar o defeito e validar a correção local.

A inspeção estática encontrou uma única Camera em ConstructionLab, no GameObject `Main Camera`, com `CityBuilderCamera` e Camera habilitados. O componente da câmera lê `Mouse.current` em `CameraInputReader.Read()`, entrega `CameraInput` a `CameraMotion.Step()` em `CityBuilderCamera.Update()` e aplica posição/rotação ao mesmo Transform. A cena serializa `zoomSpeed: 0.3`, que sobrescreve o valor inicial do campo; o painel mostra o valor runtime. A observação local ainda precisa confirmar qual câmera renderiza a Game View e quais dados chegam do mouse.

Na Game View em Play Mode, testar separadamente RMB com movimento horizontal/vertical, MMB e um passo de scroll. Comparar `RMB`, `Mouse Delta`, `RotatePixels`, yaw/pitch alvo e atual, e `Transform Rotation`. Para zoom, comparar `Raw Scroll Y`, `Normalized`, `Zoom Speed`, distância alvo e atual. O painel também mostra lock/visibilidade do cursor, nome e estado da Camera, `Camera.main`, número de câmeras ativas e Transform. Nenhum valor de scroll ou sensibilidade foi alterado nesta etapa.

Leitura sugerida: RMB UP indica falha antes do roteamento; RMB DOWN com delta zero aponta à captura/leitura; delta e RotatePixels não zero com alvo imóvel aponta ao cálculo; alvo móvel com Transform imóvel aponta à aplicação. A mesma comparação entre scroll bruto, normalizado e distâncias localiza o zoom lento. O cursor lock anterior foi mantido apenas para observação, sem considerá-lo correção confirmada.
