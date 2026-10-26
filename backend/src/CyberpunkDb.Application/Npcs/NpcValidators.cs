using FluentValidation;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Npcs;

/// <summary>Shape checks of a save request. Game rules live in <see cref="Validation.NpcValidator"/>.</summary>
public sealed class NpcSaveRequestValidator : AbstractValidator<NpcSaveRequest>
{
    public NpcSaveRequestValidator()
    {
        RuleFor(x => x.Sheet).NotNull().WithMessage(L.T("Envie a ficha do NPC.", "Send the NPC sheet."));
        RuleFor(x => x.Sheet.Name).NotEmpty().WithMessage(L.T("Dê um nome ao NPC.", "Give the NPC a name."))
            .MaximumLength(80).WithMessage(L.T("O nome aceita no máximo 80 caracteres.", "The name takes at most 80 characters."));
        RuleFor(x => x.Sheet.Concept).MaximumLength(300).WithMessage(L.T("O conceito aceita no máximo 300 caracteres.", "The concept takes at most 300 characters."));
        RuleFor(x => x.Sheet.Notes).MaximumLength(4000).WithMessage(L.T("As anotações aceitam no máximo 4.000 caracteres.", "The notes take at most 4,000 characters."));
        RuleFor(x => x.Sheet.Skills.Count).LessThanOrEqualTo(150).WithMessage(L.T("Perícias demais na ficha.", "Too many skills on the sheet."));
        RuleFor(x => x.Sheet.Gear.Count).LessThanOrEqualTo(200).WithMessage(L.T("Uma ficha aceita no máximo 200 itens de equipamento.", "A sheet takes at most 200 gear items."));
        RuleFor(x => x.Sheet.Cyberware.Count).LessThanOrEqualTo(200).WithMessage(L.T("Uma ficha aceita no máximo 200 implantes.", "A sheet takes at most 200 implants."));
        RuleForEach(x => x.Sheet.Exceptions).ChildRules(e =>
        {
            e.RuleFor(x => x.Justification).NotEmpty().WithMessage(L.T("Toda exceção precisa de uma justificativa.", "Every exception needs a reason."))
                .MaximumLength(500).WithMessage(L.T("A justificativa aceita no máximo 500 caracteres.", "The reason takes at most 500 characters."));
        });
    }
}
