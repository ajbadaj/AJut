; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AJSON001 | AJson | Error | Type opted into AJson optimization has no usable constructor
AJSON002 | AJson | Error | Property has unsupported type for source generation
AJSON003 | AJson | Error | JsonOmitIfDefault explicit value type mismatch
AJSON005 | AJson | Error | More than one constructor marked AJsonConstructor
AJSON006 | AJson | Warning | Constructor parameter matches no property
AJSON007 | AJson | Warning | Constructor parameter default differs from the property's JsonOmitIfDefault value
