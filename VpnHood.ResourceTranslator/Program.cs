using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnHood.ResourceTranslator.Models;
using VpnHood.ResourceTranslator.Translators;

namespace VpnHood.ResourceTranslator;

internal static class Program
{
    private const int DefaultTranslateTimeoutSeconds = 100;

    private static readonly JsonSerializerOptions OutputSerializerOptions = new() {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static async Task<int> Main(string[] args)
    {
        var baseOption = new Option<string?>("--base", "-b") {
            Description = "Path to base language file (e.g., en.json, fr.json, de.json)"
        };
        var extraPromptOption = new Option<string?>("--extra-prompt", "-x") {
            Description = "Path to extra instructions text file for the AI prompt"
        };
        var showChangesOption = new Option<bool>("--show-changes", "-c") {
            Description = "Show changed keys since last translation and exit"
        };
        var rebuildLangOption = new Option<string?>("--rebuild-lang", "-r") {
            Description = "Force rebuild/translate all items for specific language (e.g., 'fr', 'es')"
        };
        var ignoreChangesOption = new Option<bool>("--ignore-changes", "-i") {
            Description = "Rebuild hash file to mark all entries as current (no translation)"
        };
        var apiKeyOption = new Option<string?>("--api-key", "-k") {
            Description = "API key (or set GEMINI_API_KEY/OPENAI_API_KEY/GROK_API_KEY env var)"
        };
        var modelOption = new Option<string?>("--model", "-m") {
            Description = "AI model (default: gemini-flash-lite-latest, grok-4-latest for grok engine)"
        };
        var engineOption = new Option<string?>("--engine", "-e") {
            Description = "Translation engine: gemini, gpt, or grok (default: auto-detected from model)"
        };
        var batchOption = new Option<int>("--batch", "-n") {
            Description = "Batch size for translation requests",
            DefaultValueFactory = _ => 20
        };
        batchOption.Validators.Add(result => {
            if (result.GetValueOrDefault<int>() <= 0)
                result.AddError("Batch size must be a positive number.");
        });

        var rootCommand = new RootCommand(
            "Translates JSON i18n resource files using AI (Google Gemini, OpenAI ChatGPT, or Grok AI) " +
            "while preserving placeholders, HTML tags, and formatting. " +
            "The engine is auto-detected from the model name if not specified; " +
            "missing entries in target languages are always translated regardless of hash changes.") {
            baseOption,
            extraPromptOption,
            showChangesOption,
            rebuildLangOption,
            ignoreChangesOption,
            apiKeyOption,
            modelOption,
            engineOption,
            batchOption
        };

        rootCommand.SetAction(async (parseResult, _) => {
            var options = new TranslatorOptions {
                BasePath = parseResult.GetValue(baseOption),
                ExtraPromptPath = parseResult.GetValue(extraPromptOption),
                ApiKey = parseResult.GetValue(apiKeyOption),
                Model = parseResult.GetValue(modelOption),
                Engine = parseResult.GetValue(engineOption),
                ShowChanges = parseResult.GetValue(showChangesOption),
                RebuildLang = parseResult.GetValue(rebuildLangOption),
                RebuildHashes = parseResult.GetValue(ignoreChangesOption),
                BatchSize = parseResult.GetValue(batchOption)
            };

            try {
                return await RunAsync(options);
            }
            catch (Exception ex) {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 10;
            }
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunAsync(TranslatorOptions options)
    {
        // Get base path
        var basePath = options.BasePath;
        if (string.IsNullOrWhiteSpace(basePath)) {
            Console.Write("Enter path to base language file (e.g., en.json, fr.json): ");
            basePath = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(basePath)) {
            await Console.Error.WriteLineAsync("Error: Base language file path is required.");
            return 1;
        }

        basePath = Path.GetFullPath(basePath);
        if (!File.Exists(basePath)) {
            await Console.Error.WriteLineAsync($"Error: File not found: {basePath}");
            return 2;
        }

        // Select engine and model
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(options.Engine, options.Model);

        // Extract source language from filename (e.g., "en" from "en.json")
        var sourceLanguage = Path.GetFileNameWithoutExtension(basePath);

        // Load prompt files
        var promptFile = Path.Combine(AppContext.BaseDirectory, "translation-prompt.txt");
        var prompt = await File.ReadAllTextAsync(promptFile);

        string? extraPrompt = null;
        if (!string.IsNullOrWhiteSpace(options.ExtraPromptPath))
            extraPrompt = await File.ReadAllTextAsync(Path.GetFullPath(options.ExtraPromptPath));
        else if (File.Exists(GetCustomPromptFilePath(basePath)))
            extraPrompt = await File.ReadAllTextAsync(GetCustomPromptFilePath(basePath));

        var hashesPath = GetHashesFilePath(basePath);

        // Load base language file
        if (!TryLoadJsonObject(basePath, out var baseObj, out var loadErr)) {
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
        if (options.RebuildHashes) {
            await SaveHashesAsync(hashesPath, currentMd5);
            Console.WriteLine($"✓ Hashes rebuilt for {orderedKeys.Count} keys. All entries now marked as current.");
            return 0;
        }

        var changedKeys = DetermineChangedKeys(baseMap.Keys, currentMd5, previousHashes);

        if (options.ShowChanges) {
            Console.WriteLine($"Changed keys since last translation: {changedKeys.Count}");
            foreach (var key in changedKeys) {
                Console.WriteLine($" - {key}");
            }
            return 0;
        }

        // Get API key
        var apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable(EngineModelSelector.GetEnvironmentVariableName(engine));

        if (string.IsNullOrWhiteSpace(apiKey)) {
            var envVarName = EngineModelSelector.GetEnvironmentVariableName(engine);
            await Console.Error.WriteLineAsync($"Error: Missing API key. Provide via --api-key or {envVarName} env var.");
            return 4;
        }

        // Create translator based on engine
        ITranslator translator = engine switch {
            "gpt" => new ChatGptTranslator(apiKey, model),
            "gemini" => new GeminiTranslator(apiKey, model),
            "grok" => new GrokAiTranslator(apiKey, model),
            _ => throw new ArgumentException($"Unknown engine: {engine}. Supported engines: gemini, gpt, grok")
        };

        var dir = Path.GetDirectoryName(basePath)!;

        // Handle rebuild specific language
        if (!string.IsNullOrWhiteSpace(options.RebuildLang)) {
            var rebuildPath = Path.Combine(dir, $"{options.RebuildLang}.json");
            await TranslateFileAsync(rebuildPath, orderedKeys, baseMap, baseMap.Keys.ToHashSet(), translator,
                prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, batchSize: options.BatchSize, isRebuild: true);
        }
        else {
            // Find sibling locale files (all *.json except the base)
            var baseFileName = Path.GetFileName(basePath);
            var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
                .Where(p => !Path.GetFileName(p).Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
                Console.WriteLine("No sibling locale files found to translate.");

            foreach (var localePath in files) {
                await TranslateFileAsync(localePath, orderedKeys, baseMap, changedKeys, translator,
                    prompt: prompt, extraPrompt: extraPrompt, sourceLanguage: sourceLanguage, batchSize: options.BatchSize);
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
        var localeFileName = Path.GetFileName(localePath);

        if (!TryLoadJsonObject(localePath, out var localeObj, out _))
            localeObj = new JsonObject();

        var localeMap = new Dictionary<string, string>();
        foreach (var kv in localeObj!)
            localeMap[kv.Key] = kv.Value?.GetValue<string>() ?? string.Empty;

        var output = new JsonObject();
        var translatedCount = 0;
        var missingCount = orderedKeys.Count(key => !localeMap.ContainsKey(key) || string.IsNullOrWhiteSpace(localeMap[key]));

        if (isRebuild)
            Console.WriteLine($"Rebuilding {localeFileName} ({localeCode}) - translating all {orderedKeys.Count} keys...");
        else if (missingCount > 0)
            Console.WriteLine($"Processing {localeFileName} ({localeCode}) - {changedKeys.Count} changed, {missingCount} missing entries...");

        // Pre-populate output with existing values and collect items that need translation
        var itemsToTranslate = new List<TranslateItem>(orderedKeys.Count);
        foreach (var key in orderedKeys) {
            var baseText = baseMap[key];
            var hasExisting = localeMap.TryGetValue(key, out var existingValue) && !string.IsNullOrWhiteSpace(existingValue);
            var needsTranslation = isRebuild || changedKeys.Contains(key) || !hasExisting;

            // Nothing to translate for empty source values; copy them as-is
            if (needsTranslation && string.IsNullOrWhiteSpace(baseText)) {
                output[key] = baseText;
                continue;
            }

            // default output is the existing translation if any, or empty until translated
            output[key] = hasExisting ? existingValue : string.Empty;

            if (needsTranslation) {
                itemsToTranslate.Add(new TranslateItem {
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = localeCode,
                    Key = key,
                    Text = baseText
                });
            }
        }

        // Batch translate items
        var effectiveBatchSize = Math.Max(1, batchSize);
        for (var i = 0; i < itemsToTranslate.Count; i += effectiveBatchSize) {
            var batch = itemsToTranslate.Skip(i).Take(effectiveBatchSize).ToArray();

            var promptOptions = BuildPromptOptionsForBatch(batch, prompt, extraPrompt);
            var results = await TranslateBatchWithRetryAsync(translator, promptOptions, localePath, i);

            // check results count
            if (results.Length != batch.Length)
                throw new Exception($"Translation result count mismatch for {localeFileName} at batch starting index {i}. Expected {batch.Length}, got {results.Length}.");

            foreach (var res in results) {
                // "*" means the AI skipped this item; keep the existing value, or fall back to the source text
                if (res.TranslatedText.Trim() == "*") {
                    if (string.IsNullOrWhiteSpace(output[res.Key]?.GetValue<string>()))
                        output[res.Key] = baseMap.GetValueOrDefault(res.Key, string.Empty);
                    continue;
                }

                // Post-process and apply
                var baseText = baseMap.GetValueOrDefault(res.Key, string.Empty);
                output[res.Key] = TranslateUtils.PostProcessTranslation(baseText, res.TranslatedText);
                translatedCount++;
            }

            if (isRebuild) {
                var done = Math.Min(i + batch.Length, itemsToTranslate.Count);
                Console.WriteLine($"  Progress: {done}/{itemsToTranslate.Count} ({done * 100 / itemsToTranslate.Count}%)");
            }
        }

        // Write JSON preserving base order
        await WriteJsonAsync(localePath, output);

        if (isRebuild)
            Console.WriteLine($"✓ {localeFileName}: Rebuilt with {translatedCount} translations.");
        else if (translatedCount > 0)
            Console.WriteLine($"✓ {localeFileName}: {translatedCount} translated/updated.");
        else
            Console.WriteLine($"  {localeFileName}: Up to date, no changes needed.");
    }

    private static PromptOptions BuildPromptOptionsForBatch(TranslateItem[] items, string prompt, string? extraPrompt)
    {
        var promptBuilder = new StringBuilder(prompt);

        if (!string.IsNullOrWhiteSpace(extraPrompt)) {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Additional guidelines:");
            promptBuilder.AppendLine(extraPrompt);
        }

        return new PromptOptions {
            Prompt = promptBuilder.ToString(),
            Items = items
        };
    }

    private static async Task<TranslateResult[]> TranslateBatchWithRetryAsync(
        ITranslator translator,
        PromptOptions promptOptions,
        string localePath,
        int batchStartIndex,
        int retryCount = 5)
    {
        var localeFileName = Path.GetFileName(localePath);

        for (var attempt = 1; attempt <= retryCount; attempt++) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTranslateTimeoutSeconds));
            try {
                await Task.Delay(500, CancellationToken.None); // brief pause to avoid rate limits
                return await translator.TranslateAsync(promptOptions, cts.Token);
            }
            catch (OperationCanceledException) {
                await Console.Error.WriteLineAsync($"Timeout while translating {localeFileName} batch starting at index {batchStartIndex} (attempt {attempt}/{retryCount}).");
            }
            catch (Exception ex) {
                await Console.Error.WriteLineAsync($"Error while translating {localeFileName} batch starting at index {batchStartIndex} (attempt {attempt}/{retryCount}): {ex.Message}");
            }

            // back off a bit more after each failed attempt to avoid rate limits
            await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), CancellationToken.None);
        }

        throw new Exception($"Failed to translate {localeFileName} batch starting at index {batchStartIndex} after {retryCount} attempts.");
    }

    private static bool TryLoadJsonObject(string path, out JsonObject? obj, out string? error)
    {
        try {
            var text = File.ReadAllText(path);
            var doc = JsonNode.Parse(text) as JsonObject;
            obj = doc ?? throw new Exception("Root is not a JSON object.");
            error = null;
            return true;
        }
        catch (Exception ex) {
            obj = null;
            error = ex.Message;
            return false;
        }
    }

    private static async Task WriteJsonAsync(string path, JsonObject obj)
    {
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, obj, OutputSerializerOptions);
    }

    private static Dictionary<string, string> ComputeMd5Hashes(Dictionary<string, string> map)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in map)
            result[key] = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value)));
        return result;
    }

    private static async Task<Dictionary<string, string>> LoadHashesAsync(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try {
            var txt = await File.ReadAllTextAsync(path);
            var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(txt) ?? new();
            return new Dictionary<string, string>(obj, StringComparer.Ordinal);
        }
        catch {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static async Task SaveHashesAsync(string path, Dictionary<string, string> hashes)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(hashes, options);
        await File.WriteAllTextAsync(path, txt);
    }

    private static HashSet<string> DetermineChangedKeys(IEnumerable<string> keys,
        Dictionary<string, string> currentMd5,
        Dictionary<string, string> previous)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys) {
            var cur = currentMd5.GetValueOrDefault(key);
            var prev = previous.GetValueOrDefault(key);
            if (!string.Equals(cur, prev, StringComparison.Ordinal))
                changed.Add(key);
        }
        return changed;
    }

    private static string GetPrivateFolderPath(string basePath)
    {
        var baseDir = Path.GetDirectoryName(basePath)!;
        return Path.Combine(baseDir, "vh_translator");
    }

    private static string GetCustomPromptFilePath(string basePath)
    {
        return Path.Combine(GetPrivateFolderPath(basePath), "custom_prompt.txt");
    }

    private static string GetHashesFilePath(string basePath)
    {
        // Location: <baseDir>/vh_translator/<baseLang>_watch.json
        var baseLang = Path.GetFileNameWithoutExtension(basePath);
        return Path.Combine(GetPrivateFolderPath(basePath), $"{baseLang}_watch.json");
    }
}
