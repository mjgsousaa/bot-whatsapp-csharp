# Instruções de Implementação - Refatoração BotZap AI

Esta atualização foca em três pilares: **Unificação de Mídia**, **Agendamento Inteligente** e **Humanização**.

## 1. Unificação de Mídia + Texto
O sistema agora utiliza a função `EnviarAnexo(caminho, legenda)`. 
- **Gatilhos**: Se um gatilho tiver anexo, a resposta será enviada como legenda da imagem/vídeo.
- **Bulk Message**: O texto da campanha agora é enviado como legenda do anexo selecionado, evitando mensagens separadas.

## 2. Módulo de Agendamento
Foi implementado o `SchedulingService` que gerencia slots de horários.
- **IA Secretária**: A IA recebe o contexto de horários disponíveis quando o usuário demonstra intenção de agendar.
- **Banco de Dados**: Os agendamentos são salvos em `%AppData%/BotZapAI/agendamentos.json`.
- **Interface**: Nova aba "Agendamentos" adicionada ao menu lateral para gestão do administrador.

## 3. Humanização (Prompt Engineering)
O prompt do sistema foi atualizado para incluir diretrizes de:
- **Empatia Contextual**: Reconhecer a necessidade do usuário.
- **Confirmação Ativa**: Substituir mensagens mecânicas por frases naturais.
- **Variação Lexical**: Evitar repetições.

## Como Executar
1. Certifique-se de ter o .NET SDK instalado.
2. Restaure as dependências: `dotnet restore`.
3. Compile o projeto: `dotnet build`.
4. Configure sua chave da Groq na aba "Inteligência Artificial".

---
*Desenvolvido com foco em UX Conversacional e Alta Conversão.*
