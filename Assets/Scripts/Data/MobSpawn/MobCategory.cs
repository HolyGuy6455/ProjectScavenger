using System;

[Flags]
public enum MobCategory
{
    None = 0,
    Animal = 1 << 0,
    Robot = 1 << 1,
    Human = 1 << 2,
    Alien = 1 << 3,
}
