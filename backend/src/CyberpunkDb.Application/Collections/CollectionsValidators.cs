using FluentValidation;

namespace CyberpunkDb.Application.Collections;

public sealed class GearListUpsertValidator : AbstractValidator<GearListUpsertRequest>
{
    public GearListUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Dê um nome à lista.")
            .MaximumLength(80).WithMessage("O nome aceita no máximo 80 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição aceita no máximo 500 caracteres.");
    }
}

public sealed class GearListEntryValidator : AbstractValidator<GearListEntryRequest>
{
    public GearListEntryValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage("Item inválido.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 999).WithMessage("Quantidade deve estar entre 1 e 999.");
        RuleFor(x => x.Note).MaximumLength(300).WithMessage("A anotação aceita no máximo 300 caracteres.");
    }
}

public sealed class GearListEntryUpdateValidator : AbstractValidator<GearListEntryUpdateRequest>
{
    public GearListEntryUpdateValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, 999).WithMessage("Quantidade deve estar entre 1 e 999.");
        RuleFor(x => x.Note).MaximumLength(300).WithMessage("A anotação aceita no máximo 300 caracteres.");
    }
}
