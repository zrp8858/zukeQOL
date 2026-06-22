namespace zukeQOL.zukeQOLCode;

/// <summary>
///     Struct which simply stores basic information to be tracked
///     and displayed by the damage panel.
/// </summary>
/// <param name="Raw"></param>
/// <param name="Unblocked"></param>
/// <param name="Blocked"></param>
/// <param name="RemainingBlock"></param>
public readonly record struct IncomingDamageInfo(
    int Raw,
    int Unblocked,
    int Blocked,
    int RemainingBlock
);