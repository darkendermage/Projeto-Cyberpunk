using System.ComponentModel.DataAnnotations;

namespace CyberpunkDb.Domain.Enums;

/// <summary>The three official ways to generate a Character (CP:R p.40).</summary>
public enum CreationMethod
{
    [Display(Name = "Streetrat (Templates)")] Streetrat,
    [Display(Name = "Edgerunner (Fast and Dirty)")] Edgerunner,
    [Display(Name = "The Complete Package (Calculated)")] CompletePackage,
}

public enum CharacterStatus
{
    /// <summary>Character generation not finalized yet; creation budgets still apply.</summary>
    [Display(Name = "Em criação")] Creating,
    /// <summary>Finalized with no blocking issue.</summary>
    [Display(Name = "Concluído")] Complete,
    /// <summary>Finalized, but a later edit left a blocking issue.</summary>
    [Display(Name = "Pendências")] NeedsAttention,
}

/// <summary>Kind of entry in a character's append-only history.</summary>
public enum CharacterEventKind
{
    [Display(Name = "Financeiro")] Ledger,
    [Display(Name = "Humanidade")] Humanity,
    [Display(Name = "Combate")] Combat,
    [Display(Name = "Lesão")] Injury,
    [Display(Name = "Progressão")] Progression,
    [Display(Name = "Edição")] Edit,
}
