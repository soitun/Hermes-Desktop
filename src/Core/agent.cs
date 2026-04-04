namespace Hermes.Agent.Core;

using Hermes.Agent.LLM;
using Hermes.Agent.Permissions;
using Hermes.Agent.Security;
using Hermes.Agent.Transcript;
using Hermes.Agent.Memory;
using Hermes.Agent.Context;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class Agent : IAgent
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<Agent> _logger;
    private readonly Dictionary<string, ITool> _tools = new();

    // Optional subsystem dependencies — Agent works without any of these
    private readonly PermissionManager? _permissions;
    private readonly TranscriptStore? _transcripts;
    private readonly MemoryManager? _memories;
    private readonly ContextManager? _contextManager;

    /// <summary>Safety limit to prevent infinite tool loops.</summary>
    public int MaxToolIterations { get; set; } = 25;

    /// <summary>
    /// Optional callback for interactive permission prompts.
    /// When PermissionBehavior.Ask is returned, this callback is invoked with (toolName, message).
    /// Returns true to allow, false to deny. If null, Ask defaults to deny.
    /// </summary>
    public Func<string, string, Task<bool>>? PermissionPromptCallback { get; set; }

    public Agent(
        IChatClient chatClient,
        ILogger<Agent> logger,
        PermissionManager? permissions = null,
        TranscriptStore? transcripts = null,
        MemoryManager? memories = null,
        ContextManager? contextManager = null)
    {
        _chatClient = chatClient;
        _logger = logger;
        _permissions = permissions;
        _transcripts = transcripts;
        _memories = memories;
        _contextManager = contextManager;
    }

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
    }

    public IReadOnlyDictionary<string, ITool> Tools => _tools;

    /// <summary>Build ToolDefinition list from registered tools for the LLM.</summary>
    public List<ToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = BuildParameterSchema(t)
        }).ToList();
    }

    /// <summary>
    /// Full chat loop with tool calling. Sends the user message, then iterates:
    /// LLM responds -> if tool calls, execute them -> feed results back -> repeat
    /// until LLM produces a final text response or we hit MaxToolIterations.
    /// </summary>
    public async Task<string> ChatAsync(string message, Session session, CancellationToken ct)
    {
        // ── Memory injection ──
        // Load relevant memories and inject as a system message at the start
        if (_memories is not null)
        {
            try
            {
                var recentTools = _tools.Keys.Take(10).ToList();
                var relevantMemories = await _memories.LoadRelevantMemoriesAsync(message, recentTools, ct);
                if (relevantMemories.Count > 0)
                {
                    var memoryBlock = string.Join("\n---\n",
                        relevantMemories.Select(m => $"[{m.Type}] {m.Filename}:\n{m.Content}"));
                    session.Messages.Insert(0, new Message
                    {
                        Role = "system",
                        Content = $"[Relevant Memories]\n{memoryBlock}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load memories, continuing without them");
            }
        }

        // ── Add user message ──
        var userMessage = new Message { Role = "user", Content = message };
        session.AddMessage(userMessage);
        if (_transcripts is not null)
            await _transcripts.SaveMessageAsync(session.Id, userMessage, ct);

        _logger.LogInformation("Processing message for session {SessionId}", session.Id);

        // ── Context manager integration ──
        // If ContextManager is available, use it to build optimized context instead of raw session.Messages
        List<Message>? preparedContext = null;
        if (_contextManager is not null)
        {
            try
            {
                preparedContext = await _contextManager.PrepareContextAsync(
                    session.Id, message, retrievedContext: null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ContextManager failed, falling back to raw session messages");
            }
        }

        if (_tools.Count == 0)
        {
            // No tools registered — simple completion
            var messagesToSend = preparedContext ?? session.Messages;
            var response = await _chatClient.CompleteAsync(messagesToSend, ct);
            var assistantMsg = new Message { Role = "assistant", Content = response };
            session.AddMessage(assistantMsg);
            if (_transcripts is not null)
                await _transcripts.SaveMessageAsync(session.Id, assistantMsg, ct);
            if (_contextManager is not null)
                await _contextManager.UpdateAfterResponseAsync(session.Id, ct: ct);
            return response;
        }

        var toolDefs = GetToolDefinitions();
        var iterations = 0;

        while (iterations < MaxToolIterations)
        {
            iterations++;

            // Use prepared context for first iteration, then fall back to session.Messages
            // because session.Messages accumulates tool results as the loop progresses
            var messagesToUse = (iterations == 1 && preparedContext is not null)
                ? preparedContext
                : session.Messages;

            var response = await _chatClient.CompleteWithToolsAsync(messagesToUse, toolDefs, ct);

            if (!response.HasToolCalls)
            {
                // LLM is done — return final text
                var finalContent = response.Content ?? "";
                var finalMsg = new Message { Role = "assistant", Content = finalContent };
                session.AddMessage(finalMsg);
                if (_transcripts is not null)
                    await _transcripts.SaveMessageAsync(session.Id, finalMsg, ct);
                if (_contextManager is not null)
                    await _contextManager.UpdateAfterResponseAsync(session.Id, ct: ct);
                return finalContent;
            }

            // Record the assistant message with its tool call requests
            var assistantToolMsg = new Message
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = response.ToolCalls
            };
            session.AddMessage(assistantToolMsg);
            if (_transcripts is not null)
                await _transcripts.SaveMessageAsync(session.Id, assistantToolMsg, ct);

            // Execute each tool call and append results
            foreach (var toolCall in response.ToolCalls!)
            {
                // ── Permission gate ──
                if (_permissions is not null)
                {
                    try
                    {
                        var decision = await _permissions.CheckPermissionsAsync(
                            toolCall.Name, toolCall.Arguments, ct);

                        if (decision.Behavior == PermissionBehavior.Deny)
                        {
                            var denialMsg = new Message
                            {
                                Role = "tool",
                                Content = $"Permission denied: {decision.DecisionReason ?? decision.Message ?? "Blocked by permission rule"}",
                                ToolCallId = toolCall.Id,
                                ToolName = toolCall.Name
                            };
                            session.AddMessage(denialMsg);
                            if (_transcripts is not null)
                                await _transcripts.SaveMessageAsync(session.Id, denialMsg, ct);
                            continue;
                        }

                        if (decision.Behavior == PermissionBehavior.Ask)
                        {
                            var permissionMessage = decision.Message ?? "This operation requires permission";
                            bool allowed = false;

                            if (PermissionPromptCallback is not null)
                            {
                                try
                                {
                                    allowed = await PermissionPromptCallback(toolCall.Name, permissionMessage);
                                }
                                catch (Exception promptEx)
                                {
                                    _logger.LogWarning(promptEx, "Permission prompt callback failed, denying");
                                }
                            }

                            if (!allowed)
                            {
                                var askMsg = new Message
                                {
                                    Role = "tool",
                                    Content = $"Permission denied by user: {permissionMessage}",
                                    ToolCallId = toolCall.Id,
                                    ToolName = toolCall.Name
                                };
                                session.AddMessage(askMsg);
                                if (_transcripts is not null)
                                    await _transcripts.SaveMessageAsync(session.Id, askMsg, ct);
                                continue;
                            }
                            // User approved — fall through to execute the tool
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Permission check failed for {ToolName}, allowing execution", toolCall.Name);
                    }
                }

                _logger.LogInformation("Executing tool {ToolName} (call {CallId})", toolCall.Name, toolCall.Id);
                var result = await ExecuteToolCallAsync(toolCall, ct);

                // ── Secret exfiltration scan ──
                var resultContent = result.Content;
                if (SecretScanner.ContainsSecrets(resultContent))
                {
                    _logger.LogWarning("Secrets detected in tool result from {ToolName}, redacting", toolCall.Name);
                    resultContent = SecretScanner.RedactSecrets(resultContent);
                }

                var toolResultMsg = new Message
                {
                    Role = "tool",
                    Content = resultContent,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name
                };
                session.AddMessage(toolResultMsg);
                if (_transcripts is not null)
                    await _transcripts.SaveMessageAsync(session.Id, toolResultMsg, ct);
            }
        }

        _logger.LogWarning("Hit max tool iterations ({Max}) for session {SessionId}", MaxToolIterations, session.Id);
        var fallback = "I've reached the maximum number of tool call iterations. Here's what I've accomplished so far based on the conversation above.";
        var fallbackMsg = new Message { Role = "assistant", Content = fallback };
        session.AddMessage(fallbackMsg);
        if (_transcripts is not null)
            await _transcripts.SaveMessageAsync(session.Id, fallbackMsg, ct);
        return fallback;
    }

    private async Task<ToolResult> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolCall.Name, out var tool))
        {
            _logger.LogWarning("Unknown tool requested: {ToolName}", toolCall.Name);
            return ToolResult.Fail($"Unknown tool: {toolCall.Name}");
        }

        try
        {
            var parameters = JsonSerializer.Deserialize(toolCall.Arguments, tool.ParametersType, ToolArgJsonOptions)
                ?? throw new JsonException($"Failed to deserialize arguments for {toolCall.Name}");
            return await tool.ExecuteAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} failed", toolCall.Name);
            return ToolResult.Fail($"Tool execution failed: {ex.Message}", ex);
        }
    }

    private static JsonElement BuildParameterSchema(ITool tool)
    {
        // Build a JSON Schema from the tool's ParametersType using reflection
        var props = tool.ParametersType.GetProperties();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in props)
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var jsonType = propType switch
            {
                Type t when t == typeof(string) => "string",
                Type t when t == typeof(int) || t == typeof(long) => "integer",
                Type t when t == typeof(double) || t == typeof(float) => "number",
                Type t when t == typeof(bool) => "boolean",
                Type t when t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) => "array",
                _ => "string"
            };

            var propSchema = new Dictionary<string, object> { ["type"] = jsonType };

            // Check for Description attribute or XML doc
            var descAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                .FirstOrDefault() as System.ComponentModel.DescriptionAttribute;
            if (descAttr is not null)
                propSchema["description"] = descAttr.Description;

            properties[ToCamelCase(prop.Name)] = propSchema;

            // Non-nullable value types are required; nullable properties are not
            if (propType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null)
                required.Add(ToCamelCase(prop.Name));
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
            schema["required"] = required;

        var json = JsonSerializer.Serialize(schema);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static readonly JsonSerializerOptions ToolArgJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
