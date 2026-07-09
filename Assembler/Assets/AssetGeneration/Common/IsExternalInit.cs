namespace System.Runtime.CompilerServices
{
    // Shared polyfill for `init`-only setters and records. Public so every AssetGeneration
    // asmdef that references Assembler.AssetGeneration.Common gets it, replacing the per-folder
    // internal copies that used to be duplicated across these assemblies.
    public static class IsExternalInit { }
}
