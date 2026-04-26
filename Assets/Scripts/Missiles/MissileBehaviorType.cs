namespace LeiTing.Missiles
{
    public enum MissileBehaviorType
    {
        Straight = 1,
        Accelerate = 2,
        WeakHoming = 3,
        StrongHoming = 4,
        LockAndDash = 5,
        Curve = 6,
        Wave = 7,
        Split = 8,
        Explode = 9,
        Carrier = 10,
        Mine = 11,
        Return = 12
    }

    public enum MissileState
    {
        Idle,
        Launch,
        Warning,
        Flying,
        Tracking,
        Locking,
        Dashing,
        Splitting,
        Exploding,
        Dead
    }
}
