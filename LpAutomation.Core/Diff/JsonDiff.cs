using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using LpAutomation.Core.Serialization;

namespace LpAutomation.Core.Diff;

public sealed record DiffItem(string Path, string OldValue, string NewValue);

public static class JsonDiff
{
    public static List<DiffItem> Diff(object oldObj, object newObj)
    {
        var oldNode = JsonNode.Parse(JsonSerializer.Serialize(oldObj, JsonStrict.Options))!;
        var newNode = JsonNode.Parse(JsonSerializer.Serialize(newObj, JsonStrict.Options))!;
        var diffs = new List<DiffItem>();
        Walk("$", oldNode, newNode, diffs);
        return diffs;
    }

    private static void Walk(string path, JsonNode? a, JsonNode? b, List<DiffItem> diffs)
    {
        if (a is null && b is null) return;
        if (a is null || b is null)
        {
            diffs.Add(new(path, a?.ToJsonString() ?? "null", b?.ToJsonString() ?? "null"));
            return;
        }

        if (a is JsonValue && b is JsonValue)
        {
            var av = a.ToJsonString();
            var bv = b.ToJsonString();
            if (!string.Equals(av, bv, StringComparison.Ordinal))
                diffs.Add(new(path, av, bv));
            return;
        }

        if (a is JsonObject ao && b is JsonObject bo)
        {
            var keys = new HashSet<string>(ao.Select(k => k.Key));
            keys.UnionWith(bo.Select(k => k.Key));
            foreach (var k in keys.OrderBy(x => x))
                Walk($"{path}.{k}", ao[k], bo[k], diffs);
            return;
        }

        if (a is JsonArray || b is JsonArray)
        {
            // arrays treated as replace-on-change
            var av = a.ToJsonString();
            var bv = b.ToJsonString();
            if (!string.Equals(av, bv, StringComparison.Ordinal))
                diffs.Add(new(path, av, bv));
            return;
        }

        diffs.Add(new(path, a.ToJsonString(), b.ToJsonString()));
    }
}
