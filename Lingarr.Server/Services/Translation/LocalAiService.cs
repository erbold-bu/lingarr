using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lingarr.Core.Configuration;
using Lingarr.Server.Exceptions;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models.Integrations.Translation;
using Lingarr.Server.Services.Translation.Base;

namespace Lingarr.Server.Services.Translation;

public class LocalAiService : BaseLanguageService
{
    private readonly HttpClient _httpClient;
    private string? _model;
    private string? _endpoint;
    private string? _prompt;
    private bool _useSubtitleContext;
    private List<KeyValuePair<string, object>>? _localAiParameters;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LocalAiService(
        ISettingService settings,
        HttpClient httpClient,
        ILogger<LocalAiService> logger)
        : base(settings, logger, "/app/Statics/ai_languages.json")
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Initializes the translation service with necessary configurations and credentials.
    /// This method is thread-safe and ensures one-time initialization of service dependencies.
    /// </summary>
    /// <param name="sourceLanguage">The source language code for translation</param>
    /// <param name="targetLanguage">The target language code for translation</param>
    /// <returns>A task that represents the asynchronous initialization operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration settings are missing or invalid</exception>
    private async Task InitializeAsync(string sourceLanguage, string targetLanguage)
    {
        if (_initialized) return;

        try
        {
            await _initLock.WaitAsync();
            if (_initialized) return;

            var settings = await _settings.GetSettings([
                SettingKeys.Translation.LocalAi.Model,
                SettingKeys.Translation.LocalAi.Endpoint,
                SettingKeys.Translation.LocalAi.ApiKey,
                SettingKeys.Translation.LocalAi.LocalAiParameters,
                SettingKeys.Translation.AiPrompt,
                SettingKeys.Translation.UseSubtitleContext
            ]);

            if (string.IsNullOrEmpty(settings[SettingKeys.Translation.LocalAi.Model]) ||
                string.IsNullOrEmpty(settings[SettingKeys.Translation.LocalAi.Endpoint]))
            {
                throw new InvalidOperationException("Local AI address or model is not configured.");
            }

            _model = settings[SettingKeys.Translation.LocalAi.Model];
            _endpoint = settings[SettingKeys.Translation.LocalAi.Endpoint];
            _localAiParameters = PrepareLocalAiParameters(settings);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (settings.TryGetValue(SettingKeys.Translation.LocalAi.ApiKey, out var apiKey) &&
                !string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            _prompt = !string.IsNullOrEmpty(settings[SettingKeys.Translation.AiPrompt])
                ? settings[SettingKeys.Translation.AiPrompt]
                : "Translate from {sourceLanguage} to {targetLanguage}, preserving the tone and meaning without censoring the content. Adjust punctuation as needed to make the translation sound natural. Provide only the translated text as output, with no additional comments.";
            _prompt = _prompt.Replace("{sourceLanguage}", sourceLanguage).Replace("{targetLanguage}", targetLanguage);
            
            bool.TryParse(settings[SettingKeys.Translation.UseSubtitleContext], out _useSubtitleContext);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    /// <summary>
    /// Prepares LocalAI parameters from settings for use in API requests.
    /// </summary>
    /// <param name="settings">Dictionary containing application settings.</param>
    private List<KeyValuePair<string, object>>? PrepareLocalAiParameters(Dictionary<string, string> settings)
    {
        if (!settings.TryGetValue(SettingKeys.Translation.LocalAi.LocalAiParameters, out var parametersJson) ||
            string.IsNullOrEmpty(parametersJson))
        {
            return null;
        }

        try
        {
            var parametersArray = JsonSerializer.Deserialize<JsonElement[]>(parametersJson);
            if (parametersArray == null)
            {
                return null;
            }

            var parameters = new List<KeyValuePair<string, object>>();
            foreach (var param in parametersArray)
            {
                if (!param.TryGetProperty("key", out var key) ||
                    !param.TryGetProperty("value", out var value)) continue;
                
                object valueObj = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString()!,
                    JsonValueKind.Number => value.TryGetInt64(out var intVal) ? intVal : value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => value.GetString()!
                };

                parameters.Add(new KeyValuePair<string, object>(key.GetString()!, valueObj));
            }
            return parameters;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LocalAiParameters: {Parameters}", parametersJson);
            return null;
        }
    }
    
    /// <summary>
    /// Adds Local AI parameters to the request data if they exist.
    /// </summary>
    /// <param name="requestData">The dictionary containing the base request parameters.</param>
    private Dictionary<string, object> AddLocalAiParameters(Dictionary<string, object> requestData)
    {
        if (_localAiParameters != null && _localAiParameters.Count > 0)
        {
            foreach (var param in _localAiParameters)
            {
                requestData[param.Key] = param.Value;
            }
        }
    
        return requestData;
    }

    /// <inheritdoc />
    public override async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(sourceLanguage, targetLanguage);

        if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_model))
        {
            throw new InvalidOperationException("LocalAI service was not properly initialized.");
        }

