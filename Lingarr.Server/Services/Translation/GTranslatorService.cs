using System.Net;
using GTranslate.Translators;
using Lingarr.Server.Exceptions;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Services.Translation.Base;

namespace Lingarr.Server.Services.Translation;

public class GTranslatorService<T> : BaseLanguageService where T : ITranslator
{
    private readonly T _translator;

    public GTranslatorService(
        T translator,
        string languageFilePath,
        ISettingService settings,
        ILogger logger) : base(settings, logger, languageFilePath)
    {
        _translator = translator;
    }
    
    /// <inheritdoc />
    public override async Task<string> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _translator.TranslateAsync(text, sourceLanguage, targetLanguage);
            return result.Translation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Translator API error");
            throw new TranslationException("Translation failed using Google Translator API.", ex);
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
        // Google Translator API doesn't support context for translations, so we just call the regular method
        return await TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
    }
}