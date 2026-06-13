namespace VpnHood.ResourceTranslator;

internal class ArgumentParser
{
    public string? BasePath { get; private set; }
    public string? ExtraPromptPath { get; private set; }
    public string? ApiKey { get; private set; }
    public string? Model { get; private set; }
    public string? Engine { get; private set; }
    public bool ShowChanges { get; private set; }
    public string? RebuildLang { get; private set; }
    public bool RebuildHashes { get; private set; }
    public int BatchSize { get; private set; } = 20;
    public bool ShowHelp { get; private set; }

    public bool Parse(string[] args)
    {
        for (var i = 0; i < args.Length; i++) {
            switch (args[i]) {
                case "-b":
                case "--base":
                    if (!TryGetArgValue(args, ref i, out var basePath)) return false;
                    BasePath = basePath;
                    break;
                case "-x":
                case "--extra-prompt":
                    if (!TryGetArgValue(args, ref i, out var extraPromptPath)) return false;
                    ExtraPromptPath = extraPromptPath;
                    break;
                case "-k":
                case "--api-key":
                    if (!TryGetArgValue(args, ref i, out var apiKey)) return false;
                    ApiKey = apiKey;
                    break;
                case "-m":
                case "--model":
                    if (!TryGetArgValue(args, ref i, out var model)) return false;
                    Model = model;
                    break;
                case "-e":
                case "--engine":
                    if (!TryGetArgValue(args, ref i, out var engine)) return false;
                    Engine = engine;
                    break;
                case "-c":
                case "--show-changes":
                    ShowChanges = true;
                    break;
                case "-r":
                case "--rebuild-lang":
                    if (!TryGetArgValue(args, ref i, out var rebuildLang)) return false;
                    RebuildLang = rebuildLang;
                    break;
                case "-i":
                case "--ignore-changes":
                    RebuildHashes = true;
                    break;
                case "-n":
                case "--batch":
                    if (!TryGetArgValue(args, ref i, out var batchValue)) return false;
                    if (!int.TryParse(batchValue, out var batchSize) || batchSize <= 0) {
                        Console.Error.WriteLine($"Invalid batch size: {batchValue}. It must be a positive number.");
                        return false;
                    }
                    BatchSize = batchSize;
                    break;
                case "-h":
                case "--help":
                    ShowHelp = true;
                    return true;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return false;
            }
        }
        return true;
    }

    private static bool TryGetArgValue(string[] args, ref int i, out string value)
    {
        if (i + 1 < args.Length) {
            value = args[++i];
            return true;
        }

        Console.Error.WriteLine($"Missing value for argument: {args[i]}");
        value = string.Empty;
        return false;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Resource Translator");
        Console.WriteLine("Usage: vhtranslator [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -b, --base <path>          Path to base language file (e.g., en.json, fr.json, de.json)");
        Console.WriteLine("  -x, --extra-prompt <path>  Path to extra instructions text file for the AI prompt");
        Console.WriteLine("  -c, --show-changes         Show changed keys since last translation and exit");
        Console.WriteLine("  -r, --rebuild-lang <code>  Force rebuild/translate all items for specific language (e.g., 'fr', 'es')");
        Console.WriteLine("  -i, --ignore-changes       Rebuild hash file to mark all entries as current (no translation)");
        Console.WriteLine("  -k, --api-key <key>        API key (or set GEMINI_API_KEY/OPENAI_API_KEY/GROK_API_KEY env var)");
        Console.WriteLine("  -m, --model <name>         AI model (default: gemini-flash-lite-latest, grok-4-latest for grok engine)");
        Console.WriteLine("  -e, --engine <name>        Translation engine: gemini, gpt, or grok (default: auto-detected from model)");
        Console.WriteLine("  -n, --batch <number>       Batch size for translation requests (default: 20)");
        Console.WriteLine("  -h, --help                 Show help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  vhtranslator -b locales/en.json");
        Console.WriteLine("  vhtranslator -b locales/en.json -m gpt-4o-mini");
        Console.WriteLine("  vhtranslator -b locales/en.json -m grok-4-latest");
        Console.WriteLine("  vhtranslator -b locales/fr.json -r es -e gemini");
        Console.WriteLine("  vhtranslator -b locales/en.json -x custom-prompt.txt -e gpt -m gpt-4");
        Console.WriteLine("  vhtranslator -b locales/de.json -c");
        Console.WriteLine();
        Console.WriteLine("Engines:");
        Console.WriteLine("  gemini    - Google Gemini (requires GEMINI_API_KEY)");
        Console.WriteLine("  gpt       - OpenAI ChatGPT (requires OPENAI_API_KEY)");
        Console.WriteLine("  chatgpt   - Alias for gpt");
        Console.WriteLine("  grok      - Grok AI (requires GROK_API_KEY)");
        Console.WriteLine("  grok-ai   - Alias for grok");
        Console.WriteLine("  x-ai      - Alias for grok");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  - Engine is auto-detected from model name if not specified");
        Console.WriteLine("  - Models containing 'gemini' use Gemini engine");
        Console.WriteLine("  - Models containing 'grok' use Grok AI engine");
        Console.WriteLine("  - Other models use ChatGPT engine");
        Console.WriteLine("  - Any language can be used as the base source for translations");
        Console.WriteLine("  - Missing entries in target languages are always translated regardless of hash changes");
        Console.WriteLine("  - Use --ignore-changes after manual translations to avoid re-translating unchanged entries");
    }
}
