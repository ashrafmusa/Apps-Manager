using System.Collections.Concurrent;
using System.Linq;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Infrastructure.Modules;

public sealed class ModuleCatalog : IModuleCatalog
{
    private readonly ConcurrentDictionary<string, IModuleDescriptor> _modules = new();

    public IReadOnlyCollection<IModuleDescriptor> Modules => _modules.Values.ToList();

    public void RegisterModule(IModuleDescriptor descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        _modules.AddOrUpdate(descriptor.ModuleId, descriptor, (_, _) => descriptor);
    }
}
