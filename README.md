# VpnHood Resource Translator

An intelligent i18n resource translator that uses Google's Gemini AI to automatically translate JSON localization files while preserving placeholders, HTML tags, and formatting.

## Features

- 🤖 **AI-Powered Translation** - Uses Google Gemini AI for high-quality translations
- 🔄 **Incremental Updates** - Only translates changed entries using hash-based tracking
- 🎯 **Smart Placeholder Preservation** - Keeps `{variables}`, HTML tags, and URLs intact
- 📁 **Batch Processing** - Translates multiple language files simultaneously
- 🌐 **Flexible Source Language** - Use any language as the base source (not just English)
- 🔧 **Customizable Prompts** - Template-based prompts with custom instructions
- 📊 **Progress Tracking** - Real-time progress for large translation jobs
- 🛡️ **Robust Error Handling** - Automatic retries and detailed error messages

## Installation

### Prerequisites
- .NET 8.0 or later
- Google Gemini API key ([Get one here](https://makersuite.google.com/app/apikey))

## Quick Start

1. **Set your API key**:
   ```bash
   export GEMINI_API_KEY="your-api-key-here"
   # or on Windows:
   set GEMINI_API_KEY=your-api-key-here
   ```

2. **Basic translation**:
   ```bash
   vhtranslate -b locales/en.json
   ```

## Usage

### Command Line Options

```
vhtranslate [options]

Options:
  -b, --base <path>          Path to base language file (e.g., en.json, fr.json, de.json)
  -x, --extra-prompt <path>  Path to extra instructions text file for the AI prompt
  -c, --show-changes         Show changed keys since last translation and exit
  -r, --rebuild-lang <code>  Force rebuild/translate all items for specific language
  -i, --ignore-changes       Rebuild hash file to mark all entries as current
  -k, --api-key <key>        Gemini API key (or set GEMINI_API_KEY env var)
  -m, --model <name>         Gemini model (default: gemini-2.5-flash-lite)
  -n, --batch <number>       Batch size for translation requests (default: 20)
  -h, --help                 Show help
```

### Examples

#### Basic Translation (English as Source)
Translate all locale files using English as the base:
```bash
vhtranslate -b locales/en.json
```

#### Use French as Source Language
Translate from French to other languages:
```bash
vhtranslate -b locales/fr.json
```

#### Force Rebuild a Language
Retranslate all entries for Spanish from English base:
```bash
vhtranslate -b locales/en.json -r es
```

#### Cross-Language Translation
Translate from German to Spanish:
```bash
vhtranslate -b locales/de.json -r es
```

#### Show What Changed
See which keys have changed since last translation:
```bash
vhtranslate -b locales/en.json -c
```

#### Use Custom Instructions
Add custom translation rules via a text file:
```bash
vhtranslate -b locales/en.json -x custom-rules.txt
```

#### Reset Hash State
Mark all entries as current without translating:
```bash
vhtranslate -b locales/en.json -i
```

#### Use Different Model
Use a different Gemini model:
```bash
vhtranslate -b locales/en.json -m gemini-1.5-pro
```

#### Skip Specific Translations
Create custom rules to skip certain translations:
```bash
# Create custom-rules.txt with skip rules
echo "For Chinese: Return '*' for any key containing 'PRIVACY' or 'LEGAL'" > custom-rules.txt
vhtranslate -b locales/en.json -x custom-rules.txt
```

## File Structure

### Input Files

Your locale directory can have any language as the base:

**English as Base:**
```
locales/
├── en.json              # Base English file
├── fr.json              # French translations
├── es.json              # Spanish translations
├── de.json              # German translations
└── en.json.hashes.json  # Hash tracking file (auto-generated)
```

**French as Base:**
```
locales/
├── fr.json              # Base French file
├── en.json              # English translations
├── es.json              # Spanish translations
├── de.json              # German translations
└── fr.json.hashes.json  # Hash tracking file (auto-generated)
```

### Generated Files

- `{base}.json.hashes.json` - Tracks changes to detect what needs retranslation
- `translation-prompt.txt` - Default AI prompt template (customizable)

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

### custom-rules.txt (Custom Instructions)
```text
Translation Guidelines:
- Keep technical terms like "VPN", "API", "JSON" untranslated
- Use formal tone for German translations
- For Spanish, use Latin American variants
- Brand name "VpnHood" should always remain unchanged
- Use gender-neutral language where possible
```

### translation-prompt.txt (AI Prompt Template)
```text
You are an expert app localization system.
Task: Translate the given string from the source language to the target language.
Rules:
- Preserve placeholders exactly as-is: tokens in curly braces like {x}, {name}, {minutes}.
- Preserve HTML tags, entities, and attributes exactly (do not translate tags or attribute names).
- Preserve punctuation, Markdown, and capitalization style when appropriate.
- Keep URLs and domains unchanged.
- Return ONLY the translated string without quotes or extra commentary.
- If the text is already a URL or looks untranslatable, return it unchanged.
```

## Workflow Examples

### Daily Development Workflow
1. Add new keys to your base language file (e.g., `en.json`)
2. Run translator: `vhtranslate -b locales/en.json`
3. Only new/changed keys get translated
4. Review and commit changes

### Setting Up New Language
1. Run: `vhtranslate -b locales/en.json -r it`
2. All entries get translated for Italian

### Cross-Language Translation
1. Translate from French to Spanish: `vhtranslate -b locales/fr.json -r es`
2. Use German as source for Italian: `vhtranslate -b locales/de.json -r it`
3. Mix and match source languages as needed

### Quality Control Workflow
1. Improve your `translation-prompt.txt`
2. Rebuild all languages: `vhtranslate -b locales/en.json -r fr`
3. Compare results and iterate

### Mixed Manual/Auto Workflow
1. Manually fix some translations in `fr.json`
2. Mark as current: `vhtranslate -b locales/en.json -i`
3. Add new keys to base language file
4. Run translator: only new keys get auto-translated

### Selective Translation Workflow
1. Create `skip-rules.txt` with language-specific skip rules:
   ```text
   For Chinese market: Skip "GOOGLE_PLAY_LINK" keys by returning "*"
   For Arabic: Skip "FACEBOOK_SHARE" by returning "*" 
   For Japanese: Skip keys containing "WESTERN" by returning "*"
   ```
2. Run: `vhtranslate -b locales/en.json -x skip-rules.txt`
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

**custom-rules.txt example:**
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
# Required
export GEMINI_API_KEY="your-api-key"

# Optional
export GEMINI_MODEL="gemini-2.5-flash-lite"  # Override default model
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
  "GREETING": "Hello, {username}!",           
  "MESSAGE": "You have {count} new messages"
}
```

### 3. HTML in Translations
```json
{
  "TERMS": "I agree to the <a href=\"/terms\">Terms of Service</a>",
  "HIGHLIGHT": "This is <strong>important</strong> text"
}
```

### 4. Regular Workflow
```bash
# 1. Check what changed
vhtranslate -b locales/en.json -c

# 2. Translate changes
vhtranslate -b locales/en.json

# 3. Review and commit
git add locales/
git commit -m "Update translations"
```

### 5. Multi-Source Workflows
```bash
# Use English as primary source
vhtranslate -b locales/en.json

# But translate French to German directly for better accuracy
vhtranslate -b locales/fr.json -r de

# Or use Spanish as source for Portuguese
vhtranslate -b locales/es.json -r pt
```

## Troubleshooting

### Common Issues

**API Key Problems**
```bash
Error: Missing Gemini API key
```
- Set `GEMINI_API_KEY` environment variable
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
If you hit Gemini API rate limits:
- Use `gemini-2.5-flash-lite` model (fast, cost-effective)
- Process smaller batches with `-r` flag
- Add delays between requests (built-in retry logic)

### Quality Issues
- Improve `translation-prompt.txt` with specific instructions
- Use `custom-rules.txt` for domain-specific guidelines
- Review and manually edit problematic translations
- Use `-i` flag to prevent re-translation of manual fixes
