namespace MHRebornLauncher.Models;

public enum DiagnosticSeverity { Passed, Warning, Failed }

public sealed record DiagnosticResult(string Name, DiagnosticSeverity Severity, string Details);
