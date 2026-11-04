using System.Text.Json.Nodes;

namespace Flect.Text.Json;

internal sealed class NaturalSort : IComparer<string>
{
    public static readonly NaturalSort Instance = new();

    public int Compare(string? left, string? right)
    {
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;

        int a = 0;
        int b = 0;

        while (a < left.Length && b < right.Length)
        {
            if (char.IsDigit(left[a]) && char.IsDigit(right[b]))
            {
                int startA = a;
                int startB = b;
                while (a < left.Length && char.IsDigit(left[a])) a++;
                while (b < right.Length && char.IsDigit(right[b])) b++;

                var numberA = left.AsSpan(startA, a - startA).TrimStart('0');
                var numberB = right.AsSpan(startB, b - startB).TrimStart('0');

                if (numberA.Length != numberB.Length) return numberA.Length - numberB.Length;

                int digits = numberA.SequenceCompareTo(numberB);
                if (digits != 0) return digits;
                continue;
            }

            int letters = left[a].CompareTo(right[b]);
            if (letters != 0) return letters;
            a++;
            b++;
        }

        return (left.Length - a) - (right.Length - b);
    }
}
