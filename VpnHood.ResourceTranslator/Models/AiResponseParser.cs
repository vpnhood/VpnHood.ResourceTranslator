using System.Text.Json;
using System.Text.Json.Nodes;
using VpnHood.ResourceTranslator.Translators;

namespace VpnHood.ResourceTranslator.Models;

internal static class AiResponseParser
{
    public static TranslateResult[] ParseResponse(string content)
    {
        try
        {
            // Clean the content - remove any Markdown formatting
            content = content.Trim();
            if (content.StartsWith("```json") && content.EndsWith("```"))
            {
                content = content.Substring(7, content.Length - 10).Trim();
            }
            else if (content.StartsWith("```") && content.EndsWith("```"))
            {
                content = content.Substring(3, content.Length - 6).Trim();
            }

            // Try to parse as JSON node first to inspect the structure
            var jsonNode = JsonNode.Parse(content);
            
            if (jsonNode is JsonArray jsonArray)
            {
                // Direct array - this is what we want
                return DeserializeArray(jsonArray);
            }
            else if (jsonNode is JsonObject jsonObject)
            {
                // Check for common wrapper patterns
                if (jsonObject.ContainsKey("result") && jsonObject["result"] is JsonArray resultArray)
                {
                    // Handle { "result": [...] } format
                    return DeserializeArray(resultArray);
                }
                else if (jsonObject.ContainsKey("results") && jsonObject["results"] is JsonArray resultsArray)
                {
                    // Handle { "results": [...] } format
                    return DeserializeArray(resultsArray);
                }
                else if (jsonObject.ContainsKey("translations") && jsonObject["translations"] is JsonArray translationsArray)
                {
                    // Handle { "translations": [...] } format
                    return DeserializeArray(translationsArray);
                }
                else if (jsonObject.ContainsKey("data") && jsonObject["data"] is JsonArray dataArray)
                {
                    // Handle { "data": [...] } format
                    return DeserializeArray(dataArray);
                }
                else
                {
                    // Single object - wrap it in an array
                    var singleResult = JsonSerializer.Deserialize<TranslateResult>(content);
                    if (singleResult != null)
                    {
                        return new[] { singleResult };
                    }
                }
            }

            throw new Exception("Unable to parse AI response structure");
        }
        catch (JsonException ex)
        {
            throw new Exception($"AI result JSON parsing failed: {ex.Message}. Content: {content}");
        }
        catch (Exception ex)
        {
            throw new Exception($"AI result processing failed: {ex.Message}. Content: {content}");
        }
    }

    private static TranslateResult[] DeserializeArray(JsonArray jsonArray)
    {
        var results = new List<TranslateResult>();
        
        foreach (var item in jsonArray)
        {
            if (item != null)
            {
                var result = JsonSerializer.Deserialize<TranslateResult>(item.ToString());
                if (result != null)
                {
                    results.Add(result);
                }
            }
        }

        if (results.Count == 0)
        {
            throw new Exception("No valid translation results found in array");
        }

        return results.ToArray();
    }
}