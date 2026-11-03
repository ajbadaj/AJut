namespace AJut.TypeManagement
{
    using System;
    using System.Runtime.CompilerServices;

    // Whether this runtime can make code at runtime, which is what building a generic type or method from a Type needs
    //  when it was never compiled ahead of time. It is RuntimeFeature.IsDynamicCodeSupported: false on an iOS build
    //  compiled ahead of time without the interpreter, true anywhere with a JIT. The test runner always has a JIT, so
    //  tests stand in for the other kind of platform with SimulateUnsupported, on their own thread only.
    internal static class DynamicCodeAvailability
    {
        [ThreadStatic]
        private static bool g_isSimulatingUnsupported;

        // ===========[ Properties ]============================================
        /// <summary>Whether code can be made at runtime here: false on an iOS build compiled ahead of time</summary>
        /// <remarks>
        /// An ahead of time build (an iOS one, or NativeAOT) treats RuntimeFeature.IsDynamicCodeSupported as a constant false,
        /// and the trimmer and the NativeAOT compiler can cut away whatever only runs when this is true, but only if they can
        /// tell this is false too. The feature switch goes first for that. With the simulation field read first, neither
        /// tool can fold the property, since a branch on a field it can't evaluate stops both of them; with the switch
        /// first, the field is never reached. See ConstantExpressionMethodAnalyzer in ILLink's UnreachableBlocksOptimizer,
        /// and TryGetMethodConstantValue in ILC's SubstitutedILProvider, in dotnet/runtime.
        /// <para>
        /// [FeatureGuard(typeof(RequiresDynamicCodeAttribute))] would make both tools replace the body outright, but the
        /// analyzer rejects it on a body that also reads the simulation field (IL4000), so the order carries it alone.
        /// </para>
        /// </remarks>
        public static bool IsSupported => RuntimeFeature.IsDynamicCodeSupported && !g_isSimulatingUnsupported;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>For tests: this thread acts as a platform without dynamic code until the result is disposed</summary>
        public static IDisposable SimulateUnsupported ()
        {
            var scope = new SimulationScope(g_isSimulatingUnsupported);
            g_isSimulatingUnsupported = true;
            return scope;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class SimulationScope : IDisposable
        {
            private readonly bool m_wasSimulating;

            public SimulationScope (bool wasSimulating)
            {
                m_wasSimulating = wasSimulating;
            }

            public void Dispose ()
            {
                g_isSimulatingUnsupported = m_wasSimulating;
            }
        }
    }
}
