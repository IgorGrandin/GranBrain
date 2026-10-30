# Skill: Lightweight Subagent Scout (Pesquisa Distribuída Map-Reduce)

## Contexto & Motivação
Modelos básicos e leves (`gemini-3.8-flash`, `claude-3-5-haiku`, `gpt-4o-mini` / `gpt-luna`) oferecem alta velocidade e custo reduzido, porém possuem janelas de atenção efetiva mais sensíveis à saturação quando forçados a ler dezenas de arquivos em uma única janela de contexto.

Para evitar alucinações, context drift e estouro de limites de chamadas de ferramentas, **o agente principal nunca deve abrir dezenas de arquivos em sequência no seu loop principal**. Em vez disso, aplica o padrão **Map-Reduce com Subagentes de Mesmo Modelo**.

---

## Fluxo Operacional

```
                 [ Usuário / Requisição Ampla ]
                               │
                               ▼
               [ Orquestrador Principal (Leve) ]
                               │
               ┌───────────────┼───────────────┐
               ▼               ▼               ▼
      [ Subagente 1 ]    [ Subagente 2 ]   [ Subagente 3 ]
       (Mesmo Modelo)     (Mesmo Modelo)    (Mesmo Modelo)
        Fatia: daily/      Fatia: zettel/    Fatia: kanban/
               │               │               │
               ▼               ▼               ▼
         [ Resumo 1 ]     [ Resumo 2 ]     [ Resumo 3 ]
               └───────────────┬───────────────┘
                               │
                               ▼
               [ Consolidação Final (Reduce) ]
                               │
                               ▼
                 [ Resposta & Generative UI ]
```

### 1. Fase Map (Fatiamento e Delegação)
Quando uma pergunta exigir busca ampla (ex: *"O que decidi sobre arquitetura nas reuniões do último mês e quais tarefas estão pendentes?"*):
1. O agente principal **identifica as partições** de busca (ex: partição 1: `daily/`, partição 2: `zettelkasten/`, partição 3: `assets/kanban/`).
2. Invoca até **3 subagentes em paralelo** utilizando o **mesmo modelo leve** (ex: `flash` ou `inherit`).
3. O prompt do subagente deve ser estritamente delimitado:
   - Caminho do diretório alvo.
   - Padrão de busca ou palavras-chave.
   - Formato de saída esperado: apenas lista de links `[[Nota]]` e trechos relevantes de 1-2 linhas, sem inventar fatos.

### 2. Execução dos Subagentes (Isolamento)
- Subagentes operam em **somente-leitura** (`READ`).
- Nunca realizam edições no cofre ou no código.
- Retornam um pacote de evidências conciso com `OBSERVED FACT` e links de arquivos.

### 3. Fase Reduce (Síntese pelo Orquestrador)
- O agente principal recebe as respostas sintéticas dos subagentes.
- Agrega as evidências em uma narrativa coerente.
- Constrói os blocos de Generative UI correspondentes (`kpi-grid`, `markdown-note-list`, `kanban-board`).
- Emite a resposta final ao usuário de forma instantânea e sem degradação de contexto.

---

## Regras de Ouro
1. **Mesmo Nível de Modelo:** Sempre lance subagentes com o modelo correspondente ou mais leve (`flash_lite` / `flash` / `inherit`), mantendo a premissa de custo mínimo e velocidade máxima.
2. **Máximo de 3 Subagentes Simultâneos:** Para não esgotar concorrência local de processos e manter a resposta ágil.
3. **Escopo Atômico por Subagente:** Cada subagente recebe um diretório ou uma fatia temporal (ex: "últimos 7 dias").
4. **Sem Efeitos Colaterais:** Subagentes de pesquisa nunca executam escritas ou comandos destrutivos.
