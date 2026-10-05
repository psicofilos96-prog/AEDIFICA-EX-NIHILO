# ADR 0001 — Fundação do projeto

Status: aceito para P0.0.

O projeto usa Unity 6000.6.4f1, C# e URP, com Windows x64 como plataforma inicial. A arquitetura será modular, com assemblies Core, Construction e UI e referências explícitas. O modelo de dados do mundo será desacoplado de GameObjects e terá IDs estáveis. Construção paramétrica é a direção futura, mas não é implementada em P0.0.

DOTS/ECS e framework DI não entram inicialmente: a necessidade ainda não foi demonstrada. Performance é requisito arquitetural desde a fundação; decisões de otimização específicas dependerão de medidas. As cenas de laboratório e testes mínimos preparam as próximas etapas sem gameplay.

SampleScene foi inspecionada: é a cena do template URP e ainda consta da lista de build. Ela permanece intacta para evitar perda de configuração ou referência. Bootstrap foi adicionada como primeira cena do build; ConstructionLab e PerformanceLab permanecem fora do build nesta etapa.
