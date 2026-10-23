namespace OrderAccumulator.Risk;

public sealed record ExposureDecision(bool Accepted, decimal ResultingExposure, string? RejectionReason);
