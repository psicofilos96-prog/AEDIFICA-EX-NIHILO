# Arquitetura

A direção de desenvolvimento é DATA → GEOMETRY → RENDERING → INTERACTION. O modelo persistente do mundo será independente da hierarquia de GameObjects; geometria derivará desses dados, a renderização os apresentará e a interação solicitará alterações no modelo. A etapa P0.0 não implementa essa cadeia.

Entidades persistentes usarão IDs estáveis, nunca nomes de GameObjects, posição na Hierarchy ou índices de listas. A representação editável e a representação otimizada poderão ser distintas. MonoBehaviours servirão sobretudo para ciclo de vida, apresentação e interação; uma peça arquitetônica não deve equivaler a um MonoBehaviour com simulação própria.

Os módulos começam com Core, Construction e UI. Construction e UI dependem explicitamente de Core; Core não depende deles. Pastas sem asmdef não definem novos assemblies nesta etapa. Novas fronteiras serão introduzidas quando houver código e necessidade clara. Não há singleton global, service locator, framework DI nem DOTS/ECS.

O modelo futuro deverá permitir chunking espacial, carregamento parcial, batching, GPU instancing, LOD/HLOD e representação otimizada de edifícios. A estratégia concreta será decidida com medições e requisitos das próximas etapas.

Bootstrap é a entrada preparada para a aplicação. ConstructionLab e PerformanceLab são cenas de laboratório. SampleScene permanece no projeto e na lista de build para preservar o template; Bootstrap ocupa a primeira posição da lista.
