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

        public bool IsExecutableAvailable(string executableOrCommand) => Installed;

        public Task<CliProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            int timeoutSeconds,
            CancellationToken ct = default)
        {
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

            // 1. Check /api/agent/providers lists codex, claude, gemini
            var providers = await client.GetFromJsonAsync<List<AiProviderStatusDto>>("/api/agent/providers");
            Assert.NotNull(providers);
            Assert.Equal(3, providers.Count);
            Assert.Contains(providers, p => p.Id == "codex");
            Assert.Contains(providers, p => p.Id == "claude");
            Assert.Contains(providers, p => p.Id == "gemini");

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
}
