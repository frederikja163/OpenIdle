using System;

namespace Generator.Core;

/// <summary>
/// Raised when a contract in types.xml breaks one of the rules in
/// doc/backend/dto-contract.md. The parser surfaced it as DTC002; the current
/// XmlSerializer-based pipeline does not validate yet, so nothing throws it.
/// </summary>
public sealed class ParserException(string message) : Exception(message);
