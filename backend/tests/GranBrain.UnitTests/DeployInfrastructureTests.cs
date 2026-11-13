using System.Text;

namespace GranBrain.UnitTests;

public sealed class DeployInfrastructureTests
{
    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing CLAUDE.md or .git was not found.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var fullPath = Path.Combine(ResolveRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Expected infrastructure artifact '{relativePath}' to exist at '{fullPath}'.");
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    private static string ExtractServiceBlock(string composeContent, string serviceName)
    {
        var lines = composeContent.Replace("\r\n", "\n").Split('\n');
        var header = $"  {serviceName}:";
        var start = Array.FindIndex(lines, l => string.Equals(l.TrimEnd(), header, StringComparison.Ordinal));
        Assert.True(start >= 0, $"Service '{serviceName}' was not found under services: in docker-compose.yml.");

        var sb = new StringBuilder();
        sb.AppendLine(lines[start]);
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("  #", StringComparison.Ordinal))
            {
                break;
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    [Fact]
    public void DockerAndComposeArtifacts_EnforceInternalNetworkMemoryLimitsCurlHealthcheckAndSharedVolumes()
    {
        var backendDockerfile = ReadRepoFile("backend/Dockerfile");
        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0", backendDockerfile);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", backendDockerfile);
        Assert.Contains("curl", backendDockerfile);
        Assert.Contains("HEALTHCHECK", backendDockerfile);
        Assert.Contains("http://127.0.0.1:8080/api/health", backendDockerfile);

        var frontendDockerfile = ReadRepoFile("frontend/Dockerfile");
        Assert.Contains("node:22-alpine", frontendDockerfile);
        Assert.Contains("npm ci", frontendDockerfile);
        Assert.Contains("npm run build", frontendDockerfile);
        Assert.Contains("nginx:1.27-alpine", frontendDockerfile);
        Assert.Contains("dist/granbrain-frontend", frontendDockerfile);
        Assert.Contains("/docker-entrypoint.d/40-generate-runtime-config.sh", frontendDockerfile);

        _ = ReadRepoFile(".dockerignore");

        var compose = ReadRepoFile("docker-compose.yml");
        Assert.Contains("granbrain-net:", compose);
        Assert.Contains("vault-data:", compose);
        Assert.Contains("sync-state:", compose);

        foreach (var service in new[] { "api", "frontend", "cloudflared", "vault-sync" })
        {
            var block = ExtractServiceBlock(compose, service);
            Assert.Contains("mem_limit:", block);
            Assert.Contains("granbrain-net", block);
        }

        var apiBlock = ExtractServiceBlock(compose, "api");
        Assert.DoesNotContain("ports:", apiBlock);
        Assert.Contains("expose:", apiBlock);
        Assert.Contains("8080", apiBlock);
        Assert.Contains("vault-data:/data/vault", apiBlock);
        Assert.Contains("sync-state:/var/run/granbrain", apiBlock);

        var frontendBlock = ExtractServiceBlock(compose, "frontend");
        Assert.DoesNotContain("ports:", frontendBlock);
        Assert.Contains("expose:", frontendBlock);
        Assert.Contains("8080", frontendBlock);

        var vaultSyncBlock = ExtractServiceBlock(compose, "vault-sync");
        Assert.Contains("vault-data:/data/vault", vaultSyncBlock);
        Assert.Contains("sync-state:/var/run/granbrain", vaultSyncBlock);

        var envExample = ReadRepoFile(".env.example");
        Assert.Contains("GRANBRAIN_JWT_SECRET=", envExample);
        Assert.Contains("GRANBRAIN_PASSWORD_HASH=", envExample);
        Assert.Contains("GRANBRAIN_ALLOWED_ORIGINS=", envExample);
        Assert.Contains("GRANBRAIN_API_BASE_URL=", envExample);
        Assert.Contains("CLOUDFLARE_TUNNEL_TOKEN=", envExample);
        Assert.DoesNotContain("GranBrain@2026", envExample);
        Assert.DoesNotContain("GranBrain-Super-Secret-Key-2026", envExample);

        var setupVps = ReadRepoFile("deploy/scripts/setup-vps.sh");
        Assert.Contains("ufw", setupVps);
        Assert.Contains("docker compose", setupVps);
        Assert.Contains("GranBrain@2026", setupVps); // checked only as forbidden default guard
    }

    [Fact]
    public void CloudflareAndNginxConfigs_EnforceDualHostnameAndNoStoreRuntimeConfig()
    {
        var cloudflareConfig = ReadRepoFile("deploy/cloudflare/config.yml.example");
        Assert.Contains("app.", cloudflareConfig);
        Assert.Contains("http://frontend:8080", cloudflareConfig);
        Assert.Contains("api.", cloudflareConfig);
        Assert.Contains("http://api:8080", cloudflareConfig);
        Assert.Contains("http_status:404", cloudflareConfig);

        var nginxConf = ReadRepoFile("deploy/nginx/default.conf");
        Assert.Contains("listen 8080;", nginxConf);
        Assert.Contains("/healthz", nginxConf);
        Assert.Contains("runtime-config.js", nginxConf);
        Assert.Contains("/index.html", nginxConf);
        Assert.Contains("no-store, no-cache, must-revalidate", nginxConf);

        var entrypointScript = ReadRepoFile("deploy/nginx/40-generate-runtime-config.sh");
        Assert.Contains("GRANBRAIN_API_BASE_URL", entrypointScript);
        Assert.Contains("/usr/share/nginx/html/assets/runtime-config.js", entrypointScript);
        Assert.Contains("window.__GRANBRAIN_CONFIG__", entrypointScript);
    }

    [Fact]
    public void RcloneBisyncScript_EnforcesLockFirstRunResyncResilienceAndAtomicStatusOutput()
    {
        var bisyncScript = ReadRepoFile("deploy/rclone/rclone-bisync-loop.sh");
        Assert.Contains("/var/run/granbrain/bisync.lock", bisyncScript);
        Assert.Contains("/var/run/granbrain/.bisync-initialized", bisyncScript);
        Assert.Contains("rclone bisync", bisyncScript);
        Assert.Contains("--resync", bisyncScript);
        Assert.Contains("--resilient", bisyncScript);
        Assert.Contains("--conflict-resolve newer", bisyncScript);
        Assert.Contains("--create-empty-src-dirs", bisyncScript);
        Assert.Contains(".granbrain-*", bisyncScript);
        Assert.Contains(".obsidian/workspace*", bisyncScript);
        Assert.Contains("sync-status.json.tmp", bisyncScript);
        Assert.Contains("mv -f", bisyncScript);
        Assert.Contains("/var/run/granbrain/sync-status.json", bisyncScript);
        Assert.DoesNotContain("rclone sync ", bisyncScript);
    }
}
