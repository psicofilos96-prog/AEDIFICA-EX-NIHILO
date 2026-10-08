# E1a — baseline S0/S1 preparado, ainda não medido

O ambiente remoto não contém Unity Editor nem o Lenovo LOQ alvo. Portanto **não existem valores anteriores de FPS, frame time ou memória medidos** para comparar com a E1a. A implementação preserva o caminho atual de uma `PieceView` com malha e collider por peça; o `ChangeSet` acrescenta somente uma fronteira de mutação. Nenhuma meta de desempenho foi certificada.

## Execução local

1. Abrir `Assets/Game/Scenes/PerformanceLab.unity` no Unity 6000.6.4f1. No `Performance Lab Runner`, manter seed `1202`, aquecimento `30 s` e captura `120 s`. Desmarcado `S1 Ten Houses` gera S0 (1 casa, 8 peças); marcado gera S1 (10 casas, 80 peças). Não alterar materiais ou câmera entre repetições.
2. Primeiro executar no Editor apenas para verificar Console, geração, 8/80 `PieceData`, 8/80 `PieceView` e exportação. O Editor não certifica FPS.
3. Para medir, incluir temporariamente a cena `PerformanceLab` no build Windows x64 local. Iniciar o executável a 1920×1080 no Lenovo LOQ conectado à tomada, com preset PC e modo de energia/temperatura registrados. Definir a variável de ambiente `AEDIFICA_COMMIT` para o hash exato medido antes de iniciar o jogo; se ausente, o CSV declara `unavailable`.
4. Aguardar o log `PerformanceLab result:`. O CSV fica em `Application.persistentDataPath`. Repetir cada cenário três vezes, reiniciando o processo, e arquivar arquivos sem sobrescrevê-los. Para um baseline realmente anterior à E1a, executar o commit anterior em checkout separado **com instrumentação equivalente**; sem isso, não chamar a comparação de causal.

## Conteúdo e limites do CSV

O arquivo registra versão Unity, commit informado, cenário, seed, plataforma, Editor/Player, resolução, identificação de CPU/GPU, capacidade nominal de RAM/VRAM, peças lógicas, views, número de mudanças, tempo de geração, uma atualização e reversão, frames amostrados, FPS médio, 1% low derivado dos piores 1% dos tempos de quadro, p95/p99 de frame time e memória alocada Unity no final. Tempos usam `Stopwatch` e `Time.unscaledDeltaTime`. Tempo específico de GPU e **uso** de VRAM são gravados como `unavailable`, sem extrapolação da capacidade nominal. A câmera fica em posição fixa; S0/S1 não representam ainda uma cidade detalhada nem exercício contínuo de edição.

O marcador `Aedifica.PerformanceLab.Create` cobre geração lógica + views e `Aedifica.PerformanceLab.Update` cobre uma mudança de posição e sua reversão. Capturar Profiler/Frame Debugger separadamente quando houver gargalo. Não comparar FPS de Editor com Player, nem declarar ≥60 FPS sem três capturas reais no hardware-alvo.
