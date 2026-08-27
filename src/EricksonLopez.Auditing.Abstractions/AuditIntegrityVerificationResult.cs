// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Represents the outcome of an audit chain cryptographic verification operation.</summary>
/// <param name="IsValid">A value indicating whether all audit records in the range passed integrity verification.</param>
/// <param name="VerifiedCount">The total number of audit records evaluated during verification.</param>
/// <param name="FirstFailedRecordId">The identifier of the first record that failed verification, if any; otherwise, <see langword="null"/>.</param>
/// <param name="FailureReason">A description of the failure reason if verification failed; otherwise, <see langword="null"/>.</param>
public sealed record AuditIntegrityVerificationResult(
    bool IsValid,
    int VerifiedCount,
    Guid? FirstFailedRecordId = null,
    string? FailureReason = null);
