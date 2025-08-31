using System;

namespace SaltpeterProduction.Util;

public static class Extensions
{
    public static bool IsZero(this float x, float zeroForComparison = 1e-3f)
    {
        return MathF.Abs(x) <= zeroForComparison;
    }
}