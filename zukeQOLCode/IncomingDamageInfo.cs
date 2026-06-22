namespace zukeQOL.zukeQOLCode;

public readonly record struct IncomingDamageInfo(
    int Raw,
    int Total,
    int Blocked,
    int BlockRemaining
);