using System.Runtime.CompilerServices;

namespace Evals.Harness;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Serial
{
    public const string Name = "Flect";
}
