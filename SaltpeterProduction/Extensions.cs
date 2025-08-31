using System;

namespace SaltpeterProduction;

public static class Extensions
{
    public static bool IsZero(this float x, float zeroForComparison = 1e-3f)
    {
        return Math.Abs(x) <= zeroForComparison;
    }
}