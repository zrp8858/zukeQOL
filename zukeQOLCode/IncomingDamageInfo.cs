namespace zukeQOL.zukeQOLCode;

public readonly record struct IncomingDamageInfo(
    int Raw,
    int Unblocked,
    int Blocked,
    int RemainingBlock
);