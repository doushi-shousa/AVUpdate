using System.Globalization;
using System.Windows.Controls;

namespace AVUpdate
{
    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return string.IsNullOrWhiteSpace(value?.ToString())
                ? new ValidationResult(false, "Поле не может быть пустым")
                : ValidationResult.ValidResult;
        }
    }

    public class ArchiveMaskValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string input = value?.ToString();
            return string.IsNullOrWhiteSpace(input) || !input.Contains("*")
                ? new ValidationResult(false, "Имя архива должно содержать маску (например, *.zip)")
                : ValidationResult.ValidResult;
        }
    }
}