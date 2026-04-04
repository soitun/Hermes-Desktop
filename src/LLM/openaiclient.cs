namespace Hermes.Agent.LLM;

using Hermes.Agent.Core;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

public sealed class OpenAiClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    
    public OpenAiClient(LlmConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
        
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }
    
    public async Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
    {
        var payload = new
        {
            model = _config.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = _config.Temperature,
            max_tokens = _config.MaxTokens,
        };
        
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_config.BaseUrl}/chat/completions", content, ct);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
    
    public IAsyncEnumerable<StreamEvent> StreamAsync(IEnumerable<Message> messages, CancellationToken ct = default)
        => StreamAsync(null, messages, null, ct);
    
    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        string? systemPrompt,
        IEnumerable<Message> messages,
        IEnumerable<ToolDefinition>? tools,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        
        var payload = BuildPayload(systemPrompt, messageList, tools, stream: true);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/chat/completions")
        {
            Content = content
        };
        
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        
        var toolUseBuilder = new Dictionary<string, (string Name, StringBuilder Json)>();
        var inputTokens = 0;
        var outputTokens = 0;
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            
            var data = line.Substring(6);
            if (data == "[DONE]") break;
            
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                continue;
            
            var choice = choices[0];
            var delta = choice.GetProperty("delta");
            
            // Handle content
            if (delta.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                var text = contentProp.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    yield return new StreamEvent.TokenDelta(text);
                }
            }
            
            // Handle tool calls (OpenAI format)
            if (delta.TryGetProperty("tool_calls", out var toolCalls))
            {
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var index = tc.GetProperty("index").GetInt32();
                    var id = tc.TryGetProperty("id", out var idProp) ? idProp.GetString() : $"tool_{index}";
                    var function = tc.GetProperty("function");
                    var name = function.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                    var args = function.TryGetProperty("arguments", out var argsProp) ? argsProp.GetString() : "";
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        toolUseBuilder[id!] = (name, new StringBuilder());
                        yield return new StreamEvent.ToolUseStart(id!, name);
                    }
                    
                    if (!string.IsNullOrEmpty(args) && toolUseBuilder.TryGetValue(id!, out var builder))
                    {
                        builder.Json.Append(args);
                        yield return new StreamEvent.ToolUseDelta(id!, args);
                    }
                }
            }
            
            // Handle finish reason
            if (choice.TryGetProperty("finish_reason", out var finishProp) && finishProp.ValueKind == JsonValueKind.String)
            {
                var finishReason = finishProp.GetString();
                
                // Complete any pending tool uses
                foreach (var (id, (name, jsonBuilder)) in toolUseBuilder)
                {
                    var fullJson = jsonBuilder.ToString();
                    StreamEvent toolEvt;
                    try
                    {
                        var args = JsonDocument.Parse(fullJson).RootElement;
                        toolEvt = new StreamEvent.ToolUseComplete(id, name, args.Clone());
                    }
                    catch (JsonException)
                    {
                        toolEvt = new StreamEvent.StreamError(new JsonException($"Invalid tool arguments: {fullJson}"));
                    }
                    yield return toolEvt;
                }
                
                // Get usage if available
                if (root.TryGetProperty("usage", out var usage))
                {
                    inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                    outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
                }
                
                yield return new StreamEvent.MessageComplete(
                    finishReason ?? "stop",
                    new UsageStats(inputTokens, outputTokens));
            }
        }
    }
    
    private object BuildPayload(string? systemPrompt, List<Message> messages, IEnumerable<ToolDefinition>? tools, bool stream)
    {
        var formattedMessages = new List<object>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            formattedMessages.Add(new { role = "system", content = systemPrompt });
        }
        
        foreach (var msg in messages)
        {
            formattedMessages.Add(new { role = msg.Role, content = msg.Content });
        }
        
        var payload = new Dictionary<string, object>
        {
            ["model"] = _config.Model,
            ["messages"] = formattedMessages,
            ["temperature"] = _config.Temperature,
            ["max_tokens"] = _config.MaxTokens,
        };
        
        if (stream)
        {
            payload["stream"] = true;
        }
        
        if (tools is not null)
        {
            payload["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.InputSchema
                }
            }).ToList();
        }
        
        return payload;
    }
}
