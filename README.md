# VpnHood Resource Translator

An intelligent i18n resource translator that uses Google's Gemini AI to automatically translate JSON localization files while preserving placeholders, HTML tags, and formatting.

## Features

- 🤖 **AI-Powered Translation** - Uses Google Gemini AI for high-quality translations
- 🔄 **Incremental Updates** - Only translates changed entries using hash-based tracking
- 🎯 **Smart Placeholder Preservation** - Keeps `{variables}`, HTML tags, and URLs intact
- 📁 **Batch Processing** - Translates multiple language files simultaneously
- 🔧 **Customizable Prompts** - Template-based prompts with custom instructions
- 📊 **Progress Tracking** - Real-time progress for large translation jobs
- 🛡️ **Robust Error Handling** - Automatic retries and detailed error messages

## Installation

### Prerequisites
- Google Gemini API key ([Get one here](https://makersuite.google.com/app/apikey))


## Quick Start
1. **Set your API key**:
   ```
   set GEMINI_API_KEY=your-api-key-here
   ```

2. **Basic translation**:
   ```bash
   VpnHood.ResourceTranslator -e locales/en.json
   ```

## Usage

### Command Line Options

```
VpnHood.ResourceTranslator [options]

Options:
  -e, --en <path>            Path to base en.json
  -x, --extra-prompt <path>  Path to extra instructions text file for the AI prompt
  -c, --show-changes         Show changed keys since last translation and exit
  -r, --rebuild-lang <code>  Force rebuild/translate all items for specific language
  -i, --ignore-changes       Rebuild hash file to mark all entries as current
  -k, --api-key <key>        Gemini API key (or set GEMINI_API_KEY env var)
  -m, --model <name>         Gemini model (default: gemini-1.5-flash)
  -h, --help                 Show help
```

### Examples

#### Basic Translation
Translate all locale files in the same directory as `en.json`:
```bash
VpnHood.ResourceTranslator -e locales/en.json
```

#### Force Rebuild a Language
Retranslate all entries for French, ignoring hash changes:
```bash
VpnHood.ResourceTranslator -e locales/en.json -r fr
```

#### Show What Changed
See which keys have changed since last translation:
```bash
VpnHood.ResourceTranslator -e locales/en.json -c
```

#### Use Custom Instructions
Add custom translation rules via a text file:
```bash
VpnHood.ResourceTranslator -e locales/en.json -x custom-rules.txt
```

#### Reset Hash State
Mark all entries as current without translating:
```bash
VpnHood.ResourceTranslator -e locales/en.json -i
```

#### Use Different Model
Use a different Gemini model:
```bash
VpnHood.ResourceTranslator -e locales/en.json -m gemini-1.5-pro
```

## File Structure

### Input Files

Your locale directory should look like this:
```
locales/
├── en.json              # Base English file (required)
├── fr.json              # French translations
├── es.json              # Spanish translations
├── de.json              # German translations
└── en.json.hashes.json  # Hash tracking file (auto-generated)
```

### Generated Files

- `en.json.hashes.json` - Tracks changes to detect what needs retranslation
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
1. Add new keys to `en.json`
2. Run translator: `VpnHood.ResourceTranslator -e locales/en.json`
3. Only new/changed keys get translated
4. Review and commit changes

### Setting Up New Language
1. Create empty `locales/it.json` file: `{}`
2. Run: `VpnHood.ResourceTranslator -e locales/en.json -r it`
3. All entries get translated for Italian

### Quality Control Workflow
1. Improve your `translation-prompt.txt`
2. Rebuild all languages: `VpnHood.ResourceTranslator -e locales/en.json -r fr`
3. Compare results and iterate

### Mixed Manual/Auto Workflow
1. Manually fix some translations in `fr.json`
2. Mark as current: `VpnHood.ResourceTranslator -e locales/en.json -i`
3. Add new keys to `en.json`
4. Run translator: only new keys get auto-translated

## Advanced Configuration

### Custom Prompt Template

Edit `translation-prompt.txt` to customize AI behavior:

```text
You are a professional translator specializing in mobile app localization.

CONTEXT: This is for a VPN application interface.

RULES:
- Use concise, user-friendly language
- Maintain consistent terminology across all strings
- Preserve all placeholders: {variable_name}
- Keep HTML tags intact: <span class="class">text</span>
- URLs and email addresses must remain unchanged
- Technical terms: VPN, IP, DNS, UDP, TCP should not be translated

TONE: Professional but approachable, suitable for general users.

OUTPUT: Return only the translated text, no quotes or explanations.
```

### Environment Variables

```bash
# Required
export GEMINI_API_KEY="your-api-key"

# Optional
export GEMINI_MODEL="gemini-1.5-pro"  # Override default model
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
VpnHood.ResourceTranslator -e locales/en.json -c

# 2. Translate changes
VpnHood.ResourceTranslator -e locales/en.json

# 3. Review and commit
git add locales/
git commit -m "Update translations"
```

## Troubleshooting

### Common Issues

**API Key Problems**
```
Error: Missing Gemini API key
```
- Set `GEMINI_API_KEY` environment variable
- Or use `-k` flag: `--api-key your-key-here`

**JSON Parse Errors**
```
Error: Failed to parse base JSON
```
- Check `en.json` for valid JSON syntax
- Use JSON validator: `jq . en.json`

**Missing Placeholders**
- The tool automatically appends missing `{placeholders}` to prevent runtime errors
- Review output if placeholders seem misplaced

### Rate Limiting
If you hit Gemini API rate limits:
- Use `gemini-1.5-flash` model (faster, cheaper)
- Process smaller batches with `-r` flag
- Add delays between requests (built-in retry logic)

### Quality Issues
- Improve `translation-prompt.txt` with specific instructions
- Use `custom-rules.txt` for domain-specific guidelines
- Review and manually edit problematic translations
- Use `-i` flag to prevent re-translation of manual fixes

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request
