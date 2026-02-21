using System;
using System.Collections.Generic;
using LpAutomation.Core.Models;
using LpAutomation.Core.Validation;

namespace LpAutomation.Contracts.Config;

public sealed record ConfigGetResponse(StrategyConfigDocument Config, string ConfigHash);
public sealed record ConfigPutRequest(StrategyConfigDocument Config);
public sealed record ConfigValidateResponse(bool IsValid, List<ValidationIssue> Issues, List<AutoCorrection> Corrections);

public sealed record ConfigHistoryItem(long Id, DateTimeOffset CreatedUtc, string CreatedBy, string ConfigHash);

public sealed record ActivePoolDto(long ChainId, string Token0, string Token1, int FeeTier);
public sealed record ConfigImportRequest(string Json);

