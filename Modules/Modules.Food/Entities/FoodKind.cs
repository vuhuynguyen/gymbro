namespace Modules.FoodModule.Entities;

/// <summary>
/// Discriminates the kind of catalog item so a single logging pipeline serves meals and supplements.
/// Persisted as <c>int</c> (see <c>FoodConfiguration</c>); values are load-bearing — do not renumber.
/// </summary>
public enum FoodKind
{
    Food = 1,
    Supplement = 2,
    Beverage = 3
}
