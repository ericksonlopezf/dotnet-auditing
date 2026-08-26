// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;

Console.WriteLine("=================================================");
Console.WriteLine(" EricksonLopez.Auditing NativeAOT Test Suite     ");
Console.WriteLine("=================================================");

int passedTests = 0;

void Assert([DoesNotReturnIf(false)] bool condition, string testName)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName}");
        Console.ResetColor();
        Environment.Exit(1);
    }
    passedTests++;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[PASS] {testName}");
    Console.ResetColor();
}

// ── 1. AuditRecord & Enums Invariants ─────────────────────────────────────
Console.WriteLine("\n--- 1. AuditRecord & Actor Invariants ---");

var actor = new AuditActor(AuditActorType.User, "user-123", "Alice");
Assert(actor.Type == AuditActorType.User, "Actor type is User");
Assert(actor.Id == "user-123", "Actor Id matches");
Assert(actor.DisplayName == "Alice", "Actor DisplayName matches");

var action = AuditAction.Create;
Assert(action.Code == "Create", "AuditAction.Create has code 'Create'");

var resource = new AuditResource("Account", "acc-999");
Assert(resource.Type == "Account", "AuditResource.Type matches");
Assert(resource.Id == "acc-999", "AuditResource.Id matches");

// ── 2. AuditRecordBuilder & In-Memory Store ───────────────────────────────
Console.WriteLine("\n--- 2. AuditStore & Record Builder ---");

var record = AuditRecordBuilder.BuildDefault(
    tenantId: "tenant-corp",
    actorId: "user-123",
    resourceType: "Invoice",
    resourceId: "inv-001");

Assert(record.Context.TenantId == "tenant-corp", "AuditRecord tenant matches");
Assert(record.Actor.Id == "user-123", "AuditRecord actor matches");
Assert(record.Resource.Type == "Invoice", "AuditRecord resource matches");

var store = new InMemoryAuditStore();
await store.AppendAsync(record);

Assert(store.Count == 1, "InMemoryAuditStore appended 1 record");
var tenantRecords = store.ForTenant("tenant-corp");
Assert(tenantRecords.Count == 1, "store.ForTenant retrieves matching record");

Console.WriteLine("\n=================================================");
Console.WriteLine($" ALL {passedTests} NATIVE AOT SUITE TESTS PASSED SUCCESSFULLY! ");
Console.WriteLine("=== AOT Validator: OK ===");
Console.WriteLine("=================================================");
