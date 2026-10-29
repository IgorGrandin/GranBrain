# 0006 — Captura Mobile Instantânea: PWA Walkie-Talkie e Bot Privado de Mensageria (Telegram/Webhook)

## Objetivo
Viabilizar a captura ultra-rápida de pensamentos, áudios e tarefas a partir do smartphone do usuário, disponibilizando o frontend Ionic Angular como Progressive Web App (PWA) instalável e um bot privado no Telegram (ou Webhook leve) conectado à API do GranBrain para transcrição e gravação instantânea no cofre.

## Contexto
Discutido na conversa de concepção (`8cc6b830-ec40-4eae-b8a7-d22515cab2fe`): o usuário frequentemente tem ideias ou tarefas quando está fora do computador (no trânsito, caminhando ou no celular). Abrir o Obsidian mobile para digitar é lento e propenso a abandono. Com um botão de áudio de 1 toque no PWA ou um áudio encaminhado para um bot privado de Telegram, o GranBrain recebe o áudio, transcreve com IA, sintetiza a intenção e grava imediatamente no `inbox/` ou `Fleeting.md`.

## Escopo
**Dentro:**
- Progressive Web App (PWA) no Ionic Angular:
  - Configuração do `@angular/pwa` (manifest, ícones, service worker e cache offline básico).
  - Modo "Walkie-Talkie": botão de microfone em destaque na tela inicial para gravação com 1 toque.
  - Instalação na tela inicial do celular Android/iOS como aplicativo nativo.
- Bot Privado no Telegram (ou Webhook de Mensageria):
  - Conector leve no backend `.NET 10` escutando mensagens enviadas exclusivamente pelo Chat ID do desenvolvedor.
  - Suporte a mensagens de texto, mensagens de voz (áudios `.ogg`/`.mp3`) e links compartilhados.
  - Transcrição de áudio via Whisper CLI, Web Speech ou modelo de transcrição local/CLI.
  - Gravação automática da mensagem transcrita em `inbox/` com tag `#mobile-capture` e resposta de confirmação no Telegram.
- Segurança de Acesso:
  - Trava de segurança no Telegram: ignora qualquer mensagem vinda de IDs não autorizados.

**Fora:**
- Grupos públicos ou bots multi-usuário (o GranBrain é 100% de uso pessoal e privado).
- Suporte a envio de arquivos binários pesados (vídeos longos, ISOs).

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — PWA Instalável e Responsivo:** O app frontend exibe o prompt de instalação no celular e opera com interface ajustada para telas sensíveis ao toque (Mobile-First).
2. **AC2 — Bot Telegram Conectado e Restrito:** O bot responde exclusivamente ao ID do desenvolvedor; mensagens de outros remetentes são silenciosamente descartadas.
3. **AC3 — Processamento de Áudio com Transcrição:** Áudio enviado no Telegram é convertido em texto e gera uma nota formatada em `inbox/` ou `zettelkasten/Fleeting.md`.
4. **AC4 — Notificação de Confirmação:** O usuário recebe uma confirmação imediata (com link/título da nota gerada) após a gravação no cofre.

## Área de impacto esperada
- `frontend/src/manifest.webmanifest`
- `frontend/ngsw-config.json`
- `backend/src/GranBrain.Infrastructure/Telegram/**`
- `backend/src/GranBrain.Api/Endpoints/MobileEndpoints.cs`
- `backend/tests/GranBrain.UnitTests/**`
- Cofre Obsidian: `inbox/`, `zettelkasten/Fleeting.md`

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Telegram Bot escolhido como mensageiro de entrada pela simplicidade da API de áudios sem custo e estabilidade em segundo plano no celular. |
| D2 | Frontend Ionic standalone configurado com Service Worker para abrir instantaneamente mesmo com oscilação de 4G/5G. |
