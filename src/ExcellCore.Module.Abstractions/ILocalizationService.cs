using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Module.Abstractions;

public interface ILocalizationService
{
    string GetString(string key, IEnumerable<string> contexts);
    IReadOnlyDictionary<string, string> GetStrings(IEnumerable<string> keys, IEnumerable<string> contexts);
}

public interface ILocalizationContext
{
    IReadOnlyList<string> Contexts { get; }
    event EventHandler? ContextsChanged;
    void SetContexts(IEnumerable<string> contexts);
}

public interface IMetadataFormService
{
    Task<IReadOnlyList<MetadataFieldValue>> GetFieldsAsync(string context, Guid? aggregateId, CancellationToken cancellationToken = default);
    Task SaveFieldsAsync(string context, Guid aggregateId, IEnumerable<MetadataFieldValue> fields, CancellationToken cancellationToken = default);
    IReadOnlyList<MetadataFieldDefinition> GetDefinitions(string context);
}

public sealed class MetadataFieldDefinition
{
    public MetadataFieldDefinition(string key, string label, string dataType = "string")
    {
        Key = key;
        Label = label;
        DataType = dataType;
    }

    public string Key { get; }
    public string Label { get; }
    public string DataType { get; }
}

public sealed class MetadataFieldValue
{
    public MetadataFieldValue(string key, string label, string? value, string dataType = "string")
    {
        Key = key;
        Label = label;
        Value = value;
        DataType = dataType;
    }

    public string Key { get; }
    public string Label { get; }
    public string? Value { get; }
    public string DataType { get; }
}
