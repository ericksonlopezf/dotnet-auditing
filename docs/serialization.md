# Serialization & Payload Invariants

## 1. Zero Direct Serialization in Core Kernel
`EricksonLopez.Mediator` operates purely on in-memory object references and does not serialize messages internally. When serializing messages for background persistence or logging, use `System.Text.Json` source generation (`JsonSerializerContext`) to maintain Native AOT compatibility.
