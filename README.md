# VpnHood Resource Translator

An intelligent i18n resource translator that uses AI (Google Gemini, OpenAI ChatGPT, or Grok AI) to automatically translate JSON localization files while preserving placeholders, HTML tags, and formatting.

## Features

- 🤖 **Multi-Engine AI Translation** - Supports Google Gemini, OpenAI ChatGPT, and Grok AI with smart engine detection
- 🔄 **Incremental Updates** - Only translates changed entries using hash-based tracking
- 🎯 **Smart Placeholder Preservation** - Keeps `{variables}`, HTML tags, and URLs intact
- 📁 **Batch Processing** - Translates multiple language files simultaneously
- 🌐 **Flexible Source Language** - Use any language as the base source (not just English)
- 🔧 **Customizable Prompts** - Template-based prompts with custom instructions
- 📊 **Progress Tracking** - Real-time progress for large translation jobs
- 🛡️ **Robust Error Handling** - Automatic retries and detailed error messages
- 🧠 **Intelligent Engine Detection** - Automatically selects the right AI engine based on model name

## Installation

### Prerequisites
- .NET 8.0 or later
- API key for your preferred service:
  - Google Gemini API key ([Get one here](https://makersuite.google.com/app/apikey))
  - OpenAI API key ([Get one here](https://platform.openai.com/api-keys))
  - Grok AI API key ([Get one here](https://console.x.ai/))

### Build from Source
```bash
git clone https://github.com/your-repo/VpnHood.ResourceTranslator.git
cd VpnHood.ResourceTranslator
dotnet build --configuration Release
```

## Quick Start

1. **Set your API key**:
   ```bash
   # For Gemini (default)
   export GEMINI_API_KEY="your-gemini-api-key-here"
   
   # For OpenAI ChatGPT
   export OPENAI_API_KEY="your-openai-api-key-here"
   
   # For Grok AI
   export GROK_API_KEY="your-grok-api-key-here"
   
   # On Windows:
   set GEMINI_API_KEY=your-gemini-api-key-here
   set OPENAI_API_KEY=your-openai-api-key-here
   set GROK_API_KEY=your-grok-api-key-here
   ```

2. **Basic translation** (uses Gemini by default):
   ```bash
   vhtranslator -b locales/en.json
   ```

3. **Use other AI engines**:
   ```bash
   # ChatGPT
   vhtranslator -b locales/en.json -m gpt-4o-mini
   
   # Grok AI
   vhtranslator -b locales/en.json -m grok-4-latest
   ```

## Engine and Model Selection

The translator features **intelligent engine detection**:

- **Auto-detection**: If no engine is specified, it's automatically detected from the model name
- **Gemini models**: Any model containing "gemini" uses the Gemini engine
- **ChatGPT models**: Models like gpt-4, gpt-3.5-turbo, etc. use the ChatGPT engine
- **Grok AI models**: Models containing "grok" use the Grok AI engine
- **Manual override**: Use `-e` to explicitly specify the engine

### Examples:
```bash
# Auto-detects Gemini engine
vhtranslator -b locales/en.json -m gemini-2.5-flash

# Auto-detects ChatGPT engine  
vhtranslator -b locales/en.json -m gpt-4o-mini

# Auto-detects Grok AI engine
vhtranslator -b locales/en.json -m grok-4-latest

# Explicitly specify engine
vhtranslator -b locales/en.json -e grok -m grok-4-latest

# Default behavior (Gemini)
vhtranslator -b locales/en.json
```

## Usage

### Command Line Options

```
vhtranslator [options]

Options:
  -b, --base <path>          Path to base language file (e.g., en.json, fr.json, de.json)
  -x, --extra-prompt <path>  Path to extra instructions text file for the AI prompt
  -c, --show-changes         Show changed keys since last translation and exit
  -r, --rebuild-lang <code>  Force rebuild/translate all items for specific language
  -i, --ignore-changes       Rebuild hash file to mark all entries as current
  -k, --api-key <key>        API key (or set GEMINI_API_KEY/OPENAI_API_KEY/GROK_API_KEY env var)
  -m, --model <name>         AI model (default: gemini-2.5-flash-lite)
  -e, --engine <name>        Translation engine: gemini, gpt, or grok (default: auto-detected)
  -n, --batch <number>       Batch size for translation requests (default: 20)
  -h, --help                 Show help
```

### Examples

#### Basic Translation with Different Engines
```bash
# Use Gemini (default)
vhtranslator -b locales/en.json

# Use ChatGPT with auto-detection
vhtranslator -b locales/en.json -m gpt-4o-mini

# Use Grok AI with auto-detection
vhtranslator -b locales/en.json -m grok-4-latest

# Use specific Gemini model
vhtranslator -b locales/en.json -m gemini-2.5-pro

# Explicitly specify engine
vhtranslator -b locales/en.json -e grok -m grok-4-latest
```

#### Advanced Usage
```bash
# Use French as source language with Grok AI
vhtranslator -b locales/fr.json -m grok-4-latest

# Force rebuild Spanish with Grok AI
vhtranslator -b locales/en.json -r es -e grok -m grok-4-latest

# Use custom instructions with ChatGPT
vhtranslator -b locales/en.json -x custom-prompt.txt -m gpt-4

# Show what changed since last translation
vhtranslator -b locales/en.json -c
```

## File Structure

### Input Files

Your locale directory can have any language as the base:

**English as Base:**
```
locales/
+-- en.json              # Base English file
+-- fr.json              # French translations
+-- es.json              # Spanish translations
+-- de.json              # German translations
+-- vh_translator/
    +-- en_watch.json    # Hash tracking file (auto-generated)
```

**French as Base:**
```
locales/
+-- fr.json              # Base French file
+-- en.json              # English translations
+-- es.json              # Spanish translations
+-- de.json              # German translations
+-- vh_translator/
    +-- fr_watch.json    # Hash tracking file (auto-generated)
```

### Generated Files

- `vh_translator/<base>_watch.json` - Tracks changes to detect what needs retranslation

## Sample Files

### en.json (Base File)
```json
{
  "WELCOME_MESSAGE": "Welcome to our application!",
  "USER_GREETING": "Hello, {username}!",
  "ITEM_COUNT": "You have {count} items in your cart.",
  "SETTINGS": "Settings",
  "SAVE_BUTTON": "Save Changes",
  "ERROR_NETWORK": "Network connection failed. Please try again.",
  "PRIVACY_POLICY_URL": "https://example.com/privacy",
  "COLORED_TEXT": "This is <span class=\"highlight\">important</span> information.",
  "EMAIL_LINK": "Contact us at <a href=\"mailto:support@example.com\">support@example.com</a>"
}
```

### fr.json (Generated Translation)
```json
{
  "WELCOME_MESSAGE": "Bienvenue dans notre application !",
  "USER_GREETING": "Bonjour, {username} !",
  "ITEM_COUNT": "Vous avez {count} articles dans votre panier.",
  "SETTINGS": "Paramètres",
  "SAVE_BUTTON": "Enregistrer les modifications",
  "ERROR_NETWORK": "La connexion réseau a échoué. Veuillez réessayer.",
  "PRIVACY_POLICY_URL": "https://example.com/privacy",
  "COLORED_TEXT": "Ceci est une information <span class=\"highlight\">importante</span>.",
  "EMAIL_LINK": "Contactez-nous à <a href=\"mailto:support@example.com\">support@example.com</a>"
}
```

### custom-prompt.txt (Custom Instructions)
```text
Translation Guidelines:
- Keep technical terms like "VPN", "API", "JSON" untranslated
- Use formal tone for German translations
- For Spanish, use Latin American variants
- Brand name "VpnHood" should always remain unchanged
- Use gender-neutral language where possible
```

## Workflow Examples

### Daily Development Workflow
1. Add new keys to your base language file (e.g., `en.json`)
2. Run translator: `vhtranslator -b locales/en.json`
3. Only new/changed keys get translated
4. Review and commit changes

### Setting Up New Language
1. Create empty `locales/it.json` file: `{}` 
2. Run: `vhtranslator -b locales/en.json -r it`
3. All entries get translated for Italian

### Cross-Language Translation
1. Translate from French to Spanish: `vhtranslator -b locales/fr.json -r es`
2. Use German as source for Italian: `vhtranslator -b locales/de.json -r it`
3. Mix and match source languages as needed

### Quality Control Workflow
1. Improve your `translation-prompt.txt`
2. Rebuild all languages: `vhtranslator -b locales/en.json -r fr`
3. Compare results and iterate

### Mixed Manual/Auto Workflow
1. Manually fix some translations in `fr.json`
2. Mark as current: `vhtranslator -b locales/en.json -i`
3. Add new keys to base language file
4. Run translator: only new keys get auto-translated

### Selective Translation Workflow
1. Create `skip-rules.txt` with language-specific skip rules:
   ```text
   For Chinese market: Skip "GOOGLE_PLAY_LINK" keys by returning "*"
   For Arabic: Skip "FACEBOOK_SHARE" by returning "*" 
   For Japanese: Skip keys containing "WESTERN" by returning "*"
   ```
2. Run: `vhtranslator -b locales/en.json -x skip-rules.txt`
3. Certain keys remain untranslated based on cultural/regional appropriateness
4. Manually handle skipped keys if needed

## Advanced Configuration

### Skip Translation Feature

The translator supports selective translation skipping. If the AI returns "*" as the translation, the tool will skip translating that specific key for that language and keep the existing value (or use the source text if missing).

This is useful for:
- Language-specific terms that shouldn't be translated
- Cultural references that don't apply to certain regions
- Technical terms that should remain in the source language

### Using Key Information in Custom Prompts

The translator provides the key name to the AI, allowing you to create key-specific translation rules:

**custom-prompt.txt example:**
```text
Translation Guidelines:
- Keep technical terms like "VPN", "API", "JSON" untranslated
- Brand name "VpnHood" should always remain unchanged
- Use formal tone for German translations
- For Spanish, use Latin American variants

Key-Specific Rules:
- PRIVACY_POLICY_URL: Return "*" for Chinese to skip translation
- TECHNICAL_SUPPORT_EMAIL: Return "*" for all languages to keep original
- APP_VERSION_INFO: Return "*" for Japanese, Korean to avoid translation
- If key contains "DEBUG" or "DEVELOPER": Return "*" for all non-English languages
```

### Environment Variables

```bash
# Required (choose one based on your preferred engine)
export GEMINI_API_KEY="your-gemini-api-key"
export OPENAI_API_KEY="your-openai-api-key" 
export GROK_API_KEY="your-grok-api-key"

# Optional
export GEMINI_MODEL="gemini-2.5-flash"  # Override default model
```

## Best Practices

### 1. Key Naming
```json
{
  "SECTION_TITLE": "Good - descriptive and clear",
  "btn1": "Avoid - unclear purpose",
  "USER_PROFILE_EDIT_BUTTON": "Good - specific and descriptive"
}
```

### 2. Placeholder Usage
```json
{
  "GREETING": "Hello, {username}!",           // ✅ Good
  "MESSAGE": "You have {count} new messages", // ✅ Good
  "WELCOME": "Welcome {0}!"                   // ❌ Avoid numbered placeholders
}
```

### 3. HTML in Translations
```json
{
  "TERMS": "I agree to the <a href=\"/terms\">Terms of Service</a>",  // ✅ Good
  "HIGHLIGHT": "This is <strong>important</strong> text"              // ✅ Good
}
```

### 4. Regular Workflow
```bash
# 1. Check what changed
vhtranslator -b locales/en.json -c

# 2. Translate changes
vhtranslator -b locales/en.json

# 3. Review and commit
git add locales/
git commit -m "Update translations"
```

### 5. Multi-Source Workflows
```bash
# Use English as primary source
vhtranslator -b locales/en.json

# But translate French to German directly for better accuracy
vhtranslator -b locales/fr.json -r de

# Or use Spanish as source for Portuguese
vhtranslator -b locales/es.json -r pt
```

## Troubleshooting

### Common Issues

**API Key Problems**
```bash
Error: Missing Gemini API key
Error: Missing Grok API key
```
- Set appropriate environment variable: `GEMINI_API_KEY`, `OPENAI_API_KEY`, or `GROK_API_KEY`
- Or use `-k` flag: `--api-key your-key-here`

**JSON Parse Errors**
```bash
Error: Failed to parse base JSON
```
- Check your base file for valid JSON syntax
- Use JSON validator: `jq . your-base-file.json`

**Missing Placeholders**
- The tool automatically appends missing `{placeholders}` to prevent runtime errors
- Review output if placeholders seem misplaced

### Rate Limiting
If you hit API rate limits:
- **Gemini**: Use `gemini-2.5-flash-lite` model (faster, cheaper)
- **ChatGPT**: Use `gpt-3.5-turbo` for faster, cheaper requests
- **Grok**: Monitor usage and implement delays if needed
- Add delays between requests (built-in retry logic)

**Engine-Specific Notes**
- **Grok AI**: Check that your X.AI account has API access enabled
- **API Endpoints**: The tool uses standard OpenAI-compatible endpoints for supported engines
