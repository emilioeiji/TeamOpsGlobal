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
}
