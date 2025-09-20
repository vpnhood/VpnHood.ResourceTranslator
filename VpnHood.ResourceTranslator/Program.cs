using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnHood.ResourceTranslator.Models;
using VpnHood.ResourceTranslator.Translators;

namespace VpnHood.ResourceTranslator;

internal static class Program
{
    private const string DefaultModel = "gemini-1.5-flash"; // Can be overridden via --model

    private static async Task<int> Main(string[] args)
    {
        // Simple args parsing
        string? enPath = null;
        string? extraPromptPath = null;
        string? apiKey = null;
        var model = DefaultModel;
        var showChanges = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-e":
                case "--en":
                    enPath = GetArgValue(args, ref i);
                    break;
                case "-x":
                case "--exceptions":
                    extraPromptPath = GetArgValue(args, ref i);
                    break;
                case "-k":
                case "--api-key":
                    apiKey = GetArgValue(args, ref i);
                    break;
                case "-m":
                case "--model":
                    model = GetArgValue(args, ref i);
                    break;
                case "-c":
                case "--show-changes":
                    showChanges = true;
                    break;
                case "-h":
                case "--help":
                    PrintHelp();
                    return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(enPath))
        {
            Console.Write("Enter path to en.json: ");
            enPath = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(enPath))
        {
            await Console.Error.WriteLineAsync("Error: en.json path is required.");
            return 1;
        }

        enPath = Path.GetFullPath(enPath);
        if (!File.Exists(enPath))
        {
            await Console.Error.WriteLineAsync($"Error: File not found: {enPath}");
            return 2;
        }

        string? extraPrompt = null;
        if (!string.IsNullOrWhiteSpace(extraPromptPath))
        {
            var extraPromptFullPath = Path.GetFullPath(extraPromptPath);
            if (!File.Exists(extraPromptFullPath))
            {
                await Console.Error.WriteLineAsync($"Warning: Extra prompt file not found: {extraPromptFullPath}");
            }
            else
            {
                extraPrompt = await File.ReadAllTextAsync(extraPromptFullPath);
            }
        }

        var hashPath = GetHashesFilePath(enPath);

        // Load base en.json
        if (!TryLoadJsonObject(enPath, out var enObj, out var loadErr))
        {
            await Console.Error.WriteLineAsync($"Error: Failed to parse base JSON: {loadErr}");
            return 3;
        }

