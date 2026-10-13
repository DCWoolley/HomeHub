using System.Globalization;
using HomeHub.Web.Modules.Households;

namespace HomeHub.Web.Modules.MealPlanner;

internal static class IngredientQuantity
{
    public const decimal Maximum = 99999999.9999m;

    public static decimal Parse(string? value, string label)
    {
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var trimmed = value?.Trim() ?? "";
        var fractionStart = trimmed.IndexOf(separator, StringComparison.Ordinal);
        if (fractionStart >= 0 && trimmed.Length - fractionStart - separator.Length > 4)
            throw new HouseholdRuleException($"Use at most four decimal places for the {label}.");
        if (!decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.CurrentCulture, out var parsed))
            throw new HouseholdRuleException($"Enter a numeric {label} using the decimal separator shown in the example.");
        Validate(parsed, label);
        return parsed;
    }

    public static void Validate(decimal value, string label)
    {
        if (value <= 0 || value > Maximum || decimal.Round(value, 4) != value)
            throw new HouseholdRuleException($"Enter a {label} greater than zero, up to 99,999,999.9999, with at most four decimal places.");
    }
}
