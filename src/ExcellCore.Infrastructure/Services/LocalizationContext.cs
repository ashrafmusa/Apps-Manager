using System;
using System.Collections.Generic;
using System.Linq;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Infrastructure.Services;

public sealed class LocalizationContext : ILocalizationContext
{
    private IReadOnlyList<string> _contexts = Array.Empty<string>();

    public IReadOnlyList<string> Contexts => _contexts;

    public event EventHandler? ContextsChanged;

    public void SetContexts(IEnumerable<string> contexts)
    {
        if (contexts is null)
        {
            _contexts = Array.Empty<string>();
        }
        else
        {
            var list = contexts.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            _contexts = list.Length == 0 ? Array.Empty<string>() : list;
        }

        ContextsChanged?.Invoke(this, EventArgs.Empty);
    }
}
