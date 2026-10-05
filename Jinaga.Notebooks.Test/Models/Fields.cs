namespace Jinaga.Notebooks.Test.Models;

// One field of every nullable kind Jinaga stores, so that a graph of a fact
// whose fields are all null shows how each kind of null is displayed.

[FactType("Optional.Measurement")]
public record Measurement(
    Site site,
    int? count,
    double? average,
    bool? valid,
    DateTime? takenAt,
    Guid? token,
    string? note);
