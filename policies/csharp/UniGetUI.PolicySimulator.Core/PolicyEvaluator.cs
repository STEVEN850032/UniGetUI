using System.Text.RegularExpressions;

namespace UniGetUI.PolicySimulator.Core;

public sealed class PolicyEvaluator
{
    public PolicyDecision Evaluate(PolicyDocument policy, PackageRequest request)
    {
        ValidatePolicyShape(policy);
        ValidateRequestShape(request);

        var matchedRules = new List<MatchedRule>();
        foreach (var rule in policy.Rules)
        {
            if (rule.Enabled == false)
            {
                continue;
            }

            if (RuleMatches(rule, request))
            {
                matchedRules.Add(new MatchedRule(rule.Id, rule.Priority, rule.Decision, rule.Reason));
            }
        }

        if (matchedRules.Count == 0)
        {
            var defaultDecision = NormalizeDefaultDecision(policy.Enforcement.DefaultDecision);
            return new PolicyDecision(
                defaultDecision,
                "<default>",
                null,
                $"No enabled rule matched; using defaultDecision '{defaultDecision}'.",
                []);
        }

        var winner = matchedRules
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Decision.Equals("deny", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .First();

        return new PolicyDecision(winner.Decision, winner.Id, winner.Priority, winner.Reason ?? "Rule matched.", matchedRules);
    }

    public static void ValidatePolicyShape(PolicyDocument policy)
    {
        if (string.IsNullOrWhiteSpace(policy.PolicyType)) throw new PolicyValidationException("Policy field 'policyType' is required.");
        if (policy.PolicyType != "packageBrokerPolicy") throw new PolicyValidationException("Policy field 'policyType' must be 'packageBrokerPolicy'.");
        if (string.IsNullOrWhiteSpace(policy.PolicyVersion)) throw new PolicyValidationException("Policy field 'policyVersion' is required.");
        if (string.IsNullOrWhiteSpace(policy.Metadata.Id)) throw new PolicyValidationException("Policy field 'metadata.id' is required.");
        if (string.IsNullOrWhiteSpace(policy.Enforcement.FailureDecision)) throw new PolicyValidationException("Policy field 'enforcement.failureDecision' is required.");
        if (string.IsNullOrWhiteSpace(policy.Enforcement.DefaultDecision)) throw new PolicyValidationException("Policy field 'enforcement.defaultDecision' is required.");
        if (string.IsNullOrWhiteSpace(policy.Enforcement.RulePrecedence)) throw new PolicyValidationException("Policy field 'enforcement.rulePrecedence' is required.");
        if (policy.Rules.Count == 0) throw new PolicyValidationException("Policy field 'rules' must contain at least one rule.");
    }

    public static void ValidateRequestShape(PackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestType)) throw new PolicyValidationException("Request field 'requestType' is required.");
        if (request.RequestType != "packageOperation") throw new PolicyValidationException("Request field 'requestType' must be 'packageOperation'.");
        if (string.IsNullOrWhiteSpace(request.RequestVersion)) throw new PolicyValidationException("Request field 'requestVersion' is required.");
        if (string.IsNullOrWhiteSpace(request.RequestId)) throw new PolicyValidationException("Request field 'requestId' is required.");
        if (string.IsNullOrWhiteSpace(request.Operation)) throw new PolicyValidationException("Request operation is required.");
        if (string.IsNullOrWhiteSpace(request.Manager.Name)) throw new PolicyValidationException("Request manager.name is required.");
        if (string.IsNullOrWhiteSpace(request.Source.Name)) throw new PolicyValidationException("Request source.name is required.");
        if (string.IsNullOrWhiteSpace(request.Package.Id)) throw new PolicyValidationException("Request package.id is required.");
        if (string.IsNullOrWhiteSpace(request.Package.Name)) throw new PolicyValidationException("Request package.name is required.");
        if (string.IsNullOrWhiteSpace(request.Broker.RequestedElevation)) throw new PolicyValidationException("Request broker.requestedElevation is required.");
    }

    private static string NormalizeDefaultDecision(string? decision)
    {
        return string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase) ? "allow" : "deny";
    }

    private static bool RuleMatches(PolicyRule rule, PackageRequest request)
    {
        var flags = RequestFlags.FromRequest(request);
        var effectiveVersion = GetEffectiveVersion(request);

        return ValueInList(request.Operation, rule.Match.Operations) &&
            ValueInList(request.Manager.Name, rule.Match.Managers) &&
            WildcardAny(request.Source.Name, rule.Match.Sources) &&
            WildcardAny(request.Package.Id, rule.Match.PackageIdentifiers) &&
            WildcardAny(request.Package.Name, rule.Match.PackageNames) &&
            ValueInList(effectiveVersion, rule.Match.Versions) &&
            VersionRangeMatches(effectiveVersion, rule.Match.VersionRange) &&
            ValueInList(request.Options.Scope, rule.Match.Scopes) &&
            ValueInList(request.Options.Architecture, rule.Match.Architectures) &&
            ValueInList(request.Broker.RequestedElevation, rule.Match.Elevation) &&
            ValueInList(request.Options.RunAsAdministrator, rule.Match.RunAsAdministrator) &&
            ValueInList(request.Options.Interactive, rule.Match.Interactive) &&
            ValueInList(request.Options.SkipHashCheck, rule.Match.SkipHashCheck) &&
            ValueInList(request.Options.PreRelease, rule.Match.PreRelease) &&
            ValueInList(flags.HasCustomParameters, rule.Match.HasCustomParameters) &&
            ValueInList(flags.HasCustomInstallLocation, rule.Match.HasCustomInstallLocation) &&
            ValueInList(flags.HasPrePostCommands, rule.Match.HasPrePostCommands) &&
            ValueInList(flags.HasKillBeforeOperation, rule.Match.HasKillBeforeOperation) &&
            ConstraintsPass(rule.Constraints, request, flags);
    }

    private static bool ConstraintsPass(PolicyConstraints? constraints, PackageRequest request, RequestFlags flags)
    {
        if (constraints is null) return true;
        if (constraints.AllowInteractive == false && request.Options.Interactive) return false;
        if (constraints.AllowRunAsAdministrator == false && request.Options.RunAsAdministrator) return false;
        if (constraints.AllowSkipHashCheck == false && request.Options.SkipHashCheck) return false;
        if (constraints.AllowPreRelease == false && request.Options.PreRelease) return false;
        if (constraints.AllowCustomInstallLocation == false && flags.HasCustomInstallLocation) return false;
        if (constraints.AllowCustomParameters == false && flags.HasCustomParameters) return false;
        if (constraints.AllowPrePostCommands == false && flags.HasPrePostCommands) return false;
        if (constraints.AllowKillBeforeOperation == false && flags.HasKillBeforeOperation) return false;

        if (flags.HasCustomInstallLocation && constraints.AllowedInstallLocationPatterns is not null && !WildcardAny(flags.CustomInstallLocation, constraints.AllowedInstallLocationPatterns))
        {
            return false;
        }

        foreach (var parameter in flags.CustomParameters)
        {
            if (constraints.DeniedCustomParameters is not null && WildcardAny(parameter, constraints.DeniedCustomParameters)) return false;
            if (constraints.AllowedCustomParameters is not null || constraints.AllowedCustomParameterPatterns is not null)
            {
                var exactAllowed = ValueInList(parameter, constraints.AllowedCustomParameters);
                var patternAllowed = WildcardAny(parameter, constraints.AllowedCustomParameterPatterns);
                if (!exactAllowed && !patternAllowed) return false;
            }
        }

        return true;
    }

    private static bool ValueInList<T>(T? value, IReadOnlyCollection<T>? list)
    {
        return list is null || list.Contains(value!);
    }

    private static bool WildcardAny(string? value, IReadOnlyCollection<string>? patterns)
    {
        if (patterns is null) return true;
        if (value is null) return false;
        return patterns.Any(pattern => Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase));
    }

    private static string GetEffectiveVersion(PackageRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Options.Version)) return request.Options.Version;
        if (!string.IsNullOrWhiteSpace(request.Package.NewVersion)) return request.Package.NewVersion;
        if (!string.IsNullOrWhiteSpace(request.Package.Version)) return request.Package.Version;
        return string.Empty;
    }

    private static bool VersionRangeMatches(string version, VersionRange? range)
    {
        if (range is null) return true;
        if (string.IsNullOrWhiteSpace(version)) return false;
        if (version.Contains('-', StringComparison.Ordinal) && !range.IncludePrerelease) return false;
        if (!string.IsNullOrWhiteSpace(range.MinVersion) && CompareVersions(version, range.MinVersion) < 0) return false;
        if (!string.IsNullOrWhiteSpace(range.MaxVersion) && CompareVersions(version, range.MaxVersion) > 0) return false;
        return true;
    }

    private static int CompareVersions(string left, string right)
    {
        var normalizedLeft = left.Split('-', 2)[0];
        var normalizedRight = right.Split('-', 2)[0];
        return Version.TryParse(normalizedLeft, out var leftVersion) && Version.TryParse(normalizedRight, out var rightVersion)
            ? leftVersion.CompareTo(rightVersion)
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RequestFlags(bool HasCustomParameters, bool HasCustomInstallLocation, bool HasPrePostCommands, bool HasKillBeforeOperation, IReadOnlyList<string> CustomParameters, string CustomInstallLocation)
    {
        public static RequestFlags FromRequest(PackageRequest request)
        {
            var customParameters = request.Options.CustomParameters ?? [];
            var customInstallLocation = request.Options.CustomInstallLocation ?? string.Empty;
            return new RequestFlags(
                customParameters.Count > 0,
                !string.IsNullOrWhiteSpace(customInstallLocation),
                !string.IsNullOrWhiteSpace(request.Options.PreOperationCommand) || !string.IsNullOrWhiteSpace(request.Options.PostOperationCommand),
                request.Options.KillBeforeOperation?.Count > 0,
                customParameters,
                customInstallLocation);
        }
    }
}