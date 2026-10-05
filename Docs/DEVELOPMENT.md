# Desenvolvimento

Projeto Unity 6000.6.4f1 com URP 17.6.0 para Windows x64 inicialmente. Assets/Game contém os módulos e laboratórios; Assets/ThirdParty reserva conteúdo externo. Assets/Settings permanece no local do template. Docs registra decisões.

Namespaces usam a raiz `Aedifica`; seguir o módulo, por exemplo `Aedifica.Core`. Dependências entre assemblies são explícitas e acíclicas. Adicionar pacotes externos somente com necessidade demonstrada. Código C# puro deve ficar fora de MonoBehaviour quando apropriado.

No Unity Editor, abrir Window > General > Test Runner e executar as abas EditMode e PlayMode. Em batch mode, usar `-runTests -testPlatform EditMode` e `-runTests -testPlatform PlayMode`, com `-projectPath` apontando para este repositório e arquivos de resultado separados. Verificar também Console e importação de assets.

Trabalhar em mudanças pequenas e verificáveis. Toda feature precisa de critério de aceitação antes de entrar. Medir performance quando o impacto for relevante e registrar hardware, cena, quantidade de conteúdo e configurações para reproduzir o resultado.

CI de Unity: o Test Framework está no projeto, mas executar o Editor em CI exige uma estratégia de instalação e licenciamento/ativação apropriada ao provedor. Não criar workflow nem adicionar secrets em P0.0. Definir isso em etapa específica, preferindo credenciais gerenciadas e execução reproduzível de EditMode e PlayMode.
