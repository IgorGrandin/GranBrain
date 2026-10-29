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
}
