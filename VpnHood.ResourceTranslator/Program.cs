using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnHood.ResourceTranslator.Models;
using VpnHood.ResourceTranslator.Translators;

namespace VpnHood.ResourceTranslator;

internal static class Program
{
    private const int DefaultTranslateTimeoutSeconds = 90;

    private static async Task<int> Main(string[] args)
    {
        var argumentParser = new ArgumentParser();
        
        if (!argumentParser.Parse(args))
        {
            return 1;
        }

        if (argumentParser.ShowHelp)
        {
            ArgumentParser.PrintHelp();
            return 0;
        }

        // Get base path
        var basePath = argumentParser.BasePath;
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

        // Select engine and model
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(argumentParser.Engine, argumentParser.Model);

        // Extract source language from filename (e.g., "en" from "en.json")
        var sourceLanguage = Path.GetFileNameWithoutExtension(basePath);

        // Load prompt files
        ArgumentNullException.ThrowIfNull(Environment.ProcessPath, "Could not determine process file.");
        var promptFile = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "translation-prompt.txt");
        var prompt = await File.ReadAllTextAsync(promptFile);

        string? extraPrompt = null;
        if (!string.IsNullOrWhiteSpace(argumentParser.ExtraPromptPath))
            extraPrompt = await File.ReadAllTextAsync(Path.GetFullPath(argumentParser.ExtraPromptPath));

        var hashesPath = GetHashesFilePath(basePath);

        // Load base language file
        if (!TryLoadJsonObject(basePath, out var baseObj, out var loadErr))
        {
            await Console.Error.WriteLineAsync($"Error: Failed to parse base JSON: {loadErr}");
            return 3;
        }

