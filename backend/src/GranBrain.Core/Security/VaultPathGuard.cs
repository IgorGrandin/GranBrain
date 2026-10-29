namespace GranBrain.Core.Security;

public sealed class VaultPathViolationException : Exception
{
    public string ErrorCode => "vault_path_violation";
    public string AttemptedPath { get; }

    public VaultPathViolationException(string attemptedPath, string message)
        : base(message)
    {
        AttemptedPath = attemptedPath;
    }
}

public sealed class VaultEntityNotFoundException : Exception
{
    public string ErrorCode { get; }

    public VaultEntityNotFoundException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public sealed class VaultPathGuard
{
    private readonly string _normalizedRoot;
    private readonly string _normalizedRootWithSeparator;

    public string VaultRoot => _normalizedRoot;

    public VaultPathGuard(string vaultRoot)
    {
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            throw new ArgumentException("VaultRoot cannot be empty.", nameof(vaultRoot));
        }

        _normalizedRoot = Path.GetFullPath(vaultRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _normalizedRootWithSeparator = _normalizedRoot + Path.DirectorySeparatorChar;
    }

    public string ResolveSafePath(string relativeOrSubPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrSubPath))
        {
            return _normalizedRoot;
        }

        string candidate;
        try
        {
            if (Path.IsPathRooted(relativeOrSubPath))
            {
                candidate = Path.GetFullPath(relativeOrSubPath);
            }
            else
            {
                candidate = Path.GetFullPath(Path.Combine(_normalizedRoot, relativeOrSubPath));
            }
        }
        catch (Exception ex)
        {
            throw new VaultPathViolationException(relativeOrSubPath, $"Invalid path format: {ex.Message}");
        }

        var trimmedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(trimmedCandidate, _normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _normalizedRoot;
        }

        if (!candidate.StartsWith(_normalizedRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new VaultPathViolationException(
                relativeOrSubPath,
                $"Access denied: '{relativeOrSubPath}' resolves outside the configured Obsidian Vault root.");
        }

        return candidate;
    }
}
