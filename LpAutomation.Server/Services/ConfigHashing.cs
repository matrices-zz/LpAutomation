using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LpAutomation.Core.Models;
using LpAutomation.Core.Serialization;

namespace LpAutomation.Server.Services;

public static class ConfigHashing
{
    public static string Sha256Hash(StrategyConfigDocument doc)
    {
        var json = JsonSerializer.Serialize(doc, JsonStrict.Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
