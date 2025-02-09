﻿namespace ExtremeRoles.Module.PRNG;

public sealed class Xorshift64 : RNG64Base
{
    /*
        以下のURLの実装を元に実装
         https://ja.wikipedia.org/wiki/Xorshift
        
    */

    private ulong x;

    public Xorshift64(
        ulong seed, ulong state) : base(seed, state)
    { }

    public override ulong NextUInt64()
    {
        ulong x0 = x;

        x0 ^= x0 << 7;
        x0 ^= x0 >> 9;

        x = x0;

        return x;
    }

    protected override void Initialize(ulong seed, ulong initStete)
    {
        x = seed;

        while (x == 0)
        {
            x = RandomGenerator.CreateLongStrongSeed();
        }// at least one value must be non-zero
    }
}
