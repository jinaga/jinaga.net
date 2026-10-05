#nullable disable

namespace Jinaga.Notebooks.Test.Models;

// Compiled without nullable annotations, so nothing distinguishes a predecessor
// that may be left out from one that may not. Neither is optional.

[FactType("Unannotated.Zone")]
public record Zone(string name);

[FactType("Unannotated.Post")]
public record Post(Zone zone, Zone backup);
