using System.IO;
using System.Text.Json;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Infrastructure.Modules;

public sealed class FileSystemModuleManifestProvider : IModuleManifestProvider
{
    private readonly string _manifestRoot;

    public FileSystemModuleManifestProvider()
    {
        _manifestRoot = Path.Combine(AppContext.BaseDirectory, "modules");
    }

    public IEnumerable<ModuleManifest> DiscoverManifests()
    {
        if (!Directory.Exists(_manifestRoot))
        {
            yield break;
        }

        foreach (var manifestFile in Directory.EnumerateFiles(_manifestRoot, "manifest.json", SearchOption.AllDirectories))
        {
            foreach (var manifest in ReadManifest(manifestFile))
            {
                yield return manifest;
            }
        }
    }

    private static IEnumerable<ModuleManifest> ReadManifest(string path)
    {
        using var stream = File.OpenRead(path);
        var manifest = JsonSerializer.Deserialize<ManifestFile>(stream);
        if (manifest is null)
        {
            yield break;
        }

        var assemblyPath = manifest.AssemblyPath;
        if (!string.IsNullOrWhiteSpace(assemblyPath) && !Path.IsPathRooted(assemblyPath))
        {
            var manifestDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            assemblyPath = Path.GetFullPath(Path.Combine(manifestDirectory, assemblyPath));
        }

        yield return new ModuleManifest(
            manifest.ModuleId,
            manifest.DisplayName,
            assemblyPath,
            manifest.Enabled);
    }

    private sealed record ManifestFile(string ModuleId, string DisplayName, string AssemblyPath, bool Enabled);
}
