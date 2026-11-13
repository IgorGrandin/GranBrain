using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GranBrain.Api.Endpoints;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GranBrain.UnitTests;

public sealed class ApiSecurityAndAgentTests
{
    private sealed class FakeCliProcessRunner : ICliProcessRunner
    {
        public string StdOutToReturn { get; set; } = string.Empty;
        public bool Installed { get; set; } = true;
        public IReadOnlyList<string>? LastArguments { get; private set; }

        public bool IsExecutableAvailable(string executableOrCommand) => Installed;

        public Task<CliProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            int timeoutSeconds,
            CancellationToken ct = default)
        {
            LastArguments = arguments;
            if (!Installed)
            {
                return Task.FromResult(new CliProcessResult(false, -1, string.Empty, "Binary not installed"));
            }

            return Task.FromResult(new CliProcessResult(true, 0, StdOutToReturn, string.Empty));
        }
    }

    private static WebApplicationFactory<Program> CreateTestFactory(string tempVault, ICliProcessRunner? customCli = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("GranBrain:VaultPath", tempVault);
            builder.UseSetting("GranBrain:Auth:Username", "igor");
            builder.UseSetting("GranBrain:Auth:DefaultPassword", "GranBrain@2026");
            builder.UseSetting("GranBrain:Auth:MaxLoginAttemptsPerWindow", "5");
            builder.UseSetting("GranBrain:Auth:LoginWindowSeconds", "60");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GranBrain:VaultPath"] = tempVault,
                    ["GranBrain:Auth:Username"] = "igor",
                    ["GranBrain:Auth:DefaultPassword"] = "GranBrain@2026",
                    ["GranBrain:Auth:MaxLoginAttemptsPerWindow"] = "5",
                    ["GranBrain:Auth:LoginWindowSeconds"] = "60"
                });
            });

            if (customCli is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(customCli);
                });
            }
        });
    }

    [Fact]
    public async Task AuthAndRateLimit_EnforcesJwtAndBlocksBruteForce()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiAuthTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = CreateTestFactory(tempVault);
            using var client = factory.CreateClient();

            // 1. Unauthenticated requests to /api/vault/** and /api/agent/** return 401
            var unauthVault = await client.GetAsync("/api/vault/overview");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthVault.StatusCode);

            var unauthAgent = await client.GetAsync("/api/agent/providers");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthAgent.StatusCode);

            // 2. Valid login attempt (1st request in window) returns 200 + JWT
            var validLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            Assert.Equal(HttpStatusCode.OK, validLogin.StatusCode);
            var loginPayload = await validLogin.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginPayload);
            Assert.False(string.IsNullOrWhiteSpace(loginPayload.Token));

            // 3. Invalid login attempts (attempts 2..5 in window) return 401 with invalid_credentials
            for (var attempt = 2; attempt <= 5; attempt++)
            {
                var badLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "WrongPass"));
                Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
                var body = await badLogin.Content.ReadAsStringAsync();
                Assert.Contains("invalid_credentials", body);
            }

            // 4. 6th attempt in the same window triggers HTTP 429 rate_limit_exceeded
            var rateLimited = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "WrongPass"));
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
            var rateLimitBody = await rateLimited.Content.ReadAsStringAsync();
            Assert.Contains("rate_limit_exceeded", rateLimitBody);
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
    public async Task AgentChat_ReturnsGenerativeUiBlocksAndExecutesVaultActions()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiAgentTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempVault, "templates"));
            Directory.CreateDirectory(Path.Combine(tempVault, "assets", "kanban"));
            await File.WriteAllTextAsync(
                Path.Combine(tempVault, "assets", "kanban", "Profissional.md"),
                "---\nkanban-plugin: board\n---\n\n## To Do\n- [ ] Tarefa 1\n\n## Doing\n\n## Done\n",
                Encoding.UTF8);

            var fakeCli = new FakeCliProcessRunner
            {
                Installed = true,
                StdOutToReturn = "Texto livre retornado pela CLI fora de JSON (teste de fallback tolerante)"
            };

            using var factory = CreateTestFactory(tempVault, fakeCli);
            using var client = factory.CreateClient();

            var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            var token = (await loginRes.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Check /api/agent/providers lists codex, claude, gemini along with configured models
            var providers = await client.GetFromJsonAsync<List<AiProviderStatusDto>>("/api/agent/providers");
            Assert.NotNull(providers);
            Assert.Equal(3, providers.Count);

            var codex = providers.First(p => p.Id == "codex");
            var claude = providers.First(p => p.Id == "claude");
            var gemini = providers.First(p => p.Id == "gemini");

            Assert.Equal("gpt-6-luna-medium", codex.Model);
            Assert.Equal("claude-haiku-5-5-medium", claude.Model);
            Assert.Equal("gemini-3.8-flash-medium", gemini.Model);

            // 2. Request full dashboard + form -> returns all 5 Generative UI block types
            var chatRes = await client.PostAsJsonAsync(
                "/api/agent/chat",
                new AgentChatRequest("Me mostre o dashboard de hoje e um formulário para cadastrar reunião", "codex"));
            Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

            var envelope = await chatRes.Content.ReadFromJsonAsync<AgentResponseEnvelope>();
            Assert.NotNull(envelope);
            Assert.Contains(envelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.KpiGrid);
            Assert.Contains(envelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.HabitChecklist);
            Assert.Contains(envelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.KanbanBoard);
            Assert.Contains(envelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.DynamicForm);
            Assert.Contains(envelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.MarkdownNoteList);

            // 3. CLI execution with non-JSON stdout falls back gracefully without crashing
            var cliChatRes = await client.PostAsJsonAsync(
                "/api/agent/chat",
                new AgentChatRequest("cli: Resuma meu dia e mostre métricas", "claude"));
            Assert.Equal(HttpStatusCode.OK, cliChatRes.StatusCode);
            var cliEnvelope = await cliChatRes.Content.ReadFromJsonAsync<AgentResponseEnvelope>();
            Assert.NotNull(cliEnvelope);
            Assert.True(cliEnvelope.ExecutedViaCli);
            Assert.Contains("Texto livre retornado pela CLI", cliEnvelope.ReplyText);
            Assert.NotEmpty(cliEnvelope.UiBlocks);
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
    public async Task VaultEndpoints_WhenPathViolationOrMissingEntity_ReturnsExpectedErrorCodes()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiErrorsTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = CreateTestFactory(tempVault);
            using var client = factory.CreateClient();

            var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            var token = (await loginRes.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Path traversal in category query -> 400 vault_path_violation
            var pathViolationRes = await client.GetAsync("/api/vault/notes?category=../../Windows");
            Assert.Equal(HttpStatusCode.BadRequest, pathViolationRes.StatusCode);
            Assert.Contains("vault_path_violation", await pathViolationRes.Content.ReadAsStringAsync());

            // 2. Non-existent Kanban board -> 404 kanban_board_not_found
            var missingKanbanRes = await client.GetAsync("/api/vault/kanban/Inexistente");
            Assert.Equal(HttpStatusCode.NotFound, missingKanbanRes.StatusCode);
            Assert.Contains("kanban_board_not_found", await missingKanbanRes.Content.ReadAsStringAsync());

            // 3. Non-existent habit -> 404 habit_not_found
            var missingHabitRes = await client.PostAsJsonAsync("/api/vault/habits/toggle", new HabitToggleRequest("Habito Que Nao Existe"));
            Assert.Equal(HttpStatusCode.NotFound, missingHabitRes.StatusCode);
            Assert.Contains("habit_not_found", await missingHabitRes.Content.ReadAsStringAsync());
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
    public async Task IdeasEndpoints_ExecuteFullPipeline_AndReturnExpectedErrorCodes()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiIdeasTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var fakeCli = new FakeCliProcessRunner
            {
                Installed = true,
                StdOutToReturn = "Pergunta socrática gerada pela CLI"
            };

            using var factory = CreateTestFactory(tempVault, fakeCli);
            using var client = factory.CreateClient();

            // 1. Unauthenticated access returns 401
            var unauthOverview = await client.GetAsync("/api/vault/ideas/overview");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthOverview.StatusCode);

            var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            var token = (await loginRes.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 2. Empty text -> 400 idea_text_required
            var emptyRes = await client.PostAsJsonAsync("/api/vault/ideas/fleeting", new FleetingIdeaCaptureRequest("   "));
            Assert.Equal(HttpStatusCode.BadRequest, emptyRes.StatusCode);
            Assert.Contains("idea_text_required", await emptyRes.Content.ReadAsStringAsync());

            // 3. Path traversal in target -> 400 vault_path_violation
            var traversalRes = await client.PostAsJsonAsync("/api/vault/ideas/fleeting", new FleetingIdeaCaptureRequest("Ideia válida", "../../evil"));
            Assert.Equal(HttpStatusCode.BadRequest, traversalRes.StatusCode);
            Assert.Contains("vault_path_violation", await traversalRes.Content.ReadAsStringAsync());

            // 4. Valid capture -> 200 IdeaItemDto
            var captureRes = await client.PostAsJsonAsync("/api/vault/ideas/fleeting", new FleetingIdeaCaptureRequest("Motor de brainstorming local"));
            Assert.Equal(HttpStatusCode.OK, captureRes.StatusCode);
            var captured = await captureRes.Content.ReadFromJsonAsync<IdeaItemDto>();
            Assert.NotNull(captured);
            Assert.Equal("fleeting", captured.Status);

            // 5. Brainstorm missing idea -> 404 idea_not_found
            var missingBrainstorm = await client.PostAsJsonAsync("/api/vault/ideas/brainstorm", new BrainstormStartRequest(IdeaId: "nao-existe"));
            Assert.Equal(HttpStatusCode.NotFound, missingBrainstorm.StatusCode);
            Assert.Contains("idea_not_found", await missingBrainstorm.Content.ReadAsStringAsync());

            // 6. Start brainstorm -> 200 BrainstormSessionDto
            var brainstormRes = await client.PostAsJsonAsync("/api/vault/ideas/brainstorm", new BrainstormStartRequest(IdeaId: captured.Id));
            Assert.Equal(HttpStatusCode.OK, brainstormRes.StatusCode);
            var session = await brainstormRes.Content.ReadFromJsonAsync<BrainstormSessionDto>();
            Assert.NotNull(session);
            Assert.False(string.IsNullOrWhiteSpace(session.Problem));
            Assert.NotEmpty(session.Hypotheses);
            Assert.NotEmpty(session.Risks);
            Assert.False(string.IsNullOrWhiteSpace(session.SocraticQuestion));

            // 7. Promote invalid target -> 400 invalid_promotion_target
            var invalidPromote = await client.PostAsJsonAsync("/api/vault/ideas/promote", new IdeaPromoteRequest(captured.Id, "invalido"));
            Assert.Equal(HttpStatusCode.BadRequest, invalidPromote.StatusCode);
            Assert.Contains("invalid_promotion_target", await invalidPromote.Content.ReadAsStringAsync());

            // 8. Promote missing idea -> 404 idea_not_found
            var missingPromote = await client.PostAsJsonAsync("/api/vault/ideas/promote", new IdeaPromoteRequest("nao-existe", "permanente"));
            Assert.Equal(HttpStatusCode.NotFound, missingPromote.StatusCode);
            Assert.Contains("idea_not_found", await missingPromote.Content.ReadAsStringAsync());

            // 9. Promote to permanente -> 200 IdeaPromotionResultDto
            var promoteRes = await client.PostAsJsonAsync("/api/vault/ideas/promote", new IdeaPromoteRequest(captured.Id, "permanente"));
            Assert.Equal(HttpStatusCode.OK, promoteRes.StatusCode);
            var promoted = await promoteRes.Content.ReadFromJsonAsync<IdeaPromotionResultDto>();
            Assert.NotNull(promoted);
            Assert.Equal("promovida", promoted.Status);

            // 10. Overview reflects counts
            var overviewRes = await client.GetAsync("/api/vault/ideas/overview");
            Assert.Equal(HttpStatusCode.OK, overviewRes.StatusCode);
            var overview = await overviewRes.Content.ReadFromJsonAsync<IdeasOverviewDto>();
            Assert.NotNull(overview);
            Assert.Equal(1, overview.PromotedCount);

            // 11. Agent chat "ideia: ..." captures to Fleeting.md and returns idea-capture + brainstorm-canvas
            var agentIdeaRes = await client.PostAsJsonAsync("/api/agent/chat", new AgentChatRequest("ideia: Capturar pensamento via chat", "codex"));
            Assert.Equal(HttpStatusCode.OK, agentIdeaRes.StatusCode);
            var agentEnvelope = await agentIdeaRes.Content.ReadFromJsonAsync<AgentResponseEnvelope>();
            Assert.NotNull(agentEnvelope);
            Assert.Contains(agentEnvelope.TouchedFiles, f => f.Contains("Fleeting.md", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(agentEnvelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.IdeaCapture && b.IdeasOverview is not null);
            Assert.Contains(agentEnvelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.BrainstormCanvas && b.IdeasOverview is not null);

            // 12. Agent chat with CLI + brainstorm prompt passes socratic instructions
            var cliBrainstormRes = await client.PostAsJsonAsync("/api/agent/chat", new AgentChatRequest("cli: brainstorm sobre captura de ideias", "claude"));
            Assert.Equal(HttpStatusCode.OK, cliBrainstormRes.StatusCode);
            Assert.NotNull(fakeCli.LastArguments);
            Assert.Contains(fakeCli.LastArguments, a => a.Contains("/brainstorming", StringComparison.OrdinalIgnoreCase));
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
    public async Task CalendarEndpoints_RequireJwt_ExecutePipeline_HandleErrors_AndEmitAgendaTimeline()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiCalendarTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = CreateTestFactory(tempVault);
            using var client = factory.CreateClient();

            // 1. Unauthenticated requests return 401 Unauthorized (JWT Bearer)
            var unauthToday = await client.GetAsync("/api/calendar/today");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthToday.StatusCode);

            var unauthSync = await client.PostAsJsonAsync("/api/calendar/sync", new CalendarSyncRequest("2026-10-30"));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthSync.StatusCode);

            var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            var token = (await loginRes.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 2. GET /api/calendar/today -> 200 OK with 5 deterministic fallback events
            var todayRes = await client.GetAsync("/api/calendar/today?date=2026-10-30");
            Assert.Equal(HttpStatusCode.OK, todayRes.StatusCode);
            var today = await todayRes.Content.ReadFromJsonAsync<CalendarTodayDto>();
            Assert.NotNull(today);
            Assert.Equal("2026-10-30", today.Date);
            Assert.Equal(5, today.Events.Count);
            Assert.Equal(1, today.ConflictsCount);

            // 3. POST /api/calendar/sync -> 200 OK and writes daily/2026-10-30.md
            var syncRes = await client.PostAsJsonAsync("/api/calendar/sync", new CalendarSyncRequest("2026-10-30"));
            Assert.Equal(HttpStatusCode.OK, syncRes.StatusCode);
            var syncPayload = await syncRes.Content.ReadFromJsonAsync<CalendarSyncResultDto>();
            Assert.NotNull(syncPayload);
            Assert.Equal("daily/2026-10-30.md", syncPayload.DailyNotePath);
            Assert.True(File.Exists(Path.Combine(tempVault, "daily", "2026-10-30.md")));

            // 4. POST /api/calendar/events/{id}/note -> 200 OK and creates meeting note
            var targetEvent = today.Events.First(e => e.Title.Contains("Alinhamento GranBrain"));
            var noteRes = await client.PostAsJsonAsync($"/api/calendar/events/{targetEvent.Id}/note", new CalendarNoteRequest(Date: "2026-10-30"));
            Assert.Equal(HttpStatusCode.OK, noteRes.StatusCode);
            var notePayload = await noteRes.Content.ReadFromJsonAsync<CalendarNoteResultDto>();
            Assert.NotNull(notePayload);
            Assert.True(notePayload.Created);
            Assert.Equal("zettelkasten/Reunião - Alinhamento GranBrain.md", notePayload.NotePath);

            // 5. Negative error codes: 400 invalid_calendar_date, 400 vault_path_violation, 404 calendar_event_not_found
            var invalidDateRes = await client.GetAsync("/api/calendar/today?date=30-10-2026");
            Assert.Equal(HttpStatusCode.BadRequest, invalidDateRes.StatusCode);
            Assert.Contains("invalid_calendar_date", await invalidDateRes.Content.ReadAsStringAsync());

            var traversalRes = await client.PostAsJsonAsync("/api/calendar/sync", new CalendarSyncRequest("2026-10-30", "../../evil"));
            Assert.Equal(HttpStatusCode.BadRequest, traversalRes.StatusCode);
            Assert.Contains("vault_path_violation", await traversalRes.Content.ReadAsStringAsync());

            var missingNoteRes = await client.PostAsJsonAsync("/api/calendar/events/inexistente/note", new CalendarNoteRequest(Date: "2026-10-30"));
            Assert.Equal(HttpStatusCode.NotFound, missingNoteRes.StatusCode);
            Assert.Contains("calendar_event_not_found", await missingNoteRes.Content.ReadAsStringAsync());

            var badJsonRes = await client.PostAsync(
                "/api/calendar/sync",
                new StringContent("{ invalid json", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, badJsonRes.StatusCode);
            Assert.Contains("invalid_request_body", await badJsonRes.Content.ReadAsStringAsync());

            // 6. AgentOrchestratorService emits agenda-timeline block for agenda intent and syncs on sync prompt
            var agentAgendaRes = await client.PostAsJsonAsync("/api/agent/chat", new AgentChatRequest("Quais reuniões eu tenho na agenda hoje?", "codex"));
            Assert.Equal(HttpStatusCode.OK, agentAgendaRes.StatusCode);
            var agentEnvelope = await agentAgendaRes.Content.ReadFromJsonAsync<AgentResponseEnvelope>();
            Assert.NotNull(agentEnvelope);
            var agendaBlock = Assert.Single(agentEnvelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.AgendaTimeline);
            Assert.NotNull(agendaBlock.CalendarAgenda);
            Assert.Equal(5, agendaBlock.CalendarAgenda.Events.Count);

            var agentSyncRes = await client.PostAsJsonAsync("/api/agent/chat", new AgentChatRequest("sincronizar agenda de hoje", "codex"));
            Assert.Equal(HttpStatusCode.OK, agentSyncRes.StatusCode);
            var syncEnvelope = await agentSyncRes.Content.ReadFromJsonAsync<AgentResponseEnvelope>();
            Assert.NotNull(syncEnvelope);
            Assert.Contains(syncEnvelope.TouchedFiles, f => f.StartsWith("daily/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(syncEnvelope.UiBlocks, b => b.Type == GenerativeUiBlockTypes.AgendaTimeline);

            // 7. Partial calendar credentials -> 400 calendar_auth_failed
            using var partialFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("GranBrain:VaultPath", tempVault);
                builder.UseSetting("GranBrain:Calendar:Google:ClientId", "partial-client-only");
            });
            using var partialClient = partialFactory.CreateClient();
            var partialLogin = await partialClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            var partialToken = (await partialLogin.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            partialClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", partialToken);

            var authFailedRes = await partialClient.GetAsync("/api/calendar/today?date=2026-10-30");
            Assert.Equal(HttpStatusCode.BadRequest, authFailedRes.StatusCode);
            Assert.Contains("calendar_auth_failed", await authFailedRes.Content.ReadAsStringAsync());
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
    public async Task ForwardedHeadersAndCors_EnforcesClientIpRateLimitAndOriginPolicy()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainApiForwardedCorsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GranBrain:VaultPath"] = tempVault,
                        ["GranBrain:Auth:Username"] = "igor",
                        ["GranBrain:Auth:DefaultPassword"] = "GranBrain@2026",
                        ["GranBrain:Auth:MaxLoginAttemptsPerWindow"] = "5",
                        ["GranBrain:Auth:LoginWindowSeconds"] = "60",
                        ["GranBrain:AllowedOrigins:0"] = "https://app.granbrain.com.br/"
                    });
                });
            });

            using var client = factory.CreateClient();

            // 1. 5 invalid login attempts for CF-Connecting-IP 198.51.100.77 behind proxy X-Forwarded-For 172.18.0.2 -> 401
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
                {
                    Content = JsonContent.Create(new LoginRequest("igor", "WrongPass"))
                };
                req.Headers.TryAddWithoutValidation("CF-Connecting-IP", "198.51.100.77");
                req.Headers.TryAddWithoutValidation("X-Forwarded-For", "172.18.0.2");
                req.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
            }

            // 2. 6th attempt from the same CF-Connecting-IP -> 429 rate_limit_exceeded
            using (var rateLimitedReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("igor", "WrongPass"))
            })
            {
                rateLimitedReq.Headers.TryAddWithoutValidation("CF-Connecting-IP", "198.51.100.77");
                rateLimitedReq.Headers.TryAddWithoutValidation("X-Forwarded-For", "172.18.0.2");
                var rateLimitedRes = await client.SendAsync(rateLimitedReq);
                Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedRes.StatusCode);
                Assert.Contains("rate_limit_exceeded", await rateLimitedRes.Content.ReadAsStringAsync());
            }

            // 3. Another client sharing the same X-Forwarded-For proxy IP (172.18.0.2) but different CF-Connecting-IP gets an independent bucket -> 200 OK
            using (var secondCfClientReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("igor", "GranBrain@2026"))
            })
            {
                secondCfClientReq.Headers.TryAddWithoutValidation("CF-Connecting-IP", "198.51.100.88");
                secondCfClientReq.Headers.TryAddWithoutValidation("X-Forwarded-For", "172.18.0.2");
                var secondCfClientRes = await client.SendAsync(secondCfClientReq);
                Assert.Equal(HttpStatusCode.OK, secondCfClientRes.StatusCode);
            }

            // 4. Different client IP via X-Forwarded-For without CF-Connecting-IP also gets an independent bucket -> 200 OK
            using (var otherIpReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("igor", "GranBrain@2026"))
            })
            {
                otherIpReq.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.5, 172.18.0.2");
                var otherIpRes = await client.SendAsync(otherIpReq);
                Assert.Equal(HttpStatusCode.OK, otherIpRes.StatusCode);
            }

            // 4. CORS preflight OPTIONS for normalized AllowedOrigins -> allows https://app.granbrain.com.br
            using (var allowedCorsReq = new HttpRequestMessage(HttpMethod.Options, "/api/agent/chat"))
            {
                allowedCorsReq.Headers.TryAddWithoutValidation("Origin", "https://app.granbrain.com.br");
                allowedCorsReq.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
                allowedCorsReq.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");

                var allowedCorsRes = await client.SendAsync(allowedCorsReq);
                Assert.True(allowedCorsRes.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowValues));
                Assert.Contains("https://app.granbrain.com.br", allowValues);
            }

            // 5. CORS preflight OPTIONS from unauthorized origin -> omits Access-Control-Allow-Origin
            using (var evilCorsReq = new HttpRequestMessage(HttpMethod.Options, "/api/agent/chat"))
            {
                evilCorsReq.Headers.TryAddWithoutValidation("Origin", "https://evil.example.com");
                evilCorsReq.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
                evilCorsReq.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");

                var evilCorsRes = await client.SendAsync(evilCorsReq);
                Assert.False(evilCorsRes.Headers.Contains("Access-Control-Allow-Origin"));
            }
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
    public async Task HealthEndpoint_ReadsRcloneSyncStatusFileAndHandlesMissingOrCorruptFile()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainHealthVault_" + Guid.NewGuid().ToString("N"));
        var tempSyncDir = Path.Combine(Path.GetTempPath(), "GranBrainHealthSync_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);
        Directory.CreateDirectory(tempSyncDir);
        var syncStatusPath = Path.Combine(tempSyncDir, "sync-status.json");

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GranBrain:VaultPath"] = tempVault,
                        ["GranBrain:SyncStatusFilePath"] = syncStatusPath,
                        ["GranBrain:TunnelMode"] = "cloudflare-tunnel"
                    });
                });
            });

            using var client = factory.CreateClient();

            // 1. Missing sync-status.json -> 200 OK with tunnelStatus=ok and syncStatus=pending
            var pendingRes = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, pendingRes.StatusCode);
            var pendingDto = await pendingRes.Content.ReadFromJsonAsync<SystemHealthStatusDto>();
            Assert.NotNull(pendingDto);
            Assert.Equal("ok", pendingDto.Status);
            Assert.Equal("GranBrain.Api", pendingDto.Service);
            Assert.True(pendingDto.VaultAccessible);
            Assert.Equal("ok", pendingDto.TunnelStatus);
            Assert.Equal("pending", pendingDto.SyncStatus);
            Assert.Null(pendingDto.LastSyncedAtUtc);

            // 2. Valid rclone sync-status.json -> reflects ok and lastSyncedAtUtc
            await File.WriteAllTextAsync(
                syncStatusPath,
                """{"status":"ok","lastSyncedAtUtc":"2026-11-13T18:00:00Z"}""",
                Encoding.UTF8);

            var okRes = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
            var okDto = await okRes.Content.ReadFromJsonAsync<SystemHealthStatusDto>();
            Assert.NotNull(okDto);
            Assert.Equal("ok", okDto.Status);
            Assert.Equal("ok", okDto.SyncStatus);
            Assert.Equal("2026-11-13T18:00:00Z", okDto.LastSyncedAtUtc);

            // 3. Corrupt/truncated JSON -> 200 OK degraded with syncStatus=error (no 500)
            await File.WriteAllTextAsync(syncStatusPath, "{corrupt", Encoding.UTF8);

            var corruptRes = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, corruptRes.StatusCode);
            var corruptDto = await corruptRes.Content.ReadFromJsonAsync<SystemHealthStatusDto>();
            Assert.NotNull(corruptDto);
            Assert.Equal("degraded", corruptDto.Status);
            Assert.Equal("error", corruptDto.SyncStatus);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
            if (Directory.Exists(tempSyncDir))
            {
                Directory.Delete(tempSyncDir, true);
            }
        }
    }
}
