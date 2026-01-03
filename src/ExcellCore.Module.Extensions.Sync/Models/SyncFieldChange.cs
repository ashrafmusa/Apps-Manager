using System;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed class SyncFieldChange
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SyncFieldChange(string fieldName, object? newValue, object? previousValue)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name is required.", nameof(fieldName));
        }

        FieldName = fieldName;
        NewValue = newValue;
        PreviousValue = previousValue;
    }

    public string FieldName { get; }
    public object? NewValue { get; }
    public object? PreviousValue { get; }
}
