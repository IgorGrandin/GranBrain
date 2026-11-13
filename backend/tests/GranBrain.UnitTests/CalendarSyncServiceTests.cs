using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Calendar;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.UnitTests;

public sealed class CalendarSyncServiceTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<Uri> RequestedUris { get; } = new();

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                RequestedUris.Add(request.RequestUri);
            }

            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    [Fact]
    public async Task FallbackMode_Returns5DeterministicEvents_WithStrictConflictAndZeroDiskTokens()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainCalFallback_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempVault);
            var options = new CalendarOptions(); // Empty credentials -> deterministic fallback
            var factory = new StubHttpClientFactory(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("Fallback mode must not make HTTP calls.")));
            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var vaultService = new ObsidianVaultService(guard, initializer);

            // Fixed clock: 2026-10-30 14:20 in America/Sao_Paulo (UTC-3 => 17:20Z)
            var fixedUtc = new DateTimeOffset(2026, 10, 30, 17, 20, 0, TimeSpan.Zero);
            var connectors = new ICalendarConnector[]
            {
                new GoogleCalendarConnector(options, factory),
                new MicrosoftGraphCalendarConnector(options, factory)
            };
            var syncService = new CalendarSyncService(connectors, options, guard, initializer, vaultService, () => fixedUtc);

            var today = await syncService.GetTodayAsync("2026-10-30");

            Assert.Equal("2026-10-30", today.Date);
            Assert.Equal(5, today.Events.Count);
            Assert.Contains(today.Events, e => e.Provider == CalendarProvider.Google);
            Assert.Contains(today.Events, e => e.Provider == CalendarProvider.Teams);
            Assert.Contains(today.Events, e => e.Provider == CalendarProvider.Outlook);

            // Adjacent 09:00-09:30 and 09:30-10:30 must NOT be marked as conflict
            var first = today.Events.First(e => e.StartTime == "09:00");
            var second = today.Events.First(e => e.StartTime == "09:30");
            Assert.Equal("09:30", first.EndTime);
            Assert.Equal("10:30", second.EndTime);
            Assert.False(first.HasConflict);
            Assert.False(second.HasConflict);
            Assert.Equal("past", first.Status);

            // 14:00-14:30 at 14:20 local must be "now"
            var current = today.Events.First(e => e.StartTime == "14:00");
            Assert.Equal("now", current.Status);

            // Overlapping 15:00-16:00 and 15:30-16:00 must be flagged as 1 conflict pair with 2 columns
            Assert.Equal(1, today.ConflictsCount);
            var clash1 = today.Events.First(e => e.StartTime == "15:00");
            var clash2 = today.Events.First(e => e.StartTime == "15:30");
            Assert.True(clash1.HasConflict);
            Assert.True(clash2.HasConflict);
            Assert.Equal("next", clash1.Status);
            Assert.Equal(2, clash1.CalendarTotalColumns);
            Assert.Equal(2, clash2.CalendarTotalColumns);
            Assert.NotEqual(clash1.CalendarColumn, clash2.CalendarColumn);

            // Zero token/credential files created inside tempVault
            Assert.Empty(Directory.EnumerateFiles(tempVault, "*", SearchOption.AllDirectories));
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
    public async Task RemoteConnectors_RefreshInMemory_ConvertTimeZone_ClassifyTeamsVsOutlook_AndHandleAuthErrors()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainCalOAuth_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempVault);
            var tokenRefreshCalls = 0;

            var handler = new StubHttpMessageHandler(req =>
            {
                var uri = req.RequestUri!.ToString();
                if (uri.Contains("oauth2.googleapis.com/token") || uri.Contains("login.microsoftonline.com"))
                {
                    tokenRefreshCalls++;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"access_token\":\"mem-token-123\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                    };
                }

                if (uri.Contains("googleapis.com/calendar"))
                {
                    var gJson = """
                        {
                          "items": [
                            {
                              "id": "g-1",
                              "summary": "Alinhamento UTC->SP",
                              "start": { "dateTime": "2026-10-30T13:30:00Z" },
                              "end": { "dateTime": "2026-10-30T14:15:00Z" },
                              "hangoutLink": "https://meet.google.com/abc-defg-hij",
                              "attendees": [ { "displayName": "Igor" } ]
                            },
                            {
                              "id": "g-late",
                              "summary": "Deploy Madrugada UTC (Ainda 30/10 em SP)",
                              "start": { "dateTime": "2026-10-31T01:30:00Z" },
                              "end": { "dateTime": "2026-10-31T02:00:00Z" }
                            },
                            {
                              "id": "g-cancelled",
                              "status": "cancelled",
                              "summary": "Reunião Cancelada Google",
                              "start": { "dateTime": "2026-10-30T11:00:00Z" },
                              "end": { "dateTime": "2026-10-30T11:30:00Z" }
                            }
                          ]
                        }
                        """;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(gJson, Encoding.UTF8, "application/json")
                    };
                }

                if (uri.Contains("graph.microsoft.com/v1.0/me/calendar/events"))
                {
                    var msJson = """
                        {
                          "value": [
                            {
                              "id": "ms-teams-flag",
                              "subject": "Daily Teams Flag",
                              "start": { "dateTime": "2026-10-30T12:00:00Z", "timeZone": "UTC" },
                              "end": { "dateTime": "2026-10-30T12:30:00Z", "timeZone": "UTC" },
                              "isOnlineMeeting": true,
                              "onlineMeeting": { "joinUrl": "https://teams.microsoft.com/l/meetup-join/1" }
                            },
                            {
                              "id": "ms-teams-url",
                              "subject": "Teams via URL apenas",
                              "start": { "dateTime": "2026-10-30T12:30:00Z", "timeZone": "UTC" },
                              "end": { "dateTime": "2026-10-30T13:00:00Z", "timeZone": "UTC" },
                              "isOnlineMeeting": false,
                              "onlineMeetingUrl": "https://teams.microsoft.com/l/meetup-join/2"
                            },
                            {
                              "id": "ms-cancelled",
                              "isCancelled": true,
                              "subject": "Reunião Cancelada Graph",
                              "start": { "dateTime": "2026-10-30T14:00:00Z", "timeZone": "UTC" },
                              "end": { "dateTime": "2026-10-30T14:30:00Z", "timeZone": "UTC" }
                            },
                            {
                              "id": "ms-outlook",
                              "subject": "Almoço Presencial",
                              "start": { "dateTime": "2026-10-30T15:30:00Z", "timeZone": "UTC" },
                              "end": { "dateTime": "2026-10-30T16:30:00Z", "timeZone": "UTC" },
                              "isOnlineMeeting": false,
                              "location": { "displayName": "Restaurante" }
                            }
                          ]
                        }
                        """;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(msJson, Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var options = new CalendarOptions
            {
                TimeZoneId = "America/Sao_Paulo",
                Google = new GoogleCalendarOptions
                {
                    ClientId = "g-client",
                    ClientSecret = "g-secret",
                    RefreshToken = "g-refresh"
                },
                Microsoft = new MicrosoftCalendarOptions
                {
                    ClientId = "ms-client",
                    ClientSecret = "ms-secret",
                    RefreshToken = "ms-refresh"
                }
            };

            var factory = new StubHttpClientFactory(handler);
            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var vaultService = new ObsidianVaultService(guard, initializer);
            var connectors = new ICalendarConnector[]
            {
                new GoogleCalendarConnector(options, factory),
                new MicrosoftGraphCalendarConnector(options, factory)
            };
            var syncService = new CalendarSyncService(connectors, options, guard, initializer, vaultService);

            var today = await syncService.GetTodayAsync("2026-10-30");
            Assert.Equal(5, today.Events.Count);

            // 2026-10-30T13:30:00Z -> 10:30 local in America/Sao_Paulo
            var g1 = Assert.Single(today.Events, e => e.Id == "g-1");
            Assert.Equal("10:30", g1.StartTime);
            Assert.Equal("11:15", g1.EndTime);
            Assert.Equal(CalendarProvider.Google, g1.Provider);

            // 2026-10-31T01:30:00Z -> 22:30 local on 2026-10-30 in America/Sao_Paulo
            var gLate = Assert.Single(today.Events, e => e.Id == "g-late");
            Assert.Equal("22:30", gLate.StartTime);

            // Teams vs Outlook classification + cancelled filtering
            var msTeams = Assert.Single(today.Events, e => e.Id == "ms-teams-flag");
            Assert.Equal(CalendarProvider.Teams, msTeams.Provider);
            var msTeamsUrl = Assert.Single(today.Events, e => e.Id == "ms-teams-url");
            Assert.Equal(CalendarProvider.Teams, msTeamsUrl.Provider);
            var msOutlook = Assert.Single(today.Events, e => e.Id == "ms-outlook");
            Assert.Equal(CalendarProvider.Outlook, msOutlook.Provider);
            Assert.DoesNotContain(today.Events, e => e.Id == "g-cancelled" || e.Id == "ms-cancelled");

            // Second call reuses in-memory cached access tokens (still 2 token calls total)
            await syncService.GetTodayAsync("2026-10-30");
            Assert.Equal(2, tokenRefreshCalls);
            Assert.Empty(Directory.EnumerateFiles(tempVault, "*", SearchOption.AllDirectories));

            // Partial credentials (ClientId without RefreshToken) -> calendar_auth_failed
            var partialOptions = new CalendarOptions
            {
                Google = new GoogleCalendarOptions { ClientId = "only-id" }
            };
            var partialService = new CalendarSyncService(
                new ICalendarConnector[] { new GoogleCalendarConnector(partialOptions, factory) },
                partialOptions, guard, initializer, vaultService);
            var partialEx = await Assert.ThrowsAsync<VaultValidationException>(() => partialService.GetTodayAsync("2026-10-30"));
            Assert.Equal("calendar_auth_failed", partialEx.ErrorCode);

            // OAuth 401 -> calendar_auth_failed
            var unauthFactory = new StubHttpClientFactory(new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.Unauthorized)));
            var unauthService = new CalendarSyncService(
                new ICalendarConnector[] { new GoogleCalendarConnector(options, unauthFactory) },
                options, guard, initializer, vaultService);
            var authEx = await Assert.ThrowsAsync<VaultValidationException>(() => unauthService.GetTodayAsync("2026-10-30"));
            Assert.Equal("calendar_auth_failed", authEx.ErrorCode);

            // Calendar GET 401 -> evicts cache and throws calendar_auth_failed
            var get401Factory = new StubHttpClientFactory(new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.AbsoluteUri.Contains("oauth2.googleapis.com"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"access_token":"expired-access","expires_in":3600}""", Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }));
            var get401Service = new CalendarSyncService(
                new ICalendarConnector[] { new GoogleCalendarConnector(options, get401Factory) },
                options, guard, initializer, vaultService);
            var get401Ex = await Assert.ThrowsAsync<VaultValidationException>(() => get401Service.GetTodayAsync("2026-10-30"));
            Assert.Equal("calendar_auth_failed", get401Ex.ErrorCode);
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
    public async Task SyncAsync_IsIdempotent_PreservesHabits_AndHandlesMissingReunioesSection()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainCalSync_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "templates"));
            Directory.CreateDirectory(Path.Combine(tempVault, "daily"));

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

                - [ ] 07:30 - Orar/Ler Bíblia
                - [ ] 09:00 - Iniciar Trabalho

                #### Anotações
                - Nota manual livre

                ## 🤝🏻 Reuniões

                - 

                ```dataview
                LIST
                FROM ""
                WHERE type = "reuniao"
                ```
                """,
                Encoding.UTF8);

            var options = new CalendarOptions();
            var factory = new StubHttpClientFactory(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var vaultService = new ObsidianVaultService(guard, initializer);
            var connectors = new ICalendarConnector[]
            {
                new GoogleCalendarConnector(options, factory),
                new MicrosoftGraphCalendarConnector(options, factory)
            };
            var syncService = new CalendarSyncService(connectors, options, guard, initializer, vaultService);

            var date = "2026-10-30";
            var sync1 = await syncService.SyncAsync(date);
            Assert.Equal("daily/2026-10-30.md", sync1.DailyNotePath);
            Assert.Equal(5, sync1.SyncedCount);

            // Toggle real habit after first sync
            var habits = await vaultService.ToggleHabitAsync("Orar/Ler Bíblia", new DateOnly(2026, 10, 30));
            Assert.True(Assert.Single(habits, h => h.Label.Contains("Orar/Ler Bíblia")).Completed);

            // Second sync must NOT duplicate cal-id lines and MUST preserve [x] habit & free notes
            await syncService.SyncAsync(date);

            var dailyFullPath = Path.Combine(tempVault, "daily", "2026-10-30.md");
            var dailyText = await File.ReadAllTextAsync(dailyFullPath, Encoding.UTF8);
            Assert.Contains("- [x] 07:30 - Orar/Ler Bíblia", dailyText);
            Assert.Contains("- Nota manual livre", dailyText);

            // Check no duplicate cal-id markers in #### Cronograma or ## 🤝🏻 Reuniões
            foreach (var ev in sync1.Agenda.Events)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(dailyText, $"<!-- cal-id:{System.Text.RegularExpressions.Regex.Escape(ev.Id)} -->").Count;
                Assert.Equal(2, matches); // once in #### Cronograma, once in ## 🤝🏻 Reuniões
            }

            // Meetings appear BEFORE ```dataview
            var reunioesIdx = dailyText.IndexOf("## 🤝🏻 Reuniões", StringComparison.Ordinal);
            var firstCalInReunioes = dailyText.IndexOf("<!-- cal-id:", reunioesIdx, StringComparison.Ordinal);
            var dataviewIdx = dailyText.IndexOf("```dataview", reunioesIdx, StringComparison.Ordinal);
            Assert.True(reunioesIdx >= 0 && firstCalInReunioes > reunioesIdx && firstCalInReunioes < dataviewIdx);

            // Toggling a synced calendar meeting as a habit must throw habit_not_found
            var toggleEx = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                vaultService.ToggleHabitAsync("Revisão de arquitetura", new DateOnly(2026, 10, 30)));
            Assert.Equal("habit_not_found", toggleEx.ErrorCode);

            // Fallback daily note WITHOUT ## 🤝🏻 Reuniões must append ## 🤝🏻 Reuniões cleanly
            var fallbackDaily = Path.Combine(tempVault, "daily", "2026-10-31.md");
            await File.WriteAllTextAsync(fallbackDaily, "# 2026-10-31\n\n#### Cronograma\n- [ ] 08:00 - Meditar\n", Encoding.UTF8);
            await syncService.SyncAsync("2026-10-31");
            var fallbackText = await File.ReadAllTextAsync(fallbackDaily, Encoding.UTF8);
            Assert.Contains("## 🤝🏻 Reuniões", fallbackText);
            Assert.Contains("<!-- cal-id:", fallbackText);
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
    public async Task CreateMeetingNoteAsync_PreservesManualSections_AndEnforcesVaultJailAndValidation()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainCalNotes_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempVault);
            var options = new CalendarOptions();
            var factory = new StubHttpClientFactory(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var guard = new VaultPathGuard(tempVault);
            var initializer = new VaultInitializer(guard);
            var vaultService = new ObsidianVaultService(guard, initializer);
            var connectors = new ICalendarConnector[]
            {
                new GoogleCalendarConnector(options, factory),
                new MicrosoftGraphCalendarConnector(options, factory)
            };
            var syncService = new CalendarSyncService(connectors, options, guard, initializer, vaultService);

            var today = await syncService.GetTodayAsync("2026-10-30");
            var targetEvent = today.Events.First(e => e.Title.Contains("Alinhamento GranBrain"));

            // 1. Create meeting note
            var noteResult1 = await syncService.CreateMeetingNoteAsync(targetEvent.Id, date: "2026-10-30");
            Assert.True(noteResult1.Created);
            Assert.Equal("zettelkasten/Reunião - Alinhamento GranBrain.md", noteResult1.NotePath);

            var fullNotePath = Path.Combine(tempVault, "zettelkasten", "Reunião - Alinhamento GranBrain.md");
            Assert.True(File.Exists(fullNotePath));

            // Simulate user editing Discussão & Plano de Ação manually
            var content = await File.ReadAllTextAsync(fullNotePath, Encoding.UTF8);
            content = content
                .Replace("## 📝 Discussão & Anotações\n-", "## 📝 Discussão & Anotações\n- Decisão manual importante do Igor")
                .Replace("## ✅ Plano de Ação & Tarefas\n- [ ]", "## ✅ Plano de Ação & Tarefas\n- [x] Revisar PR do GranBrain");
            await File.WriteAllTextAsync(fullNotePath, content, Encoding.UTF8);

            // 2. Calling CreateMeetingNoteAsync again must preserve manual edits
            var noteResult2 = await syncService.CreateMeetingNoteAsync(targetEvent.Id, date: "2026-10-30");
            Assert.False(noteResult2.Created);
            var updatedContent = await File.ReadAllTextAsync(fullNotePath, Encoding.UTF8);
            Assert.Contains("Decisão manual importante do Igor", updatedContent);
            Assert.Contains("- [x] Revisar PR do GranBrain", updatedContent);

            // 3. Vault Jail: raw TargetFolder "../../evil" must throw VaultPathViolationException before sanitization
            var syncTraversal = await Assert.ThrowsAsync<VaultPathViolationException>(() =>
                syncService.SyncAsync("2026-10-30", "../../evil"));
            Assert.Equal("vault_path_violation", syncTraversal.ErrorCode);

            var noteTraversal = await Assert.ThrowsAsync<VaultPathViolationException>(() =>
                syncService.CreateMeetingNoteAsync(targetEvent.Id, "../../evil", "2026-10-30"));
            Assert.Equal("vault_path_violation", noteTraversal.ErrorCode);

            // 4. Unknown event id -> calendar_event_not_found
            var missingEx = await Assert.ThrowsAsync<VaultEntityNotFoundException>(() =>
                syncService.CreateMeetingNoteAsync("evento-inexistente", date: "2026-10-30"));
            Assert.Equal("calendar_event_not_found", missingEx.ErrorCode);

            // 5. Invalid date format -> invalid_calendar_date
            var invalidDateEx = await Assert.ThrowsAsync<VaultValidationException>(() =>
                syncService.GetTodayAsync("30-10-2026"));
            Assert.Equal("invalid_calendar_date", invalidDateEx.ErrorCode);
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
