// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.Auditing.Tests.Common;

[ExcludeFromCodeCoverage]
internal sealed record FakeChangeDto(string Field, string? OldValue, string? NewValue, bool IsRedacted);
