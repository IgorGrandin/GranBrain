# 0006 — Captura Mobile Instantânea: PWA Walkie-Talkie e Web Share Target

## Objetivo
Viabilizar a captura ultra-rápida de pensamentos, áudios e tarefas a partir do smartphone do usuário, transformando o frontend Ionic Angular em um Progressive Web App (PWA) instalável com modo "Walkie-Talkie" (gravação de voz com 1 toque) e suporte ao Web Share Target nativo para compartilhar links e textos diretamente para o cofre.

## Contexto
O usuário precisa registrar ideias e tarefas rapidamente quando está fora do PC (no trânsito, caminhando ou no celular). Como o GranBrain já possui frontend em Ionic Angular com chat e Web Speech API, transformá-lo em um PWA nativo elimina a necessidade de mensageiros de terceiros (como Telegram/WhatsApp), mantendo toda a experiência unificada, segura e sob controle total do desenvolvedor.

## Escopo
**Dentro:**
- Progressive Web App (PWA) no Ionic Angular:
  - Configuração do `@angular/pwa` (manifest, ícones, service worker e cache offline básico).
  - Modo "Walkie-Talkie": botão de microfone flutuante em destaque na tela inicial para gravação com 1 toque.
  - Instalação na tela inicial do celular Android/iOS como aplicativo nativo.
  - Web Share Target API: o GranBrain aparece no menu de "Compartilhar" do celular (permitindo enviar links do navegador ou textos direto para o `inbox/`).
- Otimização Mobile-First:
  - Layout adaptado para uso com uma mão (One-Handed Navigation).
  - Feedback tátil (vibração suave ao iniciar/encerrar gravação de voz onde suportado).
  - Envio e transcrição em background de mensagens de voz.

**Fora:**
- Dependência de bots de Telegram ou WhatsApp (descartados em favor do PWA direto e autossuficiente).
- Publicação nas lojas oficiais Google Play ou Apple App Store (o PWA direto pelo navegador/domínio resolve 100% da necessidade pessoal sem taxas de desenvolvedor).

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — PWA Instalável:** O app exibe o prompt de instalação no celular e abre em tela cheia (standalone) sem barras de navegador.
2. **AC2 — Botão Walkie-Talkie de 1 Toque:** Pressionar e segurar (ou clicar) grava áudio, converte em texto e dispara para a API do GranBrain sem abrir telas secundárias.
3. **AC3 — Web Share Target Funcional:** Compartilhar um link ou texto de outro app no celular abre o GranBrain diretamente na tela de confirmação de captura no `inbox/`.
4. **AC4 — Gravação Resiliente no Cofre:** Pensamentos e áudios gravados pelo celular são persistidos com frontmatter YAML e tags `#mobile-capture` no cofre.

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
