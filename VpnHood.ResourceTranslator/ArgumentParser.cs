namespace VpnHood.ResourceTranslator;

internal class ArgumentParser
{
    public string? BasePath { get; set; }
    public string? ExtraPromptPath { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Engine { get; set; }
    public bool ShowChanges { get; set; }
    public string? RebuildLang { get; set; }
    public bool RebuildHashes { get; set; }
    public int BatchSize { get; set; } = 20;
    public bool ShowHelp { get; set; }

    public bool Parse(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-b":
                case "--base":
                    BasePath = GetArgValue(args, ref i);
                    break;
                case "-x":
                case "--extra-prompt":
                    ExtraPromptPath = GetArgValue(args, ref i);
                    break;
                case "-k":
                case "--api-key":
                    ApiKey = GetArgValue(args, ref i);
                    break;
                case "-m":
                case "--model":
                    Model = GetArgValue(args, ref i);
                    break;
                case "-e":
                case "--engine":
                    Engine = GetArgValue(args, ref i);
                    break;
                case "-c":
                case "--show-changes":
                    ShowChanges = true;
                    break;
                case "-r":
                case "--rebuild-lang":
                    RebuildLang = GetArgValue(args, ref i);
                    break;
                case "-i":
                case "--ignore-changes":
                    RebuildHashes = true;
                    break;
                case "-n":
                case "--batch":
                {
                    var val = GetArgValue(args, ref i);
                    if (int.TryParse(val, out var n) && n > 0)
                        BatchSize = n;
                    break;
                }
                case "-h":
                case "--help":
                    ShowHelp = true;
                    return true;
                default:
                    Console.WriteLine($"Unknown argument: {args[i]}");
                    return false;
            }
        }
        return true;
    }

    private static string GetArgValue(string[] args, ref int i)
    {
        if (i + 1 < args.Length) { return args[++i]; }
        return string.Empty;
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
        Console.WriteLine("  -k, --api-key <key>        API key (or set GEMINI_API_KEY/OPENAI_API_KEY/META_API_KEY/GROK_API_KEY env var)");
        Console.WriteLine("  -m, --model <name>         AI model (default: gemini-2.5-flash-lite)");
        Console.WriteLine("  -e, --engine <name>        Translation engine: gemini, gpt, meta, or grok (default: auto-detected from model)");
        Console.WriteLine("  -n, --batch <number>       Batch size for translation requests (default: 20)");
        Console.WriteLine("  -h, --help                 Show help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  vhtranslator -b locales/en.json");
        Console.WriteLine("  vhtranslator -b locales/en.json -m gpt-4o-mini");
        Console.WriteLine("  vhtranslator -b locales/en.json -m llama-3.1-70b-instruct");
        Console.WriteLine("  vhtranslator -b locales/en.json -m grok-4-latest");
        Console.WriteLine("  vhtranslator -b locales/fr.json -r es -e gemini");
        Console.WriteLine("  vhtranslator -b locales/en.json -x custom-prompt.txt -e meta -m llama-3.1-8b-instruct");
        Console.WriteLine("  vhtranslator -b locales/de.json -c");
        Console.WriteLine();
        Console.WriteLine("Engines:");
        Console.WriteLine("  gemini    - Google Gemini (requires GEMINI_API_KEY)");
        Console.WriteLine("  gpt       - OpenAI ChatGPT (requires OPENAI_API_KEY)");
        Console.WriteLine("  chatgpt   - Alias for gpt");
        Console.WriteLine("  meta      - Meta AI (requires META_API_KEY)");
        Console.WriteLine("  meta-ai   - Alias for meta");
        Console.WriteLine("  grok      - Grok AI (requires GROK_API_KEY)");
        Console.WriteLine("  grok-ai   - Alias for grok");
        Console.WriteLine("  x-ai      - Alias for grok");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  - Engine is auto-detected from model name if not specified");
        Console.WriteLine("  - Models containing 'gemini' use Gemini engine");
        Console.WriteLine("  - Models containing 'llama' or 'meta' use Meta AI engine");
        Console.WriteLine("  - Models containing 'grok' use Grok AI engine");
        Console.WriteLine("  - Other models use ChatGPT engine");
        Console.WriteLine("  - Any language can be used as the base source for translations");
        Console.WriteLine("  - Missing entries in target languages are always translated regardless of hash changes");
        Console.WriteLine("  - Use --ignore-changes after manual translations to avoid re-translating unchanged entries");
    }
}