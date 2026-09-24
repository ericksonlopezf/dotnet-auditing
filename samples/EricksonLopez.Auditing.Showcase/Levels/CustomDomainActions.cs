// Copyright © Erickson Lopez. MIT License.
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Custom Domain Action Definitions (AuditAction is extensible, not a closed enum).
/// </summary>
public static class CustomDomainActions
{
    public static readonly AuditAction ProcessPayment = new("ProcessPayment");
    public static readonly AuditAction ExportGdprData = new("ExportGdprData");
    public static readonly AuditAction AuthorizeRefund = new("AuthorizeRefund");
    public static readonly AuditAction BulkUserImport = new("BulkUserImport");
}
