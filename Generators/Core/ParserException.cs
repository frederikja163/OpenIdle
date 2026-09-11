using System;

namespace Generator.Core;

/// <summary>
/// Raised when a contract in types.xml breaks one of the rules in
/// doc/backend/dto-contract.md — either a node's <c>Validate</c> or the cross-node checks in
/// <see cref="ValidationVisitor"/>. The pipeline surfaces it as DTC002.
/// </summary>
public sealed class ParserException(string message) : Exception(message);
