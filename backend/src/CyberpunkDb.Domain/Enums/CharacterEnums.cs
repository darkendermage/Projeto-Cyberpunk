using System.ComponentModel.DataAnnotations;

namespace CyberpunkDb.Domain.Enums;

// CP:R p.40
public enum CreationMethod
{
    [Display(Name = "Streetrat (Templates)")] Streetrat,
    [Display(Name = "Edgerunner (Fast and Dirty)")] Edgerunner,
    [Display(Name = "The Complete Package (Calculated)")] CompletePackage,
}

public enum CharacterStatus
{
    // creation budgets still apply
    [Display(Name = "Em criação")] Creating,
    [Display(Name = "Concluído")] Complete,
    // a later edit broke something
    [Display(Name = "Pendências")] NeedsAttention,
}

public enum CharacterEventKind
{
    [Display(Name = "Financeiro")] Ledger,
    [Display(Name = "Humanidade")] Humanity,
    [Display(Name = "Combate")] Combat,
    [Display(Name = "Lesão")] Injury,
    [Display(Name = "Progressão")] Progression,
    [Display(Name = "Edição")] Edit,
}
