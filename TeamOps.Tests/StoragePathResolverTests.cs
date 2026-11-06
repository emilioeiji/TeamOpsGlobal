using TeamOps.Config;
using Xunit;

namespace TeamOps.Tests;

public sealed class StoragePathResolverTests
{
    [Fact]
    public void ResolveCombinesRelativePathWithStorageRoot()
    {
        var result = StoragePathResolver.Resolve(
            @"Anexo\Hikitsugui",
            @"C:\TeamOps",
            @"C:\TeamOps\App");

        Assert.Equal(@"C:\TeamOps\Anexo\Hikitsugui", result);
    }

    [Fact]
    public void ResolvePreservesAbsoluteUncPath()
    {
        var result = StoragePathResolver.Resolve(
            @"\\servidor\compartilhamento\TeamOps\Haidai",
            @"C:\TeamOps",
            @"C:\TeamOps\App");

        Assert.Equal(@"\\servidor\compartilhamento\TeamOps\Haidai", result);
        Assert.True(StoragePathResolver.IsUncPath(result));
    }

    [Fact]
    public void ResolvePreservesAbsoluteLocalPath()
    {
        var result = StoragePathResolver.Resolve(
            @"D:\TeamOps\PR",
            @"C:\TeamOps",
            @"C:\TeamOps\App");

        Assert.Equal(@"D:\TeamOps\PR", result);
        Assert.False(StoragePathResolver.IsUncPath(result));
    }

    [Fact]
    public void ResolveRejectsDriveRelativePath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            StoragePathResolver.Resolve(
                @"H:TeamOps\PR",
                @"C:\TeamOps",
                @"C:\TeamOps\App"));

        Assert.Contains("Use caminho absoluto local ou UNC", exception.Message);
    }

    [Fact]
    public void ResolveCombinesRelativePathWithUncStorageRoot()
    {
        var result = StoragePathResolver.Resolve(
            @"Anexo\Hikitsugui",
            @"\\servidor\compartilhamento\TeamOps",
            @"C:\TeamOps\App");

        Assert.Equal(@"\\servidor\compartilhamento\TeamOps\Anexo\Hikitsugui", result);
        Assert.True(StoragePathResolver.IsUncPath(result));
    }

    [Fact]
    public void ResolveRejectsRelativePathOutsideStorageRoot()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            StoragePathResolver.Resolve(
                @"..\DB\teamops.db",
                @"C:\TeamOps\Storage",
                @"C:\TeamOps\App"));

        Assert.Contains("sai da raiz de armazenamento", exception.Message);
    }

    [Fact]
    public void DatabaseSettingsRejectUncBecauseSqliteMustRemainLocal()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DbSettings(@"\\servidor\compartilhamento\TeamOps\DB\teamops.db"));

        Assert.Contains("SQLite deve permanecer em disco local", exception.Message);
    }

    [Fact]
    public void DatabaseSettingsAcceptLocalAbsolutePath()
    {
        var settings = new DbSettings(@"C:\TeamOps\DB\teamops.db");

        Assert.Equal(@"C:\TeamOps\DB\teamops.db", settings.DatabasePath);
        Assert.Contains("Pooling=True", settings.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }
}
