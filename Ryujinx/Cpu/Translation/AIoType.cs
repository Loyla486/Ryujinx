using System;

namespace ChocolArm64.Translation
{
    [Flags]
    enum AIoType
    {
        Arg,
        Fields,
        Flag,
        Int,
        Float,
        Vector
    }
}