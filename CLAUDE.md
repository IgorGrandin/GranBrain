# GranBrain — Diretrizes de Governança e Arquitetura

## 1. Visão Geral
O **GranBrain** é o Sistema Operacional de Conhecimento Pessoal (Obsidian AI OS) de Igor Grandin. Ele opera o cofre do Obsidian (`VaultPath`) como um banco de dados em Markdown estruturado, oferecendo um backend `.NET 10`, um orquestrador Multi-IA baseado em CLIs locais autenticadas (`Codex`, `Claude Code`, `Gemini/Antigravity`) e um frontend `Ionic Angular` com **Generative UI** (renderização dinâmica de interfaces por voz e texto).

## 2. Regras de Execução (`/spec-task`, `/plan-task`, `/execute-ig`)
- Toda entrega parte de uma especificação canônica em `backlog/tasks/<id>-<slug>.md` e um plano commitado em `.cursor/plans/wi_<id>_<slug>.plan.md`.
- Implementação segue `/execute-ig` com um commit por fatia aprovada, testes discriminantes reais e árvore limpa no gate final.
- **Segurança Inegociável (`Vault Jail`):** Nenhum serviço de arquivo ou CLI pode ler ou escrever fora do diretório raiz configurado em `GranBrain:VaultPath`. Caminhos relativos contendo `..` ou links para fora da raiz devem lançar exceção de segurança (`VaultPathViolationException` -> HTTP 403/400).
- **Preservação do Cofre:** Operações de inicialização do cofre (`VaultInitializer`) nunca apagam ou sobrescrevem notas existentes do usuário em `daily/`, `zettelkasten/` ou `assets/`.

## 3. Hierarquia de Fontes
1. Especificação da Task (`backlog/tasks/<id>-<slug>.md` — seção `Decisões do dev`).
2. ADRs (`docs/adr/`).
3. Plano da Task (`.cursor/plans/wi_<id>_<slug>.plan.md`).
