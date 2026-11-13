using System.Text;
using System.Text.RegularExpressions;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Jobs;
using GranBrain.Infrastructure.Vault;
using Microsoft.Extensions.Logging.Abstractions;

namespace GranBrain.UnitTests;

public sealed class RoutineExecutionServiceTests
{
    private sealed class FakeCliProcessRunner : ICliProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, CliProcessResult> _handler;
        public int CallsCount { get; private set; }

        public FakeCliProcessRunner(Func<string, IReadOnlyList<string>, CliProcessResult> handler)
        {
            _handler = handler;
        }

        public bool IsExecutableAvailable(string executableOrCommand) => true;

        public Task<CliProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            int timeoutSeconds,
            CancellationToken ct = default)
        {
            CallsCount++;
            return Task.FromResult(_handler(executable, arguments));
        }
    }

    private static string CreateTempVault(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static RoutineExecutionService CreateService(
        string vaultPath,
        RoutinesOptions? options = null,
        ICliProcessRunner? cliRunner = null,
        Func<DateTimeOffset>? clock = null)
    {
        var guard = new VaultPathGuard(vaultPath);
        var initializer = new VaultInitializer(guard);
        var vaultService = new ObsidianVaultService(guard, initializer);
        return new RoutineExecutionService(
            options ?? new RoutinesOptions(),
            guard,
            initializer,
            vaultService,
            cliRunner,
            new GranBrainOptions { VaultPath = vaultPath },
            clock ?? (() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task InboxTriageJob_PromotesMdAndTxtNotes_RoutesMeetings_PreservesExistingZettel_AndArchivesOriginals()
    {
        var tempVault = CreateTempVault("GranBrainTriage");
        try
        {
            var indicesDir = Path.Combine(tempVault, "zettelkasten", "Índices");
            Directory.CreateDirectory(indicesDir);
            Directory.CreateDirectory(Path.Combine(tempVault, "inbox", "_archive"));

            await File.WriteAllTextAsync(Path.Combine(indicesDir, "CSharp.md"), "# CSharp", Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(indicesDir, "Curso de Seguros - Confitec.md"), "# Curso de Seguros", Encoding.UTF8);

            var existingZettelPath = Path.Combine(tempVault, "zettelkasten", "Estudo LINQ.md");
            const string existingZettelOriginal = "# Estudo LINQ\n\nNota permanente protegida do usuário.";
            await File.WriteAllTextAsync(existingZettelPath, existingZettelOriginal, Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Ideia sobre flashcards.txt"),
                "Criar deck de flashcards para revisar conceitos do Curso de Seguros - Confitec.",
                Encoding.UTF8);

            const string ataOriginal = "---\ntype: fleeting\ntags:\n  - arquitetura\n---\n---\n\nParticipantes: Igor e equipe. Decisão sobre C# e filas.";
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Ata da reunião arquitetura.md"),
                ataOriginal,
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Estudo LINQ.md"),
                "Anotações extras sobre LINQ e expressões lambda em C#.",
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Captura rápida.md"),
                "Insight de estudo em trânsito #mobile-capture",
                Encoding.UTF8);

            // Already archived file must be ignored by TopDirectoryOnly enumeration
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "_archive", "anterior.md"),
                "Arquivo já arquivado",
                Encoding.UTF8);

            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var vaultService = new ObsidianVaultService(guard, initializer);
            var service = CreateService(tempVault);

            var vaultOverviewBefore = await vaultService.GetOverviewAsync(new DateOnly(2026, 10, 30));
            var routineOverviewBefore = await service.GetOverviewAsync();
            Assert.Equal(4, vaultOverviewBefore.InboxPendingCount);
            Assert.Equal(4, routineOverviewBefore.InboxPendingCount);

            var result = await service.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(result.Succeeded);
            Assert.Equal(4, result.ProcessedCount);
            Assert.Equal(0, result.FailedCount);

            var meetingZettelPath = Path.Combine(tempVault, "zettelkasten", "Reunião - Ata da reunião arquitetura.md");
            var txtZettelPath = Path.Combine(tempVault, "zettelkasten", "Ideia sobre flashcards.md");
            var collisionZettelPath = Path.Combine(tempVault, "zettelkasten", "Estudo LINQ - 2.md");
            var mobileZettelPath = Path.Combine(tempVault, "zettelkasten", "Captura rápida.md");

            Assert.True(File.Exists(meetingZettelPath));
            Assert.True(File.Exists(txtZettelPath));
            Assert.True(File.Exists(collisionZettelPath));
            Assert.True(File.Exists(mobileZettelPath));

            // Existing permanent note must remain untouched
            Assert.Equal(existingZettelOriginal, await File.ReadAllTextAsync(existingZettelPath, Encoding.UTF8));

            var meetingContent = await File.ReadAllTextAsync(meetingZettelPath, Encoding.UTF8);
            Assert.Single(Regex.Matches(meetingContent, @"^---\n[\s\S]*?\n---", RegexOptions.Multiline));
            Assert.Contains("type: reuniao", meetingContent);
            Assert.Contains("source_inbox:", meetingContent);
            Assert.Contains("[[Fleeting]]", meetingContent);
            Assert.Contains("[[CSharp]]", meetingContent);

            var txtContent = await File.ReadAllTextAsync(txtZettelPath, Encoding.UTF8);
            Assert.Contains("[[Fleeting]]", txtContent);
            Assert.Contains("[[Curso de Seguros - Confitec]]", txtContent);

            var mobileContent = await File.ReadAllTextAsync(mobileZettelPath, Encoding.UTF8);
            Assert.Contains("mobile-capture", mobileContent);

            // Originals moved intact to inbox/_archive/
            var archivedAta = Path.Combine(tempVault, "inbox", "_archive", "Ata da reunião arquitetura.md");
            Assert.True(File.Exists(archivedAta));
            Assert.Equal(ataOriginal, await File.ReadAllTextAsync(archivedAta, Encoding.UTF8));
            Assert.False(File.Exists(Path.Combine(tempVault, "inbox", "Ata da reunião arquitetura.md")));

            // Re-triaging a file with the same name creates collision-safe archive copy
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Ata da reunião arquitetura.md"),
                "Segunda passagem com mesmo nome.",
                Encoding.UTF8);
            var secondResult = await service.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(secondResult.Succeeded);
            Assert.True(File.Exists(Path.Combine(tempVault, "inbox", "_archive", "Ata da reunião arquitetura - 2.md")));

            var vaultOverviewAfter = await vaultService.GetOverviewAsync(new DateOnly(2026, 10, 30));
            var routineOverviewAfter = await service.GetOverviewAsync();
            Assert.Equal(0, vaultOverviewAfter.InboxPendingCount);
            Assert.Equal(0, routineOverviewAfter.InboxPendingCount);
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
    public async Task InboxTriageJob_WithUseCliTrue_AppliesCliOutputAndFallsBackToHeuristicOnCliFailure()
    {
        var tempVault = CreateTempVault("GranBrainTriageCli");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "inbox"));
            Directory.CreateDirectory(Path.Combine(tempVault, "zettelkasten", "Índices"));
            await File.WriteAllTextAsync(Path.Combine(tempVault, "zettelkasten", "Índices", "CSharp.md"), "# CSharp", Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Anotação bruta 1.md"),
                "Discussão técnica de arquitetura.",
                Encoding.UTF8);

            var cliSuccessRunner = new FakeCliProcessRunner((_, _) => new CliProcessResult(
                Executed: true,
                ExitCode: 0,
                StdOut: "```json\n{\"category\":\"reuniao\",\"title\":\"Sync Arquitetura CLI\",\"tags\":[\"cli-tag\"],\"mocs\":[\"CSharp\"]}\n```",
                StdErr: string.Empty));

            var serviceWithCli = CreateService(
                tempVault,
                new RoutinesOptions { UseCli = true },
                cliSuccessRunner);

            var cliRun = await serviceWithCli.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(cliRun.Succeeded);
            Assert.Equal(1, cliSuccessRunner.CallsCount);
            var cliNotePath = Path.Combine(tempVault, "zettelkasten", "Reunião - Sync Arquitetura CLI.md");
            Assert.True(File.Exists(cliNotePath));
            var cliNoteContent = await File.ReadAllTextAsync(cliNotePath, Encoding.UTF8);
            Assert.Contains("cli-tag", cliNoteContent);
            Assert.Contains("[[CSharp]]", cliNoteContent);

            // Second file with failing / invalid CLI output must fall back to deterministic heuristic
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "inbox", "Ideia fallback deterministico.md"),
                "Estudo de padrões de projeto sem CLI.",
                Encoding.UTF8);

            var cliFailRunner = new FakeCliProcessRunner((_, _) => new CliProcessResult(
                Executed: false,
                ExitCode: -1,
                StdOut: "NOT_JSON_AT_ALL",
                StdErr: "CLI timeout"));

            var serviceWithFallback = CreateService(
                tempVault,
                new RoutinesOptions { UseCli = true },
                cliFailRunner);

            var fallbackRun = await serviceWithFallback.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(fallbackRun.Succeeded);
            Assert.Equal(1, fallbackRun.ProcessedCount);
            Assert.Equal(1, cliFailRunner.CallsCount);
            Assert.True(File.Exists(Path.Combine(tempVault, "zettelkasten", "Ideia fallback deterministico.md")));
            Assert.True(File.Exists(Path.Combine(tempVault, "inbox", "_archive", "Ideia fallback deterministico.md")));
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
    public async Task NightlyClosureJob_UpsertsUpdatedMetricsIdempotently_IgnoresCalendarLines_AndPreservesUserDiary()
    {
        var tempVault = CreateTempVault("GranBrainNightly");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "daily"));
            Directory.CreateDirectory(Path.Combine(tempVault, "assets", "kanban"));

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Profissional.md"),
                "## To Do\n- [ ] Tarefa A\n\n## Doing\n- [ ] Tarefa B\n\n## Done\n- [x] Tarefa C\n",
                Encoding.UTF8);

            var dailyPath = Path.Combine(tempVault, "daily", "2026-10-30.md");
            await File.WriteAllTextAsync(
                dailyPath,
                """
                ---
                type: daily
                created: "2026-10-30"
                tags:
                  - daily
                ---

                # 30 October, 2026

                #### Cronograma

                - [x] 07:00 - Orar/Ler Bíblia
                - [ ] 08:15 - Exercícios Matinais
                - [ ] 12:30 - Almoço
                - 09:00–09:30 · Daily (Teams) <!-- cal-id:ms-daily -->

                #### Diário
                Dia produtivo escrevendo testes reais.

                #### Reflexões do Dia

                > **Reflexões:**
                Aprendizado manual que não pode sumir.

                > **Agradecimentos:**
                Pela família e saúde.

                #### Anotações
                Nota livre do usuário.
                """,
                Encoding.UTF8);

            var service = CreateService(tempVault);

            // Run 1: 1/3 habits -> 33%
            var run1 = await service.RunJobAsync("NightlyClosureJob", new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(run1.Succeeded);

            var content1 = await File.ReadAllTextAsync(dailyPath, Encoding.UTF8);
            Assert.Contains("1/3 (33%)", content1);
            Assert.Contains("Aprendizado manual que não pode sumir.", content1);
            Assert.Contains("Pela família e saúde.", content1);
            Assert.Contains("Dia produtivo escrevendo testes reais.", content1);
            Assert.Contains("Nota livre do usuário.", content1);
            Assert.Single(Regex.Matches(content1, @"<!-- routine-id:nightly-closure -->"));

            // Check a second habit -> 2/3 habits -> 67% on re-run (idempotent upsert, same 1 line)
            content1 = content1.Replace("- [ ] 08:15 - Exercícios Matinais", "- [x] 08:15 - Exercícios Matinais");
            await File.WriteAllTextAsync(dailyPath, content1, Encoding.UTF8);

            var run2 = await service.RunJobAsync("NightlyClosureJob", new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(run2.Succeeded);

            var content2 = await File.ReadAllTextAsync(dailyPath, Encoding.UTF8);
            Assert.Contains("2/3 (67%)", content2);
            Assert.DoesNotContain("1/3 (33%)", content2);
            Assert.Single(Regex.Matches(content2, @"<!-- routine-id:nightly-closure -->"));

            // Minimal note with 0 habits and missing #### Reflexões do Dia -> 0/0 (0%) without DivideByZeroException
            var minimalDailyPath = Path.Combine(tempVault, "daily", "2026-10-31.md");
            await File.WriteAllTextAsync(minimalDailyPath, "# Nota mínima\n\n#### Cronograma\n- 10:00–11:00 · Call <!-- cal-id:g-1 -->\n", Encoding.UTF8);

            var minimalRun = await service.RunJobAsync("NightlyClosureJob", new RoutineRunRequest(Date: "2026-10-31"));
            Assert.True(minimalRun.Succeeded);
            var minimalContent = await File.ReadAllTextAsync(minimalDailyPath, Encoding.UTF8);
            Assert.Contains("#### Reflexões do Dia", minimalContent);
            Assert.Contains("0/0 (0%)", minimalContent);
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
    public async Task MorningPrepJob_NormalizesNewDailyHabits_PreservesExistingCheckedHabits_AndUpsertsKanbanTasks()
    {
        var tempVault = CreateTempVault("GranBrainMorning");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "templates"));
            Directory.CreateDirectory(Path.Combine(tempVault, "assets", "kanban"));

            // Template intentionally has - [x] to prove normalization to - [ ] when creating a new daily note
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "templates", "Notas Diárias.md"),
                """
                ---
                type: daily
                created: "{{date:YYYY-MM-DD}}"
                tags:
                  - daily
                ---

                # {{date:DD MMMM, YYYY (dddd)}}

                #### Cronograma
                - [x] 07:30 - Orar/Ler Bíblia
                - [ ] 08:15 - Exercícios Matinais

                #### Tarefas
                - [[Profissional]]
                - [[Pessoal]]
                """,
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Profissional.md"),
                "## To Do\n- [ ] Revisar PR do deploy\n\n## Doing\n- [ ] Implementar worker\n\n## Done\n- [x] Tarefa concluída\n\n## Blocked\n- [ ] Tarefa bloqueada\n",
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Pessoal.md"),
                "## To Do\n- [ ] Comprar café\n\n## Done\n- [x] Pagar conta\n",
                Encoding.UTF8);

            var service = CreateService(tempVault);
            var dailyPath = Path.Combine(tempVault, "daily", "2026-10-30.md");

            // Run 1: creates daily note normalizing [x] -> [ ] and pulling only To Do / Doing
            var run1 = await service.RunJobAsync("MorningPrepJob", new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(run1.Succeeded);
            Assert.True(File.Exists(dailyPath));

            var content1 = await File.ReadAllTextAsync(dailyPath, Encoding.UTF8);
            Assert.Contains("- [ ] 07:30 - Orar/Ler Bíblia", content1);
            Assert.DoesNotContain("- [x] 07:30 - Orar/Ler Bíblia", content1);
            Assert.Contains("- [[Profissional]]", content1);
            Assert.Contains("- [[Pessoal]]", content1);
            Assert.Contains("Revisar PR do deploy", content1);
            Assert.Contains("Implementar worker", content1);
            Assert.Contains("Comprar café", content1);
            Assert.DoesNotContain("Tarefa concluída", content1);
            Assert.DoesNotContain("Tarefa bloqueada", content1);
            Assert.DoesNotContain("Pagar conta", content1);

            // User checks a habit and Kanban changes
            content1 = content1.Replace("- [ ] 07:30 - Orar/Ler Bíblia", "- [x] 07:30 - Orar/Ler Bíblia");
            await File.WriteAllTextAsync(dailyPath, content1, Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Profissional.md"),
                "## To Do\n- [ ] Nova tarefa urgente\n\n## Doing\n\n## Done\n- [x] Revisar PR do deploy\n",
                Encoding.UTF8);

            // Run 2: preserves existing [x] habit and updates <!-- routine-id:morning-prep --> block without duplicates
            var run2 = await service.RunJobAsync("MorningPrepJob", new RoutineRunRequest(Date: "2026-10-30"));
            Assert.True(run2.Succeeded);

            var content2 = await File.ReadAllTextAsync(dailyPath, Encoding.UTF8);
            Assert.Contains("- [x] 07:30 - Orar/Ler Bíblia", content2);
            Assert.Contains("Nova tarefa urgente", content2);
            Assert.DoesNotContain("Revisar PR do deploy", content2);
            Assert.Contains("- [[Profissional]]", content2);
            Assert.Contains("- [[Pessoal]]", content2);
            Assert.Equal(2, Regex.Matches(content2, @"<!-- routine-id:morning-prep -->").Count);
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
    public async Task FaultIsolationConcurrencyAndScheduler_IsolatesCorruptFile_SerializesLogs_AndRespectsSchedulerFlag()
    {
        var tempVault = CreateTempVault("GranBrainFaultAndSched");
        try
        {
            var inboxDir = Path.Combine(tempVault, "inbox");
            var archiveDir = Path.Combine(inboxDir, "_archive");
            Directory.CreateDirectory(archiveDir);

            // Pre-corrupt audit log JSON must be tolerated safely
            var auditLogPath = Path.Combine(archiveDir, "routines-audit-log.json");
            await File.WriteAllTextAsync(auditLogPath, "{ invalid json !!!", Encoding.UTF8);

            var lockedPath = Path.Combine(inboxDir, "01-arquivo-travado.md");
            var healthyPath = Path.Combine(inboxDir, "02-arquivo-saudavel.md");
            await File.WriteAllTextAsync(lockedPath, "Conteúdo bloqueado", Encoding.UTF8);
            await File.WriteAllTextAsync(healthyPath, "Conceito saudável de estudo", Encoding.UTF8);

            var service = CreateService(tempVault);

            // 1. VaultPathViolationException on traversal before SanitizeFileName
            await Assert.ThrowsAsync<VaultPathViolationException>(() =>
                service.RunInboxTriageAsync(new RoutineRunRequest(InboxFolder: "../../evil")));

            // 2. Isolate locked file IOException while healthy sibling still processes
            using (var exclusiveLock = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var triageResult = await service.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30"));
                Assert.False(triageResult.Succeeded);
                Assert.Equal(1, triageResult.FailedCount);
                Assert.Equal(1, triageResult.ProcessedCount);
                Assert.True(File.Exists(Path.Combine(tempVault, "zettelkasten", "02-arquivo-saudavel.md")));
                Assert.True(File.Exists(lockedPath));
            }

            // 3. Concurrent Task.WhenAll serialized safely by SemaphoreSlim + atomic log write
            await File.WriteAllTextAsync(Path.Combine(inboxDir, "03-concorrente-a.md"), "Nota A", Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(inboxDir, "04-concorrente-b.md"), "Nota B", Encoding.UTF8);

            var concurrentResults = await Task.WhenAll(
                service.RunInboxTriageAsync(new RoutineRunRequest(Date: "2026-10-30")),
                service.RunJobAsync("MorningPrepJob", new RoutineRunRequest(Date: "2026-10-30")),
                service.RunJobAsync("NightlyClosureJob", new RoutineRunRequest(Date: "2026-10-30")));

            Assert.All(concurrentResults, r => Assert.NotNull(r.Overview));
            var overviewAfter = await service.GetOverviewAsync();
            Assert.Equal(4, overviewAfter.Logs.Count);
            var concurrentDaily = await File.ReadAllTextAsync(Path.Combine(tempVault, "daily", "2026-10-30.md"), Encoding.UTF8);
            Assert.Contains("<!-- routine-id:nightly-closure -->", concurrentDaily);

            // 4. RoutineSchedulerHostedService respects EnableBackgroundScheduler flag & time windows
            var disabledScheduler = new RoutineSchedulerHostedService(
                service,
                new RoutinesOptions { EnableBackgroundScheduler = false },
                NullLogger<RoutineSchedulerHostedService>.Instance,
                () => new DateTimeOffset(2026, 10, 30, 23, 45, 0, TimeSpan.FromHours(-3)));

            var logsBeforeDisabledTick = (await service.GetOverviewAsync()).Logs.Count;
            await disabledScheduler.ExecuteTickAsync();
            var logsAfterDisabledTick = (await service.GetOverviewAsync()).Logs.Count;
            Assert.Equal(logsBeforeDisabledTick, logsAfterDisabledTick);

            var fixedLocalClock = new DateTimeOffset(2026, 10, 30, 23, 35, 0, TimeSpan.FromHours(-3));
            var enabledScheduler = new RoutineSchedulerHostedService(
                service,
                new RoutinesOptions
                {
                    EnableBackgroundScheduler = true,
                    MorningPrepTime = "06:00",
                    NightlyClosureTime = "23:30",
                    InboxIntervalMinutes = 120,
                    TimeZoneId = "America/Sao_Paulo"
                },
                NullLogger<RoutineSchedulerHostedService>.Instance,
                () => fixedLocalClock);

            await enabledScheduler.ExecuteTickAsync();
            var logsAfterFirstTick = (await service.GetOverviewAsync()).Logs.Count;
            Assert.Equal(logsAfterDisabledTick + 3, logsAfterFirstTick);

            // Same tick / window does not duplicate runs
            await enabledScheduler.ExecuteTickAsync();
            var logsAfterSecondTick = (await service.GetOverviewAsync()).Logs.Count;
            Assert.Equal(logsAfterFirstTick, logsAfterSecondTick);
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