        var orderedKeys = enObj!.Select(p => p.Key).ToList();
        var enMap = enObj.ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<string>() ?? string.Empty);

        // Compute current hashes
        var currentHashes = ComputeHashes(enMap);
        var previousHashes = await LoadHashesAsync(hashPath);

        var changedKeys = DetermineChangedKeys(enMap.Keys, currentHashes, previousHashes);

        if (showChanges)
        {
            Console.WriteLine($"Changed keys since last translation: {changedKeys.Count}");
            foreach (var key in changedKeys)
            {
                Console.WriteLine($" - {key}");
            }
            return 0;
        }

        apiKey ??= Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await Console.Error.WriteLineAsync("Error: Missing Gemini API key. Provide via --api-key or GEMINI_API_KEY env var.");
            return 4;
        }

        ITranslator translator = new GeminiTranslator(apiKey, model);

        // Find sibling locale files (all *.json except the base and our hash file)
        var dir = Path.GetDirectoryName(enPath)!;
        var baseFileName = Path.GetFileName(enPath);
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).Equals(baseFileName, StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(p).Equals(Path.GetFileName(hashPath), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No sibling locale files found to translate.");
        }

        foreach (var localePath in files)
        {
            await TranslateFileAsync(localePath, orderedKeys, enMap, changedKeys, translator, extraPrompt);
        }

        // Save updated hashes (only after attempting translations)
        await SaveHashesAsync(hashPath, currentHashes);
        Console.WriteLine("Done.");
        return 0;
    }

    private static async Task TranslateFileAsync(
        string localePath,
        List<string> orderedKeys,
        Dictionary<string, string> enMap,
        HashSet<string> changedKeys,
        ITranslator translator,
        string? extraPrompt)
    {
        var localeCode = Path.GetFileNameWithoutExtension(localePath);

        if (!TryLoadJsonObject(localePath, out var localeObj, out _))
        {
            localeObj = new JsonObject();
        }

        var localeMap = new Dictionary<string, string>();
        foreach (var kv in localeObj!)
            localeMap[kv.Key] = kv.Value?.GetValue<string>() ?? string.Empty;

        var output = new JsonObject();
        var translatedCount = 0;

        foreach (var key in orderedKeys)
        {
            var enText = enMap[key];
            var translated = localeMap.TryGetValue(key, out var value) ? value : string.Empty;
            var needsTranslation = changedKeys.Contains(key) || !localeMap.ContainsKey(key);

            if (needsTranslation)
            {
                if (LooksLikeUrl(enText))
                {
                    translated = enText; // keep URLs as-is
                }
                else
                {
                    var promptOptions = BuildPromptOptions(enText, "en", localeCode, extraPrompt);
                    translated = await SafeTranslateAsync(translator, promptOptions);
                    translated = PostProcessTranslation(enText, translated);
                    translatedCount++;
                }
            }

            output[key] = translated;
        }

        // Write JSON preserving base order
        await WriteJsonAsync(localePath, output);
        Console.WriteLine($"{Path.GetFileName(localePath)}: {translatedCount} translated/updated.");
    }

    private static PromptOptions BuildPromptOptions(string text, string sourceLang, string targetLang, string? extraPrompt)
    {
        ArgumentNullException.ThrowIfNull(Environment.ProcessPath, "Could not determine process path.");
        var promptFilePath = Path.Combine(Environment.ProcessPath, "translation-prompt.txt");
        var prompt = new StringBuilder(File.ReadAllText(promptFilePath));
        
        if (!string.IsNullOrWhiteSpace(extraPrompt))
        {
            prompt.AppendLine();
            prompt.AppendLine("Additional guidelines:");
            prompt.AppendLine(extraPrompt);
        }

        return new PromptOptions
        {
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Text = text,
            Prompt = prompt.ToString()
        };
    }

    private static string GetArgValue(string[] args, ref int i)
    {
        if (i + 1 < args.Length) { return args[++i]; }
        return string.Empty;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Resource Translator");
        Console.WriteLine("Usage: VpnHood.ResourceTranslator [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -e, --en <path>            Path to base en.json");
        Console.WriteLine("  -x, --exceptions <path>    Path to extra instructions text file for the AI prompt");
        Console.WriteLine("  -c, --show-changes         Show changed keys since last translation and exit");
        Console.WriteLine("  -k, --api-key <key>        Gemini API key (or set GEMINI_API_KEY env var)");
        Console.WriteLine("  -m, --model <name>         Gemini model (default: gemini-1.5-flash)");
        Console.WriteLine("  -h, --help                 Show help");
    }

    private static bool TryLoadJsonObject(string path, out JsonObject? obj, out string? error)
    {
        try
        {
            var text = File.ReadAllText(path);
            var doc = JsonNode.Parse(text) as JsonObject;
            if (doc == null) throw new Exception("Root is not a JSON object.");
            obj = doc;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            obj = null;
            error = ex.Message;
            return false;
        }
    }

    private static async Task WriteJsonAsync(string path, JsonObject obj)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, obj, options);
    }

    private static Dictionary<string, string> ComputeHashes(Dictionary<string, string> map)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in map)
        {
            var hash = Sha256(v);
            result[k] = hash;
        }
        return result;
    }

    private static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static async Task<Dictionary<string, string>> LoadHashesAsync(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var txt = await File.ReadAllTextAsync(path);
            var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(txt) ?? new();
            return new Dictionary<string, string>(obj, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static async Task SaveHashesAsync(string path, Dictionary<string, string> hashes)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(hashes, options);
        await File.WriteAllTextAsync(path, txt);
    }

    private static HashSet<string> DetermineChangedKeys(IEnumerable<string> keys,
        Dictionary<string, string> currentHashes,
        Dictionary<string, string> previousHashes)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var cur = currentHashes.GetValueOrDefault(key);
            var prev = previousHashes.GetValueOrDefault(key);
            if (!string.Equals(cur, prev, StringComparison.Ordinal))
                changed.Add(key);
        }
        return changed;
    }

    private static string GetHashesFilePath(string enPath)
    {
        // Same directory, suffix .hashes.json, e.g., en.json.hashes.json
        return Path.Combine(Path.GetDirectoryName(enPath)!, Path.GetFileName(enPath) + ".hashes.json");
    }

    private static bool LooksLikeUrl(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        return s.Contains("://", StringComparison.Ordinal) || s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> SafeTranslateAsync(ITranslator translator, PromptOptions promptOptions)
    {
        // Retry a few times on transient failures
        var attempt = 0;
        Exception? lastEx = null;
        while (attempt < 3)
        {
            try
            {
                return await translator.TranslateAsync(promptOptions, CancellationToken.None);
            }
            catch (Exception ex)
            {
                lastEx = ex;
                await Task.Delay(500 * (attempt + 1));
            }
            attempt++;
        }
        throw new Exception($"Translation failed after retries: {lastEx?.Message}", lastEx);
    }

    private static string PostProcessTranslation(string source, string? translated)
    {
        if (translated == null) return string.Empty;
        translated = translated.Trim();
        // Remove wrapping quotes/backticks if present
        if ((translated.StartsWith("\"") && translated.EndsWith("\"")) ||
            (translated.StartsWith("'") && translated.EndsWith("'")) ||
            (translated.StartsWith("`") && translated.EndsWith("`")))
        {
            translated = translated.Substring(1, translated.Length - 2);
        }

        // Ensure placeholders like {x} remain present
        var tokens = ExtractPlaceholders(source);
        foreach (var token in tokens)
        {
            if (!translated.Contains(token, StringComparison.Ordinal))
            {
                // If token missing, append it to avoid breaking runtime formatting
                translated = translated + (translated.EndsWith(" ") ? string.Empty : " ") + token;
            }
        }
        return translated;
    }

    private static List<string> ExtractPlaceholders(string s)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(s)) return list;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '{')
            {
                var j = s.IndexOf('}', i + 1);
                if (j > i)
                {
                    list.Add(s.Substring(i, j - i + 1));
                    i = j;
                }
            }
        }
        return list;
    }
}