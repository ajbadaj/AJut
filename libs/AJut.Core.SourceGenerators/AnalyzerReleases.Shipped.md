; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.5.0.118

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AJSON001 | AJson | Error | Type opted into AJson optimization has no usable constructor
AJSON002 | AJson | Error | Property has unsupported type for source generation
AJSON003 | AJson | Error | JsonOmitIfDefault explicit value type mismatch

## Release 1.6.0.145

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AJSON005 | AJson | Error | More than one constructor marked AJsonConstructor
AJSON006 | AJson | Warning | Constructor parameter matches no property
AJSON007 | AJson | Warning | Constructor parameter default differs from the property's JsonOmitIfDefault value

## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AJSON008 | AJson | Warning | Type opted into AJson optimization can't be reached by generated code

## Release 2.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AJSON009 | AJson | Warning | JsonIncludeReadOnlyInWrite names a member that isn't there
