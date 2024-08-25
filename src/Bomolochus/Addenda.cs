using System.Collections.Immutable;

namespace Bomolochus;

public record Addenda(double Certainty, ImmutableArray<string> Notes)
{
    public static Addenda Empty => new(1, ImmutableArray<string>.Empty);

    public static Addenda operator +(Addenda left, Addenda right)
        => new(
            left.Certainty * right.Certainty, 
            left.Notes.AddRange(right.Notes)
            );
}