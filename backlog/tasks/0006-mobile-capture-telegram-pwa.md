# 0006 — Captura Mobile Instantânea: PWA Walkie-Talkie e Web Share Target

## Objetivo
Viabilizar a captura ultra-rápida de pensamentos, áudios e tarefas a partir do smartphone do usuário, transformando o frontend Ionic Angular em um Progressive Web App (PWA) instalável com modo "Walkie-Talkie" (gravação de voz com 1 toque) e suporte ao Web Share Target nativo para compartilhar links e textos diretamente para o cofre, reproduzindo a experiência mobile desenhada no Claude Design.

## Contexto
O usuário precisa registrar ideias e tarefas rapidamente quando está fora do PC (no trânsito, caminhando ou no celular). O artefato de design prototipou com precisão a experiência mobile do GranBrain: um modo Walkie-Talkie com botão de microfone circular generoso de 96px (`.walkie-btn`), feedback tátil/visual de gravação ativa (`.walkie-btn.live`), lista enxuta de itens capturados recentemente (`.m-item`) e cartão de compartilhamento rápido (`.share-card`). Este Work Item entrega essa experiência PWA autossuficiente e sem atrito.

## Diretriz de Planejamento de Frontend (/spec-frontend)
**Mandatório na execução de `/spec-task`:**
O planejamento do frontend desta task deve ser realizado utilizando a skill `/spec-frontend`, extraindo o recorte das classes mobile e gerando a subseção `### Especificação visual` dentro de `## Escopo`:
- **Origem do Import:** `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
- **Artefato Claude:** `https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy`
- **Recorte Visual do Mockup:** Telas e componentes Mobile (`.mobile-wrap`, `.phone`, `.phone-screen`, `.m-h`, `.m-list`, `.m-item`, `.walkie`, `.walkie-btn`, `.walkie-btn.live`, `.share-card`).
- **Elementos Visuais a Reconciliar:** Botão Walkie-Talkie circular de 96px com transição de escala (`transform: scale(1.06)`) e halo pulsante em `--danger-soft`, cartões de captura rápida para leitura em tela de celular e cartões de destino de compartilhamento.

## Escopo
**Dentro:**
- Progressive Web App (PWA) no Ionic Angular:
  - Configuração do `@angular/pwa` (manifest, ícones, service worker e cache offline básico).
  - Modo "Walkie-Talkie" (especificado via `/spec-frontend`): botão circular gigante (`.walkie-btn`) para gravação com 1 toque (press and hold / tap to toggle).
  - Instalação na tela inicial do celular Android/iOS como aplicativo nativo.
  - Web Share Target API: o GranBrain aparece no menu de "Compartilhar" do celular (permitindo enviar links do navegador ou textos direto para o `inbox/`).
- Otimização Mobile-First:
  - Layout adaptado para uso com uma mão (One-Handed Navigation).
  - Feedback tátil (vibração suave ao iniciar/encerrar gravação de voz onde suportado).
  - Envio e transcrição em background de mensagens de voz.

**Fora:**
- Dependência de bots de Telegram ou WhatsApp (descartados em favor do PWA direto e autossuficiente).
- Publicação nas lojas oficiais Google Play ou Apple App Store (o PWA direto pelo navegador/domínio resolve 100% da necessidade pessoal sem taxas de desenvolvedor).

## Critérios de aceite
1. **AC1 — PWA Instalável:** O app exibe o prompt de instalação no celular e abre em tela cheia (standalone) sem barras de navegador.
2. **AC2 — Botão Walkie-Talkie Conforme Mockup:** Pressionar e segurar (ou clicar) no botão `.walkie-btn` ativa a gravação com estilo `.walkie-btn.live`, converte em texto e dispara para a API do GranBrain sem abrir telas secundárias.
3. **AC3 — Web Share Target Funcional:** Compartilhar um link ou texto de outro app no celular abre o GranBrain diretamente na tela de confirmação de captura no `inbox/` renderizando o `.share-card`.
4. **AC4 — Gravação Resiliente no Cofre:** Pensamentos e áudios gravados pelo celular são persistidos com frontmatter YAML e tags `#mobile-capture` no cofre.

## Área de impacto esperada
- `frontend/src/manifest.webmanifest`
- `frontend/ngsw-config.json`
- `frontend/src/app/components/**`
- `frontend/src/app/app.component.html`
- `frontend/src/app/app.component.scss`
- `backend/src/GranBrain.Api/Endpoints/MobileEndpoints.cs`
- `backend/tests/GranBrain.UnitTests/**`
- Cofre Obsidian: `inbox/`, `zettelkasten/Fleeting.md`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | PWA autossuficiente com Walkie-Talkie dedicado substitui necessidade de Telegram ou bots de terceiros. |
| D2 | Componente de Walkie-Talkie implementa rigorosamente o visual `.walkie-btn` e animações do mockup via `/spec-frontend`. |
| D3 | Suporte offline gracioso com sincronização em fila assim que o dispositivo restabelecer conectividade. |
