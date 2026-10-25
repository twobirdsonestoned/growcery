using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Growcery.Api.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MaxDecimalPlacesAttribute : ValidationAttribute
{
    public MaxDecimalPlacesAttribute(int places)
        : base("{0} must have at most {1} decimal places.")
    {
        Places = places;
    }

    public int Places { get; }

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        decimal number => decimal.Round(number, Places) == number,
        _ => false,
    };

    public override string FormatErrorMessage(string name) =>
        string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Places);
}
