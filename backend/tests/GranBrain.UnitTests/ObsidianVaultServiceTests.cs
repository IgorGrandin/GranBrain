using System.Text;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.UnitTests;

public sealed class ObsidianVaultServiceTests
{
    [Fact]
    public async Task HabitsKanbanAndNotes_MutateMarkdownFilesCorrectly()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainServiceTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "templates"));
            Directory.CreateDirectory(Path.Combine(tempVault, "assets", "kanban"));

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "templates", "Notas Diárias.md"),
                "# {{date:DD MMMM, YYYY (dddd)}}\n#### Cronograma\n- [ ] 07:00 - Colocar Alanna na perua\n- [ ] 07:30 - Orar/Ler Bíblia\n",
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Profissional.md"),
                "---\nkanban-plugin: board\n---\n\n## To Do\n\n## Doing\n\n## Done\n\n%% kanban:settings\n%%\n",
                Encoding.UTF8);

            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var service = new ObsidianVaultService(guard, initializer);

            var testDate = new DateOnly(2026, 10, 29);

            // 1. Toggle habit on non-existent daily note -> creates daily note and marks [x]
            var habitsAfterCheck = await service.ToggleHabitAsync("07:30 - Orar/Ler Bíblia", testDate);
            var targetHabit = Assert.Single(habitsAfterCheck, h => h.Label.Contains("Orar/Ler Bíblia"));
            Assert.True(targetHabit.Completed);

            var dailyFile = Path.Combine(tempVault, "daily", "2026-10-29.md");
            Assert.True(File.Exists(dailyFile));
            Assert.Contains("- [x] 07:30 - Orar/Ler Bíblia", await File.ReadAllTextAsync(dailyFile, Encoding.UTF8));

            // 2. Toggle same habit again -> unchecks back to [ ]
            var habitsAfterUncheck = await service.ToggleHabitAsync("Orar/Ler Bíblia", testDate);
            Assert.False(Assert.Single(habitsAfterUncheck, h => h.Label.Contains("Orar/Ler Bíblia")).Completed);
            Assert.Contains("- [ ] 07:30 - Orar/Ler Bíblia", await File.ReadAllTextAsync(dailyFile, Encoding.UTF8));

            // 3. Add and move Kanban task
            await service.AddKanbanTaskAsync("Profissional", "To Do", "Corrigir OportunityId no WhatsApp");
            var boardAfterMove = await service.MoveKanbanTaskAsync("Profissional", "Corrigir OportunityId", "Doing");

            var toDoCol = Assert.Single(boardAfterMove.Columns, c => c.Name == "To Do");
            var doingCol = Assert.Single(boardAfterMove.Columns, c => c.Name == "Doing");
            Assert.Empty(toDoCol.Tasks);
            Assert.Single(doingCol.Tasks, t => t.Text.Contains("Corrigir OportunityId"));

            // 4. Create structured notes (reuniao -> inbox, nota -> zettelkasten)
            var createdNote = await service.CreateStructuredNoteAsync(new DynamicNoteCreateRequest(
                NoteType: "nota",
                Title: "Arquitetura Generative UI",
                Content: "Conceito de renderização dinâmica com [[CSharp]] e Angular.",
                Area: "Engenharia",
                Tags: new[] { "ia", "angular" }));

            Assert.Equal("zettelkasten/Arquitetura Generative UI.md", createdNote.RelativePath);
            var noteMarkdown = await File.ReadAllTextAsync(Path.Combine(tempVault, "zettelkasten", "Arquitetura Generative UI.md"), Encoding.UTF8);
            Assert.Contains("type: nota", noteMarkdown);
            Assert.Contains("area: \"Engenharia\"", noteMarkdown);
            Assert.Contains("- angular", noteMarkdown);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }

    [Fact]
    public async Task IdeasPipeline_CapturesFleeting_StartsBrainstorm_AndPromotesPreservingNotesAndBacklinks()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainIdeasTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "zettelkasten"));
            var fleetingPath = Path.Combine(tempVault, "zettelkasten", "Fleeting.md");
            await File.WriteAllTextAsync(
                fleetingPath,
                "---\ntype: fleeting\n---\n---\n\n#### Ideias Rápidas\n- Software AEVO existente\n",
                Encoding.UTF8);

            var permanentePath = Path.Combine(tempVault, "zettelkasten", "Permanente.md");
            await File.WriteAllTextAsync(
                permanentePath,
                "---\ntype: permanente\n---\n---\n\n#### Ideias Maduras\n- [[GranFramework]]\n",
                Encoding.UTF8);

            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var service = new ObsidianVaultService(guard, initializer);

            // (a) Capture fleeting preserving existing notes and sanitizing ---
            var idea1 = await service.CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest(
                Text: "App de leitura com resumos por voz",
                Target: "fleeting",
                Links: new[] { "GranBrain" }));
            Assert.Equal("fleeting", idea1.Status);
            Assert.Equal("zettelkasten/Fleeting.md", idea1.SourcePath);

            var fleetingContent = await File.ReadAllTextAsync(fleetingPath, Encoding.UTF8);
            Assert.Contains("- Software AEVO existente", fleetingContent);
            Assert.DoesNotContain("---\n---", fleetingContent);
            Assert.Contains("App de leitura com resumos por voz", fleetingContent);
            Assert.Contains(idea1.Id, fleetingContent);

            // (b) Capture to inbox
            var idea2 = await service.CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest(
                Text: "Ideia rápida para triagem",
                Target: "inbox"));
            Assert.StartsWith("inbox/", idea2.SourcePath);
            var inboxFullPath = Path.Combine(tempVault, idea2.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(inboxFullPath));
            var inboxContent = await File.ReadAllTextAsync(inboxFullPath, Encoding.UTF8);
            Assert.Contains("type: fleeting", inboxContent);
            Assert.Contains("status: fleeting", inboxContent);
            Assert.Contains("[[Fleeting]]", inboxContent);

            // (c) Start brainstorm on idea1
            var session1 = await service.StartBrainstormSessionAsync(new BrainstormStartRequest(
                IdeaId: idea1.Id,
                Problem: "Leitores perdem insights ao ouvir audiolivros sem captura rápida.",
                Hypotheses: new[]
                {
                    new BrainstormHypothesisDto("Resumo por voz reduz fricção em 70%", "alta"),
                    new BrainstormHypothesisDto("Sincronização direta com Obsidian aumenta retenção", "média"),
                    new BrainstormHypothesisDto("Gamificação de leitura é essencial", "baixa")
                },
                Risks: new[]
                {
                    new BrainstormRiskDto("Latência de transcrição offline", "alto"),
                    new BrainstormRiskDto("Ruído ambiente em trânsito", "médio"),
                    new BrainstormRiskDto("Tamanho do arquivo de áudio", "baixo")
                },
                SocraticQuestion: "Qual evidência real prova que o usuário revisitaria os resumos?"));

            Assert.Equal("brainstorm", session1.Idea.Status);
            Assert.Equal("zettelkasten/Brainstorming - App de leitura com resumos por voz.md", session1.NotePath);
            var bsFullPath = Path.Combine(tempVault, session1.NotePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(bsFullPath));
            var bsContent = await File.ReadAllTextAsync(bsFullPath, Encoding.UTF8);
            Assert.Contains("type: brainstorming", bsContent);
            Assert.Contains("status: em_analise", bsContent);
            Assert.Contains("[[Fleeting]]", bsContent);
            Assert.Contains("Leitores perdem insights", bsContent);
            Assert.Contains("Resumo por voz reduz fricção em 70%", bsContent);
            Assert.Contains("Latência de transcrição offline", bsContent);
            Assert.Contains("Qual evidência real prova", bsContent);

            // (d) Promote idea1 to permanente twice (idempotency)
            var promo1 = await service.PromoteIdeaAsync(new IdeaPromoteRequest(
                IdeaId: idea1.Id,
                Target: "permanente",
                Summary: "Conceito validado de captura vocal de resumos.",
                SocraticAnswer: "Revisão semanal automática no Obsidian."));
            Assert.Equal("promovida", promo1.Status);
            await service.PromoteIdeaAsync(new IdeaPromoteRequest(
                IdeaId: idea1.Id,
                Target: "permanente"));

            var bsAfterPromo = await File.ReadAllTextAsync(bsFullPath, Encoding.UTF8);
            Assert.Contains("status: promovida", bsAfterPromo);
            Assert.Contains("- [x] **Promover a Conceito:**", bsAfterPromo);

            var permContent = await File.ReadAllTextAsync(permanentePath, Encoding.UTF8);
            Assert.Contains("- [[GranFramework]]", permContent);
            Assert.DoesNotContain("---\n---", permContent);
            var linkCount = System.Text.RegularExpressions.Regex.Matches(permContent, @"\[\[App de leitura com resumos por voz\]\]").Count;
            Assert.Equal(1, linkCount);

            var atomicPermPath = Path.Combine(tempVault, "zettelkasten", "App de leitura com resumos por voz.md");
            Assert.True(File.Exists(atomicPermPath));
            var atomicPermContent = await File.ReadAllTextAsync(atomicPermPath, Encoding.UTF8);
            Assert.Contains("[[Fleeting]]", atomicPermContent);
            Assert.Contains("[[Permanente]]", atomicPermContent);

            // (e) Brainstorm and promote idea2 to projeto
            await service.StartBrainstormSessionAsync(new BrainstormStartRequest(IdeaId: idea2.Id));
            var promo2 = await service.PromoteIdeaAsync(new IdeaPromoteRequest(
                IdeaId: idea2.Id,
                Target: "projeto",
                Area: "profissional"));
            Assert.Equal("promovida", promo2.Status);
            Assert.Equal("zettelkasten/Projeto - Ideia rápida para triagem.md", promo2.TargetPath);
            var projPath = Path.Combine(tempVault, "zettelkasten", "Projeto - Ideia rápida para triagem.md");
            Assert.True(File.Exists(projPath));
            var projContent = await File.ReadAllTextAsync(projPath, Encoding.UTF8);
            Assert.Contains("type: projeto", projContent);
            Assert.Contains("status: ativo", projContent);

            // (f) Capture idea3 and discard
            var idea3 = await service.CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest("Ideia descartável", "fleeting"));
            var promo3 = await service.PromoteIdeaAsync(new IdeaPromoteRequest(IdeaId: idea3.Id, Target: "descartada"));
            Assert.Equal("descartada", promo3.Status);
            Assert.Null(promo3.TargetPath);
            Assert.False(File.Exists(Path.Combine(tempVault, "zettelkasten", "Projeto - Ideia descartável.md")));
            Assert.DoesNotContain("[[Ideia descartável]]", await File.ReadAllTextAsync(permanentePath, Encoding.UTF8));

            // (g) Negative validations
            var emptyEx = await Assert.ThrowsAsync<VaultValidationException>(() =>
                service.CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest("   ", "fleeting")));
            Assert.Equal("idea_text_required", emptyEx.ErrorCode);

            var idea4 = await service.CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest("Ideia protegida contra mutação", "fleeting"));
            var invalidTargetEx = await Assert.ThrowsAsync<VaultValidationException>(() =>
                service.PromoteIdeaAsync(new IdeaPromoteRequest(idea4.Id, "invalido")));
            Assert.Equal("invalid_promotion_target", invalidTargetEx.ErrorCode);

            var notFoundBsEx = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                service.StartBrainstormSessionAsync(new BrainstormStartRequest(IdeaId: "inexistente")));
            Assert.Equal("idea_not_found", notFoundBsEx.ErrorCode);

            var notFoundPromoEx = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                service.PromoteIdeaAsync(new IdeaPromoteRequest("inexistente", "permanente")));
            Assert.Equal("idea_not_found", notFoundPromoEx.ErrorCode);

            // (h) Overview counts (legacy-fleeting + idea4 = 2 fleeting, 2 promoted, 1 discarded)
            var overview = await service.GetIdeasOverviewAsync();
            Assert.Equal(2, overview.PromotedCount);
            Assert.Equal(1, overview.DiscardedCount);
            Assert.Equal(2, overview.FleetingCount);
            Assert.NotNull(overview.ActiveSession);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }

    [Fact]
    public async Task HabitsParser_IsolatesCronogramaSection_AndIgnoresSyncedCalendarLines()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainHabitIsolationTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "daily"));
            var testDate = new DateOnly(2026, 10, 30);
            var dailyPath = Path.Combine(tempVault, "daily", "2026-10-30.md");

            // Note has a checkbox BEFORE #### Cronograma, zero real habits inside #### Cronograma,
            // synced calendar lines with <!-- cal-id:... -->, and a checkbox AFTER #### Cronograma.
            await File.WriteAllTextAsync(
                dailyPath,
                """
                ---
                type: daily
                created: "2026-10-30"
                ---

                ## Preparação
                - [ ] Checkbox fora do cronograma antes

                #### Cronograma

                - 09:00–09:30 · Daily do time Projeto X (Teams) <!-- cal-id:ms-daily -->
                - [ ] 15:00–16:00 · Revisão de arquitetura (Teams) <!-- cal-id:ms-arch -->

                #### Tarefas

                - [ ] Tarefa fora do cronograma depois
                """,
                Encoding.UTF8);

            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var service = new ObsidianVaultService(guard, initializer);

            // Even with 0 valid habits in #### Cronograma, checkboxes outside #### Cronograma must NOT be parsed
            var habits = await service.GetDailyHabitsAsync(testDate);
            Assert.Empty(habits);

            // Toggling outside or calendar-marked item must throw habit_not_found
            var exOutside = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                service.ToggleHabitAsync("Checkbox fora do cronograma", testDate));
            Assert.Equal("habit_not_found", exOutside.ErrorCode);

            var exCalendar = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                service.ToggleHabitAsync("Revisão de arquitetura", testDate));
            Assert.Equal("habit_not_found", exCalendar.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }
}
