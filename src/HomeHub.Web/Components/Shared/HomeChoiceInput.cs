using System.ComponentModel.DataAnnotations;
using HomeHub.Application.Features.Homes.JoinHome;

namespace HomeHub.Web.Components.Shared;

public class HomeChoiceInput : IValidatableObject
{
    [StringLength(100)]
    public string? HomeName { get; set; }

    [StringLength(32)]
    public string? InvitationCode { get; set; }

    public HomeChoice ToChoice() => new(HomeName, InvitationCode);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ToChoice().Validate() is { } error)
            yield return new ValidationResult(error, [nameof(HomeName), nameof(InvitationCode)]);
    }
}