        var orderedKeys = baseObj!.Select(p => p.Key).ToList();
        var baseMap = baseObj!.ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<string>() ?? string.Empty);

        // Compute current hashes: MD5 only
        var currentMd5 = ComputeMd5Hashes(baseMap);

        // Load previous hashes (MD5 file only)
        var previousHashes = await LoadHashesAsync(hashesPath);

        // Handle rebuild hashes only
        if (argumentParser.RebuildHashes)
        {
            await SaveHashesAsync(hashesPath, currentMd5);
            Console.WriteLine($"✓ Hashes rebuilt for {orderedKeys.Count} keys. All entries now marked as current.");
            return 0;
        }

        var changedKeys = DetermineChangedKeys(baseMap.Keys, currentMd5, previousHashes);

        if (argumentParser.ShowChanges)
        {
            Console.WriteLine($"Changed keys since last translation: {changedKeys.Count}");
            foreach (var key in changedKeys)
            {
                Console.WriteLine($" - {key}");
            }
            return 0;
        }

        // Get API key
        var apiKey = argumentParser.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var envVarName = EngineModelSelector.GetEnvironmentVariableName(engine);
            apiKey = Environment.GetEnvironmentVariable(envVarName);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var envVarName = EngineModelSelector.GetEnvironmentVariableName(engine);
            await Console.Error.WriteLineAsync($"Error: Missing API key. Provide via --api-key or {envVarName} env var.");
            return 4;
        }

        // Create translator based on engine
        ITranslator translator = engine.ToLowerInvariant() switch
        {
            "gpt" or "chatgpt" => new ChatGptTranslator(apiKey, model),
            "gemini" => new GeminiTranslator(apiKey, model),
            _ => throw new ArgumentException($"Unknown engine: {engine}. Supported engines: gemini, gpt")
        };

        // Find sibling locale files (all *.json except the base)
        var dir = Path.GetDirectoryName(basePath)!;
        var baseFileName = Path.GetFileName(basePath);
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Handle rebuild specific language
        if (!string.IsNullOrWhiteSpace(argumentParser.RebuildLang))
        {
            var rebuildPath = Path.Combine(dir, $"{argumentParser.RebuildLang}.json");
            await TranslateFileAsync(rebuildPath, orderedKeys, baseMap, baseMap.Keys.ToHashSet(), translator,
                prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, batchSize: argumentParser.BatchSize, isRebuild: true);
        }
        else
        {
            if (files.Count == 0)
                Console.WriteLine("No sibling locale files found to translate.");

            foreach (var localePath in files)
            {
                await TranslateFileAsync(localePath, orderedKeys, baseMap, changedKeys, translator,
                    prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, batchSize: argumentParser.BatchSize);
            }
        }

        // Save updated hashes (only after attempting translations)
        await SaveHashesAsync(hashesPath, currentMd5);
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
        int batchSize,
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

        // Pre-populate output with existing values or base for URLs
        var itemsToTranslate = new List<TranslateItem>(totalKeys);
        foreach (var key in orderedKeys)
        {
            var baseText = baseMap[key];
            var hasExisting = localeMap.TryGetValue(key, out var existingValue) && !string.IsNullOrWhiteSpace(existingValue);
            var isMissing = !hasExisting;
            var needsTranslation = isRebuild || changedKeys.Contains(key) || isMissing;

            if (needsTranslation && LooksLikeUrl(baseText))
            {
                output[key] = baseText; // keep URLs as-is
                continue;
            }

            // default output is the existing translation if any, or empty
            output[key] = hasExisting ? existingValue : string.Empty;

            if (needsTranslation && !LooksLikeUrl(baseText))
            {
                itemsToTranslate.Add(new TranslateItem
                {
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = localeCode,
                    Key = key,
                    Text = baseText
                });
            }
        }

        // Batch translate items
        for (int i = 0; i < itemsToTranslate.Count; i += Math.Max(1, batchSize))
        {
            var batch = itemsToTranslate.Skip(i).Take(Math.Max(1, batchSize)).ToArray();
            if (batch.Length == 0) break;

            var promptOptions = BuildPromptOptionsForBatch(batch, prompt, extraPrompt);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTranslateTimeoutSeconds));
            TranslateResult[] results;
            try
            {
                await Task.Delay(500, cts.Token); // brief pause to avoid rate limits
                results = await translator.TranslateAsync(promptOptions, cts.Token);
            }
            catch (OperationCanceledException)
            {
                await Console.Error.WriteLineAsync($"Timeout while translating {Path.GetFileName(localePath)} batch starting at index {i}. Skipping this batch.");
                continue;
            }

            foreach (var res in results)
            {
                // Skip if instructed
                if (res.TranslatedText.Trim() == "*")
                    continue;

                // Post-process and apply
                var baseText = baseMap.GetValueOrDefault(res.Key, string.Empty);
                var finalText = PostProcessTranslation(baseText, res.TranslatedText);
                output[res.Key] = finalText;
                translatedCount++;
            }

            if (isRebuild)
            {
                var done = Math.Min(i + batch.Length, itemsToTranslate.Count);
                Console.WriteLine($"  Progress: {done}/{itemsToTranslate.Count} ({(done * 100 / Math.Max(1, itemsToTranslate.Count)):F0}%)");
            }
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

    private static PromptOptions BuildPromptOptionsForBatch(TranslateItem[] items, string prompt, string? extraPrompt)
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
            Prompt = promptBuilder.ToString(),
            Items = items
        };
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

    private static Dictionary<string, string> ComputeMd5Hashes(Dictionary<string, string> map)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in map)
        {
            result[k] = Md5Hex(v);
        }
        return result;
    }

    private static string Md5Hex(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
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
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(hashes, options);
        await File.WriteAllTextAsync(path, txt);
    }

    private static HashSet<string> DetermineChangedKeys(IEnumerable<string> keys,
        Dictionary<string, string> currentMd5,
        Dictionary<string, string> previous)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var cur = currentMd5.GetValueOrDefault(key);
            var prev = previous.GetValueOrDefault(key);
            if (!string.Equals(cur, prev, StringComparison.Ordinal))
            {
                changed.Add(key);
            }
        }
        return changed;
    }

    private static string GetHashesFilePath(string basePath)
    {
        // Location: <baseDir>/vh_translator/<baseLang>_watch.json
        var baseDir = Path.GetDirectoryName(basePath)!;
        var baseLang = Path.GetFileNameWithoutExtension(basePath);
        return Path.Combine(baseDir, "vh_translator", $"{baseLang}_watch.json");
    }

    private static bool LooksLikeUrl(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        return s.Contains("://", StringComparison.Ordinal) || s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
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