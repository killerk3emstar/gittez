namespace GitBounty.Core.Models;

// Points == null oznacza "za mało danych" - wynik procentuje się wtedy po
// dostępnych komponentach (SPEC §6.2), a nie zeruje.
public sealed record ScoreComponent(
    string Key,
    string Label,
    double? Points,
    double MaxPoints,
    string RawValue,
    string Explanation,
    bool IsSampled = false);
