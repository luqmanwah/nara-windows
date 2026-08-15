using System.Text.Json;

namespace Nara.PlaybookEngine;

internal static class Program
{
    internal static int Main(string[] args)
    {
        if (!TryParseArguments(args, out ParsedArguments? parsed, out bool showHelp, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintHelp(Console.Error);
            return 2;
        }

        if (showHelp)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        try
        {
            if (parsed!.VerifyLedgerPath is not null)
            {
                long count = LedgerChain.Verify(parsed.VerifyLedgerPath);
                Console.Out.WriteLine($"LEDGER VALID events={count}");
                return 0;
            }

            ExecutionRequest request = new(
                parsed.ActionPlanPath!,
                parsed.PlaybookPath!,
                parsed.SignaturePath!,
                parsed.TrustManifestPath!,
                parsed.TrustManifestSignaturePath!,
                parsed.ApprovalPath!,
                parsed.StatePath!,
                parsed.LedgerPath!,
                parsed.ReportPath!,
                parsed.TestFailAfterAction);
            ExecutionResult result = TransactionEngine.Execute(request);
            Console.Out.WriteLine(JsonSerializer.Serialize(result.Report, JsonSupport.IndentedOptions));
            return result.ExitCode;
        }
        catch (LedgerIntegrityException exception)
        {
            Console.Error.WriteLine($"Ledger verification failed: {exception.Message}");
            return 4;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Transaction failed safely: {exception.Message}");
            return 1;
        }
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out ParsedArguments? parsed,
        out bool showHelp,
        out string? error)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        parsed = null;
        showHelp = false;
        error = null;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            string[] supported =
            [
                "--plan",
                "--playbook",
                "--signature",
                "--trust-manifest",
                "--trust-signature",
                "--approval",
                "--state",
                "--ledger",
                "--report",
                "--verify-ledger",
                "--test-fail-after-action"
            ];
            if (!supported.Contains(argument, StringComparer.Ordinal))
            {
                error = $"Unknown argument: {argument}";
                return false;
            }
            if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                error = $"{argument} requires a value.";
                return false;
            }
            if (!values.TryAdd(argument, args[++index]))
            {
                error = $"Argument was provided more than once: {argument}";
                return false;
            }
        }

        if (showHelp)
        {
            return true;
        }

        if (values.TryGetValue("--verify-ledger", out string? verifyLedger))
        {
            if (values.Count != 1)
            {
                error = "--verify-ledger cannot be combined with transaction arguments.";
                return false;
            }
            parsed = new ParsedArguments(null, null, null, null, null, null, null, null, null, verifyLedger, null);
            return true;
        }

        string[] required = ["--plan", "--playbook", "--signature", "--trust-manifest", "--trust-signature", "--approval", "--state", "--ledger", "--report"];
        foreach (string name in required)
        {
            if (!values.ContainsKey(name))
            {
                error = $"Missing required argument: {name}";
                return false;
            }
        }

        values.TryGetValue("--test-fail-after-action", out string? failAfter);
        parsed = new ParsedArguments(
            values["--plan"],
            values["--playbook"],
            values["--signature"],
            values["--trust-manifest"],
            values["--trust-signature"],
            values["--approval"],
            values["--state"],
            values["--ledger"],
            values["--report"],
            null,
            failAfter);
        return true;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Nara Playbook Engine 0.3.0 — simulation only");
        writer.WriteLine("Execute: --plan <json> --playbook <json> --signature <json> --trust-manifest <json> --trust-signature <json> --approval <json> --state <fake-state.json> --ledger <jsonl> --report <json>");
        writer.WriteLine("Verify ledger: --verify-ledger <ledger.jsonl>");
        writer.WriteLine("Test rollback: --test-fail-after-action <rule-id>");
        writer.WriteLine("This version cannot change Windows.");
    }

    private sealed record ParsedArguments(
        string? ActionPlanPath,
        string? PlaybookPath,
        string? SignaturePath,
        string? TrustManifestPath,
        string? TrustManifestSignaturePath,
        string? ApprovalPath,
        string? StatePath,
        string? LedgerPath,
        string? ReportPath,
        string? VerifyLedgerPath,
        string? TestFailAfterAction);
}
