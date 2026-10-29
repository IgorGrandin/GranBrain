using System.Text;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.UnitTests;

public sealed class VaultInitializerTests
{
    [Fact]
    public async Task EnsureInitializedAsync_CreatesGovernanceFiles_FixesTemplates_AndPreservesExistingNotes()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainInitTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "templates"));
            Directory.CreateDirectory(Path.Combine(tempVault, "daily"));

            var brokenTemplatePath = Path.Combine(tempVault, "templates", "Notas.md");
            var brokenTemplateContent = "---\ncreated: \"{{date}} {{time}}\"\ntags:\n  - nota\n---\n---\n\n#### Descrição\n";
            await File.WriteAllTextAsync(brokenTemplatePath, brokenTemplateContent, Encoding.UTF8);

            var dailyTemplatePath = Path.Combine(tempVault, "templates", "Notas Diárias.md");
            await File.WriteAllTextAsync(dailyTemplatePath, "# {{date:DD MMMM, YYYY (dddd)}}\n#### Cronograma\n- [ ] 07:00 - Colocar Alanna na perua\n", Encoding.UTF8);

            var existingDailyNotePath = Path.Combine(tempVault, "daily", "2026-10-13.md");
            var originalNoteBytes = Encoding.UTF8.GetBytes("# 13 October, 2026\n- [x] Nota original intacta!");
            await File.WriteAllBytesAsync(existingDailyNotePath, originalNoteBytes);

            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);

            // First execution
            var result1 = await initializer.EnsureInitializedAsync();
            Assert.True(result1.CreatedAgentsMd);
            Assert.True(result1.CreatedHomeMd);
            Assert.True(result1.CreatedInboxDirectory);
            Assert.Contains("Notas.md", result1.SanitizedTemplates);
            Assert.Contains("Notas Diárias.md", result1.SanitizedTemplates);

            Assert.True(File.Exists(Path.Combine(tempVault, "AGENTS.md")));
            Assert.True(File.Exists(Path.Combine(tempVault, "Home.md")));
            Assert.True(Directory.Exists(Path.Combine(tempVault, "inbox")));

            var fixedNotas = await File.ReadAllTextAsync(brokenTemplatePath, Encoding.UTF8);
            Assert.DoesNotContain("---\n---", fixedNotas);

            var fixedDaily = await File.ReadAllTextAsync(dailyTemplatePath, Encoding.UTF8);
            Assert.StartsWith("---\ntype: daily", fixedDaily);

            // Existing note preserved byte-for-byte
            var afterNoteBytes = await File.ReadAllBytesAsync(existingDailyNotePath);
            Assert.Equal(originalNoteBytes, afterNoteBytes);

            // Second execution (idempotency check)
            var result2 = await initializer.EnsureInitializedAsync();
            Assert.False(result2.CreatedAgentsMd);
            Assert.False(result2.CreatedHomeMd);
            Assert.False(result2.CreatedInboxDirectory);
            Assert.Empty(result2.SanitizedTemplates);
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