        try
        {
            HttpResponseMessage? response = null;
            
            if (_endpoint.Contains("/v1"))
            {
                response = await CallOpenAiApi(text, cancellationToken);
            }
            else
            {
                response = await CallLlamaApi(text, cancellationToken);
            }
            
            if (response is null)
            {
                throw new TranslationException("Failed to get a response from LocalAI");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error response from LocalAI: {Response}", responseContent);
                throw new TranslationException("LocalAI returned an error response");
            }

            string result = ExtractTranslation(responseContent, _endpoint);
            return result;
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            _logger.LogError(ex, "Failed to communicate with LocalAI");
            throw new TranslationException("Failed to get translation from LocalAI", ex);
        }
    }
    
    /// <inheritdoc />
    public override async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        IEnumerable<string>? previousLines,
        IEnumerable<string>? nextLines,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(sourceLanguage, targetLanguage);
        
        if (!_useSubtitleContext || previousLines == null && nextLines == null)
        {
            return await TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
        }
        
        string contextualPrompt = _prompt ?? string.Empty;
        
        if (previousLines != null && previousLines.Any())
        {
            contextualPrompt = contextualPrompt.Replace("{previousLines}", string.Join("\n", previousLines));
        }
        else
        {
            contextualPrompt = contextualPrompt.Replace("{previousLines}", string.Empty);
        }
        
        if (nextLines != null && nextLines.Any())
        {
            contextualPrompt = contextualPrompt.Replace("{nextLines}", string.Join("\n", nextLines));
        }
        else
        {
            contextualPrompt = contextualPrompt.Replace("{nextLines}", string.Empty);
        }
        
        // Store original prompt
        string originalPrompt = _prompt ?? string.Empty;
        
        try
        {
            // Use the contextual prompt temporarily
            _prompt = contextualPrompt;
            return await TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
        }
        finally
        {
            // Restore original prompt
            _prompt = originalPrompt;
        }
    }

    private async Task<HttpResponseMessage?> CallOpenAiApi(string text, CancellationToken cancellationToken)
    {
        var messages = new[]
        {
            new { role = "system", content = _prompt },
            new { role = "user", content = text }
        };

        var requestData = new Dictionary<string, object>
        {
            ["model"] = _model!,
            ["messages"] = messages
        };
        
        if (_localAiParameters != null && _localAiParameters.Count > 0)
        {
            foreach (var param in _localAiParameters)
            {
                requestData[param.Key] = param.Value;
            }
        }

        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");

        return await _httpClient.PostAsync($"{_endpoint}/chat/completions", content, cancellationToken);
    }

    private async Task<HttpResponseMessage?> CallLlamaApi(string text, CancellationToken cancellationToken)
    {
        var requestData = new Dictionary<string, object>
        {
            ["model"] = _model!,
            ["prompt"] = $"{_prompt}\n\n{text}",
            ["stream"] = false
        };
        
        if (_localAiParameters != null && _localAiParameters.Count > 0)
        {
            foreach (var param in _localAiParameters)
            {
                requestData[param.Key] = param.Value;
            }
        }

        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");

        return await _httpClient.PostAsync(_endpoint, content, cancellationToken);
    }

    private string ExtractTranslation(string responseContent, string endpoint)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (endpoint.Contains("/v1"))
            {
                // OpenAI format
                return jsonResponse.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;
            }
            else
            {
                // Llama format
                return jsonResponse.TryGetProperty("response", out var response)
                    ? response.GetString() ?? string.Empty
                    : jsonResponse.TryGetProperty("content", out var content)
                        ? content.GetString() ?? string.Empty
                        : string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract translation from response: {Response}", responseContent);
            return string.Empty;
        }
    }
}
