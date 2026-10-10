# Revisão artística delimitada do Marco Visual

**Estado:** candidato à segunda inspeção humana. A primeira rodada foi reprovada. Esta branch parte de `pvt/definitive-viability` no commit `9afae25`, que contém a correção dos 12 usos de dimensões específicas em `PvtVisualCityScenario`. Nenhum teste Unity ou captura de Game View foi executado no ambiente remoto.

## Mudanças da rodada

- As seis casas simples, quatro médias e duas complexas agora têm paredes laterais orientadas pelo comprimento correto, lajes intermediárias quando há segundo pavimento, aberturas dimensionadas, folhas de porta em madeira com montantes, molduras de madeira ou pedra, cunhais, linha de piso, enchimento dos oitões e beirais. Cada elemento estrutural continua como `PieceData` com `PieceId`; os estilos são apenas receitas desta cena, não regras do motor.
- As coberturas de telha alternam gable/hip/shed e extrapolam o perímetro das paredes. O monumento recebeu paredes laterais na orientação correta, cornija periférica, cúpula, abóbadas, colunata, escada e arcos ainda individualmente editáveis.
- A rua principal liga os agrupamentos residenciais à praça do monumento. A praça e as bordas da via seguem o relevo. O terreno mistura solo e calçamento em duas camadas com transições irregulares determinísticas. Arbustos CC0 de menor escala e pinheiros CC0 com LOD foram colocados em pátios, com distribuição florestal por células mantida.
- A cena passou a gravar luz solar lateral de fim de tarde, ambiente Trilight, névoa distante e MSAA quando oferecido pelo URP Asset. Não foram alterados URP Asset, Quality ou ProjectSettings. A máscara PBR derivada agora tem canal verde de oclusão branco, necessário ao Terrain/Lit.
- Cinco câmeras de Game View foram reposicionadas: fachada, materiais, rua, panorama e monumento. A captura da Unity continua sem pós-processamento externo.

## Recursos e custo potencial

Nenhum recurso externo novo foi incorporado nesta rodada. Todos os modelos e mapas ainda são os recursos CC0 verificados em `Docs/PVT_VISUAL_ASSET_MANIFEST.csv`, com URL, resolução e hash. A mistura de terreno usa dois TerrainLayers e alphamap 256²; o aumento de detalhes das fachadas eleva o número de peças lógicas e triângulos das páginas combinadas. As plantações usam os mesmos prefabs com três LODs. Névoa e ambiente de cena são baratos; sombras suaves de uma luz direcional e o número de páginas visuais precisam de medição posterior. **Não há FPS ou memória de GPU medidos nesta rodada.** Os benchmarks A/B/C permanecem separados e não foram executados.

## Reprodução segura no Windows

Em `C:\Projetos\AEDIFICA-EX-NIHILO`, preserve alterações locais e stashes. Crie um worktree independente após o push desta branch:

```powershell
git fetch origin pvt/visual-art-review
git worktree add --detach "C:\Projetos\AEDIFICA-ART-REVIEW" origin/pvt/visual-art-review
```

Abra `C:\Projetos\AEDIFICA-ART-REVIEW` na Unity **6000.6.4f1**. Aguarde o import, confirme que não há erros no Console e execute **EditMode → Run All** e **PlayMode → Run All**. Use **Tools → Aedifica → Visual Milestone → Create Historic City Review Scene**; abra `Assets/Game/Scenes/PvtVisualCity.unity` e pressione Play na aba Game. O comando preserva uma cena já existente e não altera os cenários históricos. Se o menu recusar a criação por haver uma cena antiga nesse *worktree*, preserve-a e relate o caminho; não a sobrescreva.

Na Game View, inspecione os botões `facade`, `materials`, `street`, `landscape`, `monument` e acione **Capture 5 views**. Os PNGs são gravados em `Application.persistentDataPath`, normalmente `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`. Registre a resolução da Game View e o Console. Inspecione em especial encontros de paredes, altura das coberturas, leitura das janelas, canal normal, alpha da folhagem, transições do pavimento, recortes de sombra e edifício monumental. Use os controles da cena para seleção, movimento, resize, troca de material, criação, exclusão, Undo/Redo e Save/Load.

## Limitações para decisão humana

- A estética mediterrânea ainda usa pinheiro e arbusto genéricos da seleção CC0 anterior. Não foi localizado nesta rodada um modelo verificado e adequado de oliveira/cipreste com LOD e atlas 1K; esse recurso específico seria necessário se a vegetação atual descaracterizar a cena.
- A água continua o material transparente simples da etapa anterior. Sem refração/espuma/margens artísticas, poderá parecer provisória na vista panorâmica.
- Porta, portão e estátua FBX são adereços de apresentação. Porta acompanha a parede monumental; molduras e vidros são peças lógicas independentes. Não existe vínculo atômico de histórico entre uma parede e todas as peças independentes de acabamento ao mover ou apagar a parede. Essa limitação de UX deve ser testada manualmente e não é apresentada como recurso concluído.
- Não há validação real das normais, importação do FBX, renderização URP ou qualidade final sem a Unity Windows. A cena não representa ainda bairros amplos, LOD arquitetônico ou o orçamento medido de uma cidade extensa.

O Marco Visual permanece reprovado até compilação, suítes, gate manual de edição e aprovação explícita das cinco capturas. Se as imagens ainda não forem aceitáveis, registrar o motivo observado e os assets ou recursos concretos que faltam antes de qualquer decisão de desempenho.
