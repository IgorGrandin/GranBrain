using GranBrain.Core.Security;

namespace GranBrain.UnitTests;

public sealed class VaultPathGuardTests
{
    [Fact]
    public void ResolveSafePath_WhenPathEscapesVaultRoot_ThrowsVaultPathViolationException()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GranBrainVaultTest_" + Guid.NewGuid().ToString("N"));
        var guard = new VaultPathGuard(tempRoot);

        // 1. Relative traversal escaping root
        var ex1 = Assert.Throws<VaultPathViolationException>(() => guard.ResolveSafePath("../../Windows/win.ini"));
        Assert.Equal("vault_path_violation", ex1.ErrorCode);

        // 2. External absolute path
        var ex2 = Assert.Throws<VaultPathViolationException>(() => guard.ResolveSafePath(@"C:\Windows\win.ini"));
        Assert.Equal("vault_path_violation", ex2.ErrorCode);

        // 3. Sibling prefix directory attack (e.g., VaultRoot_leak\secret.md)
        var siblingAttack = tempRoot + "_leak" + Path.DirectorySeparatorChar + "secret.md";
        var ex3 = Assert.Throws<VaultPathViolationException>(() => guard.ResolveSafePath(siblingAttack));
        Assert.Equal("vault_path_violation", ex3.ErrorCode);

        // 4. Valid path inside vault resolves safely
        var valid = guard.ResolveSafePath("daily/2026-10-29.md");
        Assert.StartsWith(tempRoot, valid, StringComparison.OrdinalIgnoreCase);
    }
}
