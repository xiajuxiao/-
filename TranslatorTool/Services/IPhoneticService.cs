using TranslatorTool.Models;

namespace TranslatorTool.Services;

public interface IPhoneticService
{
    Task<List<PhoneticItem>> GetAmericanPhoneticsAsync(string text, CancellationToken cancellationToken);
}
