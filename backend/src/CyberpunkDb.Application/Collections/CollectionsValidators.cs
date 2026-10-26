using FluentValidation;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Collections;

public sealed class GearListUpsertValidator : AbstractValidator<GearListUpsertRequest>
{
    public GearListUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(L.T("Dê um nome à lista.", "Give the list a name."))
            .MaximumLength(80).WithMessage(L.T("O nome aceita no máximo 80 caracteres.", "The name takes at most 80 characters."));
        RuleFor(x => x.Description).MaximumLength(500).WithMessage(L.T("A descrição aceita no máximo 500 caracteres.", "The description takes at most 500 characters."));
    }
}

public sealed class GearListEntryValidator : AbstractValidator<GearListEntryRequest>
{
    public GearListEntryValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage(L.T("Item inválido.", "Invalid item."));
        RuleFor(x => x.Quantity).InclusiveBetween(1, 999).WithMessage(L.T("Quantidade deve estar entre 1 e 999.", "Quantity must be between 1 and 999."));
        RuleFor(x => x.Note).MaximumLength(300).WithMessage(L.T("A anotação aceita no máximo 300 caracteres.", "The note takes at most 300 characters."));
    }
}

public sealed class GearListEntryUpdateValidator : AbstractValidator<GearListEntryUpdateRequest>
{
    public GearListEntryUpdateValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, 999).WithMessage(L.T("Quantidade deve estar entre 1 e 999.", "Quantity must be between 1 and 999."));
        RuleFor(x => x.Note).MaximumLength(300).WithMessage(L.T("A anotação aceita no máximo 300 caracteres.", "The note takes at most 300 characters."));
    }
}
