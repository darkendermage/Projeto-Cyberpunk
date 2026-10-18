using FluentValidation;

namespace CyberpunkDb.Application.Npcs;

/// <summary>Shape checks of a save request. Game rules live in <see cref="Validation.NpcValidator"/>.</summary>
public sealed class NpcSaveRequestValidator : AbstractValidator<NpcSaveRequest>
{
    public NpcSaveRequestValidator()
    {
        RuleFor(x => x.Sheet).NotNull().WithMessage("Envie a ficha do NPC.");
        RuleFor(x => x.Sheet.Name).NotEmpty().WithMessage("Dê um nome ao NPC.")
            .MaximumLength(80).WithMessage("O nome aceita no máximo 80 caracteres.");
        RuleFor(x => x.Sheet.Concept).MaximumLength(300).WithMessage("O conceito aceita no máximo 300 caracteres.");
        RuleFor(x => x.Sheet.Notes).MaximumLength(4000).WithMessage("As anotações aceitam no máximo 4.000 caracteres.");
        RuleFor(x => x.Sheet.Skills.Count).LessThanOrEqualTo(150).WithMessage("Perícias demais na ficha.");
        RuleFor(x => x.Sheet.Gear.Count).LessThanOrEqualTo(200).WithMessage("Uma ficha aceita no máximo 200 itens de equipamento.");
        RuleFor(x => x.Sheet.Cyberware.Count).LessThanOrEqualTo(200).WithMessage("Uma ficha aceita no máximo 200 implantes.");
        RuleForEach(x => x.Sheet.Exceptions).ChildRules(e =>
        {
            e.RuleFor(x => x.Justification).NotEmpty().WithMessage("Toda exceção precisa de uma justificativa.")
                .MaximumLength(500).WithMessage("A justificativa aceita no máximo 500 caracteres.");
        });
    }
}
