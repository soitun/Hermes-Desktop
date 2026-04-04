namespace Hermes.Agent.LLM;

using Hermes.Agent.Core;
using System.Runtime.CompilerServices;
using System.Text.Json;

public interface IChatClient
{
    /// <summary>
    /// Complete a conversation (non-streaming).
    /// </summary>
    Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct);
    
    /// <summary>
    /// Complete a conversation with streaming.
    /// </summary>
    IAsyncEnumerable<StreamEvent> StreamAsync(IEnumerable<Message> messages, CancellationToken ct = default);
    
    /// <summary>
    /// Complete with system prompt and tools.
    /// </summary>
    IAsyncEnumerable<StreamEvent> StreamAsync(
        string? systemPrompt,
        IEnumerable<Message> messages,
        IEnumerable<ToolDefinition>? tools = null,
        CancellationToken ct = default);
}

public sealed class LlmConfig
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
}

/// <summary>
/// Tool definition for LLM function calling.
/// </summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);
