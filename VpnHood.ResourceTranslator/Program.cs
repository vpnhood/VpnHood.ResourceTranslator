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
        string? basePath = null;
        string? extraPromptPath = null;
        string? apiKey = null;
        var model = DefaultModel;
        var showChanges = false;
        string? rebuildLang = null;
        var rebuildHashes = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-b":
                case "--base":
                    basePath = GetArgValue(args, ref i);
                    break;
                case "-x":
                case "--extra-prompt":
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
                case "-r":
                case "--rebuild-lang":
                    rebuildLang = GetArgValue(args, ref i);
                    break;
                case "-i":
                case "--ignore-changes":
                    rebuildHashes = true;
                    break;
                case "-h":
                case "--help":
                    PrintHelp();
                    return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(basePath))
        {
            Console.Write("Enter path to base language file (e.g., en.json, fr.json): ");
            basePath = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(basePath))
        {
            await Console.Error.WriteLineAsync("Error: Base language file path is required.");
            return 1;
        }

        basePath = Path.GetFullPath(basePath);
        if (!File.Exists(basePath))
        {
            await Console.Error.WriteLineAsync($"Error: File not found: {basePath}");
            return 2;
        }

        // Extract source language from filename (e.g., "en" from "en.json")
        var sourceLanguage = Path.GetFileNameWithoutExtension(basePath);

        ArgumentNullException.ThrowIfNull(Environment.ProcessPath, "Could not determine process path.");
        var prompt = Path.Combine(Environment.ProcessPath, "translation-prompt.txt");
        string? extraPrompt = null;
        if (!string.IsNullOrWhiteSpace(extraPromptPath))
            extraPrompt = await File.ReadAllTextAsync(Path.GetFullPath(extraPromptPath));

        var hashPath = GetHashesFilePath(basePath);

        // Load base language file
        if (!TryLoadJsonObject(basePath, out var baseObj, out var loadErr))
        {
            await Console.Error.WriteLineAsync($"Error: Failed to parse base JSON: {loadErr}");
            return 3;
        }

        var orderedKeys = baseObj!.Select(p => p.Key).ToList();
        var baseMap = baseObj!.ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<string>() ?? string.Empty);

        // Compute current hashes
        var currentHashes = ComputeHashes(baseMap);
        var previousHashes = await LoadHashesAsync(hashPath);

        // Handle rebuild hashes only
        if (rebuildHashes)
        {
            await SaveHashesAsync(hashPath, currentHashes);
            Console.WriteLine($"✓ Hashes rebuilt for {orderedKeys.Count} keys. All entries now marked as current.");
            return 0;
        }

        var changedKeys = DetermineChangedKeys(baseMap.Keys, currentHashes, previousHashes);

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
        var dir = Path.GetDirectoryName(basePath)!;
        var baseFileName = Path.GetFileName(basePath);
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).Equals(baseFileName, StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(p).Equals(Path.GetFileName(hashPath), StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Handle rebuild specific language
        if (!string.IsNullOrWhiteSpace(rebuildLang))
        {
            var rebuildPath = Path.Combine(dir, $"{rebuildLang}.json");
            await TranslateFileAsync(rebuildPath, orderedKeys, baseMap, baseMap.Keys.ToHashSet(), translator,
                prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, isRebuild: true);
        }
        else
        {
            if (files.Count == 0)
                Console.WriteLine("No sibling locale files found to translate.");

            foreach (var localePath in files)
            {
                await TranslateFileAsync(localePath, orderedKeys, baseMap, changedKeys, translator,
                    prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, isRebuild: false);
            }
        }

        // Save updated hashes (only after attempting translations)
        await SaveHashesAsync(hashPath, currentHashes);
        Console.WriteLine("Done.");
        return 0;
    }

    private static async Task TranslateFileAsync(
        string localePath,
        List<string> orderedKeys,
        Dictionary<string, string> baseMap,
        HashSet<string> changedKeys,
        ITranslator translator,
        string prompt,
        string? extraPrompt,
        string sourceLanguage,
        bool isRebuild = false)
    {
        var localeCode = Path.GetFileNameWithoutExtension(localePath);

        if (!TryLoadJsonObject(localePath, out var localeObj, out _))
            localeObj = new JsonObject();

        var localeMap = new Dictionary<string, string>();
        foreach (var kv in localeObj!)
            localeMap[kv.Key] = kv.Value?.GetValue<string>() ?? string.Empty;

        var output = new JsonObject();
        var translatedCount = 0;
        var totalKeys = orderedKeys.Count;
        var missingKeys = orderedKeys.Where(key => !localeMap.ContainsKey(key) || string.IsNullOrWhiteSpace(localeMap[key])).ToList();

        if (isRebuild)
            Console.WriteLine($"Rebuilding {Path.GetFileName(localePath)} ({localeCode}) - translating all {totalKeys} keys...");
        else if (missingKeys.Count > 0)
            Console.WriteLine($"Processing {Path.GetFileName(localePath)} ({localeCode}) - {changedKeys.Count} changed, {missingKeys.Count} missing entries...");

        foreach (var key in orderedKeys)
        {
            var baseText = baseMap[key];
            var translated = localeMap.TryGetValue(key, out var value) ? value : string.Empty;
            var isMissing = !localeMap.ContainsKey(key) || string.IsNullOrWhiteSpace(translated);
            var needsTranslation = isRebuild || changedKeys.Contains(key) || isMissing;

            if (needsTranslation)
            {
                if (LooksLikeUrl(baseText))
                {
                    translated = baseText; // keep URLs as-is
                }
                else
                {
                    var promptOptions = BuildPromptOptions(baseText, sourceLanguage, localeCode, key, prompt: prompt, extraPrompt: extraPrompt);
                    var aiResult = await SafeTranslateAsync(translator, promptOptions);
                    
                    // If AI returns "*", skip translation and keep existing value
                    if (aiResult.Trim() != "*")
                    {
                        translated = PostProcessTranslation(baseText, aiResult);
                        translatedCount++;
                    }

                    if (isRebuild && translatedCount % 10 == 0)
                        Console.WriteLine($"  Progress: {translatedCount}/{totalKeys} ({(translatedCount * 100 / totalKeys):F0}%)");
                }
            }

            output[key] = translated;
        }

        // Write JSON preserving base order
        await WriteJsonAsync(localePath, output);
        
        if (isRebuild)
            Console.WriteLine($"✓ {Path.GetFileName(localePath)}: Rebuilt with {translatedCount} translations.");
        else if (translatedCount > 0)
            Console.WriteLine($"✓ {Path.GetFileName(localePath)}: {translatedCount} translated/updated.");
        else
            Console.WriteLine($"  {Path.GetFileName(localePath)}: Up to date, no changes needed.");
    }

    private static PromptOptions BuildPromptOptions(string text, string sourceLang, string targetLang,
        string key, string prompt, string? extraPrompt)
    {
        var promptBuilder = new StringBuilder(prompt);

        if (!string.IsNullOrWhiteSpace(extraPrompt))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Additional guidelines:");
            promptBuilder.AppendLine(extraPrompt);
        }

        return new PromptOptions
        {
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Text = text,
            Key = key,
            Prompt = promptBuilder.ToString()
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
        Console.WriteLine("  -b, --base <path>          Path to base language file (e.g., en.json, fr.json, de.json)");
        Console.WriteLine("  -x, --extra-prompt <path>  Path to extra instructions text file for the AI prompt");
        Console.WriteLine("  -c, --show-changes         Show changed keys since last translation and exit");
        Console.WriteLine("  -r, --rebuild-lang <code>  Force rebuild/translate all items for specific language (e.g., 'fr', 'es')");
        Console.WriteLine("  -i, --ignore-changes       Rebuild hash file to mark all entries as current (no translation)");
        Console.WriteLine("  -k, --api-key <key>        Gemini API key (or set GEMINI_API_KEY env var)");
        Console.WriteLine("  -m, --model <name>         Gemini model (default: gemini-1.5-flash)");
        Console.WriteLine("  -h, --help                 Show help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  VpnHood.ResourceTranslator -b locales/en.json");
        Console.WriteLine("  VpnHood.ResourceTranslator -b locales/fr.json -r es");
        Console.WriteLine("  VpnHood.ResourceTranslator -b locales/en.json -x custom-rules.txt");
        Console.WriteLine("  VpnHood.ResourceTranslator -b locales/de.json -c");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  - Any language can be used as the base source for translations");
        Console.WriteLine("  - Missing entries in target languages are always translated regardless of hash changes");
        Console.WriteLine("  - Use --ignore-changes after manual translations to avoid re-translating unchanged entries");
    }

    private static bool TryLoadJsonObject(string path, out JsonObject? obj, out string? error)
    {
        try
        {
            var text = File.ReadAllText(path);
            var doc = JsonNode.Parse(text) as JsonObject;
            obj = doc ?? throw new Exception("Root is not a JSON object.");
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

    private static string GetHashesFilePath(string basePath)
    {
        // Same directory, suffix .hashes.json, e.g., en.json.hashes.json, fr.json.hashes.json
        return Path.Combine(Path.GetDirectoryName(basePath)!, Path.GetFileName(basePath) + ".hashes.json");
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